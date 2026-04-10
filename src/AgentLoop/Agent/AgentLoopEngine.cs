using System.Text.Json;
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
    private readonly IWorkspaceFileOperations _workspace;
    private readonly IToolInvocationObserver _toolObserver;
    private readonly string _modelId;
    private readonly string _systemPrompt;

    public AgentLoopEngine(
        AnthropicClient client,
        IBashRunner bash,
        IWorkspaceFileOperations workspace,
        string modelId,
        string systemPrompt,
        IToolInvocationObserver? toolObserver = null
    )
    {
        _client = client;
        _bash = bash;
        _workspace = workspace;
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
        var normalized = MessageNormalizer.NormalizeForApi(state.Messages);
        var request = new MessageCreateParams
        {
            Model = _modelId,
            MaxTokens = 8000,
            System = _systemPrompt,
            Messages = normalized,
            Tools = AgentToolDefinitions.CreateToolUnions(),
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
            if (!block.TryPickToolUse(out var tool))
                continue;

            var output = ExecuteSingleTool(tool);
            var preview = output.Length <= ToolOutputPreviewLength ? output : output[..ToolOutputPreviewLength];
            _toolObserver.OnToolInvocation(tool.Name, DescribeToolInvocation(tool), preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return list.ToArray();
    }

    string ExecuteSingleTool(ToolUseBlock tool)
    {
        var input = tool.Input;
        return tool.Name switch
        {
            "bash" => RunBashTool(input),
            "read_file" => RunReadFileTool(input),
            "write_file" => RunWriteFileTool(input),
            "edit_file" => RunEditFileTool(input),
            _ => $"Unknown tool: {tool.Name}",
        };
    }

    string RunBashTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("command", out var cmdEl))
            throw new AgentLoopException("bash 工具调用缺少 command 字段。");

        var command = cmdEl.GetString();
        if (string.IsNullOrEmpty(command))
            throw new AgentLoopException("bash 工具的 command 无效。");

        return _bash.Run(command);
    }

    string RunReadFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: read_file 缺少 path。";

        var limit = TryGetOptionalPositiveInt(input, "limit");
        return _workspace.ReadFile(path, limit);
    }

    string RunWriteFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: write_file 缺少 path。";

        if (!TryGetStringContent(input, "content", out var content))
            return "Error: write_file 缺少 content。";

        return _workspace.WriteFile(path, content);
    }

    string RunEditFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: edit_file 缺少 path。";

        if (!TryGetRequiredString(input, "old_text", out var oldText))
            return "Error: edit_file 缺少 old_text。";

        if (!TryGetRequiredString(input, "new_text", out var newText))
            return "Error: edit_file 缺少 new_text。";

        return _workspace.EditFile(path, oldText, newText);
    }

    static string DescribeToolInvocation(ToolUseBlock tool)
    {
        return tool.Name switch
        {
            "bash" => GetOptionalString(tool.Input, "command") ?? "",
            "read_file" => GetOptionalString(tool.Input, "path") ?? "",
            "write_file" => GetOptionalString(tool.Input, "path") ?? "",
            "edit_file" => GetOptionalString(tool.Input, "path") ?? "",
            _ => "",
        };
    }

    static bool TryGetRequiredString(
        IReadOnlyDictionary<string, JsonElement> input,
        string key,
        out string value
    )
    {
        value = "";
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;

        value = el.GetString() ?? "";
        return value.Length > 0;
    }

    static bool TryGetStringContent(IReadOnlyDictionary<string, JsonElement> input, string key, out string value)
    {
        value = "";
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;

        value = el.GetString() ?? "";
        return true;
    }

    static string? GetOptionalString(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        return el.GetString();
    }

    static int? TryGetOptionalPositiveInt(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el))
            return null;
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        try
        {
            return el.GetInt32();
        }
        catch
        {
            return null;
        }
    }
}
