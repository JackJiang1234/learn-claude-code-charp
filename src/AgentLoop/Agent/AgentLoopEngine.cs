using Anthropic;
using Anthropic.Models.Messages;
using AgentLoop.Bash;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

public sealed class AgentLoopEngine
{
    private const int ToolOutputPreviewLength = 200;

    private readonly AnthropicClient _client;
    private readonly IBashRunner _bash;
    private readonly IToolInvocationObserver _toolObserver;
    private readonly string _modelId;
    private readonly string _systemPrompt;

    public AgentLoopEngine(
        AnthropicClient client,
        IBashRunner bash,
        string modelId,
        string systemPrompt,
        IToolInvocationObserver? toolObserver = null
    )
    {
        _client = client;
        _bash = bash;
        _modelId = modelId;
        _systemPrompt = systemPrompt;
        _toolObserver = toolObserver ?? NoOpToolInvocationObserver.Instance;
    }

    public async Task RunAgentLoopAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        while (await RunOneTurnAsync(state, cancellationToken).ConfigureAwait(false)) { }
    }

    public async Task<bool> RunOneTurnAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        var response = await CallMessagesApiAsync(state, cancellationToken).ConfigureAwait(false);
        AppendAssistantMessage(state, response);

        if (response.StopReason is not { } sr || sr.Value() != StopReason.ToolUse)
        {
            state.TransitionReason = null;
            return false;
        }

        var results = ExecuteToolCalls(response.Content);
        if (results.Length == 0)
        {
            state.TransitionReason = null;
            return false;
        }

        state.Messages.Add(new MessageParam { Role = Role.User, Content = new MessageParamContent(results) });
        state.TurnCount++;
        state.TransitionReason = "tool_result";
        return true;
    }

    async Task<Message> CallMessagesApiAsync(LoopState state, CancellationToken cancellationToken)
    {
        var request = new MessageCreateParams
        {
            Model = _modelId,
            MaxTokens = 8000,
            System = _systemPrompt,
            Messages = state.Messages,
            Tools = [BashToolDefinition.CreateTool()],
        };

        try
        {
            return await _client.Messages.Create(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new AgentLoopException("调用 Anthropic Messages API 失败。", ex);
        }
    }

    static void AppendAssistantMessage(LoopState state, Message response)
    {
        state.Messages.Add(
            new MessageParam
            {
                Role = Role.Assistant,
                Content = new MessageParamContent(
                    ContentMapping.MapAssistantBlocksToParams(response.Content)
                ),
            }
        );
    }

    ContentBlockParam[] ExecuteToolCalls(IReadOnlyList<ContentBlock> responseContent)
    {
        var list = new List<ContentBlockParam>();
        foreach (var block in responseContent)
        {
            if (!block.TryPickToolUse(out var tool) || tool.Name != "bash")
                continue;

            if (!tool.Input.TryGetValue("command", out var cmdEl))
                throw new AgentLoopException("bash 工具调用缺少 command 字段。");

            var command = cmdEl.GetString();
            if (string.IsNullOrEmpty(command))
                throw new AgentLoopException("bash 工具的 command 无效。");

            var output = _bash.Run(command);
            var preview = output.Length <= ToolOutputPreviewLength ? output : output[..ToolOutputPreviewLength];
            _toolObserver.OnToolInvocation(command, preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return list.ToArray();
    }
}
