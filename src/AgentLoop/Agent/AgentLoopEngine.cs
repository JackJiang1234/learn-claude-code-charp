using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using AgentLoop.Bash;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

public sealed class AgentLoopEngine
{
    private readonly AnthropicClient _client;
    private readonly IBashRunner _bash;
    private readonly string _modelId;
    private readonly string _systemPrompt;

    public AgentLoopEngine(
        AnthropicClient client,
        IBashRunner bash,
        string modelId,
        string systemPrompt
    )
    {
        _client = client;
        _bash = bash;
        _modelId = modelId;
        _systemPrompt = systemPrompt;
    }

    public async Task RunAgentLoopAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        while (await RunOneTurnAsync(state, cancellationToken).ConfigureAwait(false)) { }
    }

    public async Task<bool> RunOneTurnAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        var request = new MessageCreateParams
        {
            Model = _modelId,
            MaxTokens = 8000,
            System = _systemPrompt,
            Messages = state.Messages,
            Tools = [CreateBashTool()],
        };

        Message response;
        try
        {
            response = await _client
                .Messages.Create(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new AgentLoopException("调用 Anthropic Messages API 失败。", ex);
        }

        state.Messages.Add(
            new MessageParam
            {
                Role = Role.Assistant,
                Content = new MessageParamContent(
                    ContentMapping.MapAssistantBlocksToParams(response.Content)
                ),
            }
        );

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

        state.Messages.Add(
            new MessageParam { Role = Role.User, Content = new MessageParamContent(results) }
        );

        state.TurnCount++;
        state.TransitionReason = "tool_result";
        return true;
    }

    static Tool CreateBashTool() =>
        new()
        {
            Name = "bash",
            Description = "Run a shell command in the current workspace.",
            InputSchema = CreateBashInputSchema(),
        };

    static InputSchema CreateBashInputSchema()
    {
        var root = JsonSerializer.SerializeToElement(
            new
            {
                type = "object",
                properties = new { command = new { type = "string" } },
                required = new[] { "command" },
            }
        );
        var dict = new Dictionary<string, JsonElement>();
        foreach (var p in root.EnumerateObject())
            dict[p.Name] = p.Value;
        return InputSchema.FromRawUnchecked(dict);
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

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("$ " + command);
            Console.ResetColor();

            var output = _bash.Run(command);
            var preview = output.Length <= 200 ? output : output[..200];
            Console.WriteLine(preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return list.ToArray();
    }
}
