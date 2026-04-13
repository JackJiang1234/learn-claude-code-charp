using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using AgentLoop.Bash;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

public sealed class AgentLoopEngine
{
    const int ToolOutputPreviewLength = 200;
    const int MaxSubagentTurns = 30;
    const int MaxToolOutputLength = 50000;

    private readonly AnthropicClient _client;
    private readonly IBashRunner _bash;
    private readonly IWorkspaceFileOperations _workspace;
    private readonly IToolInvocationObserver _toolObserver;
    private readonly string _modelId;
    private readonly string _systemPrompt;
    private readonly string _subagentSystemPrompt;

    public AgentLoopEngine(
        AnthropicClient client,
        IBashRunner bash,
        IWorkspaceFileOperations workspace,
        string modelId,
        string systemPrompt,
        string subagentSystemPrompt,
        IToolInvocationObserver? toolObserver = null
    )
    {
        _client = client;
        _bash = bash;
        _workspace = workspace;
        _modelId = modelId;
        _systemPrompt = systemPrompt;
        _subagentSystemPrompt = subagentSystemPrompt;
        _toolObserver = toolObserver ?? NoOpToolInvocationObserver.Instance;
    }

    public async Task RunAgentLoopAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        while (await RunOneTurnAsync(state, cancellationToken).ConfigureAwait(false)) { }
    }

    public async Task<bool> RunOneTurnAsync(LoopState state, CancellationToken cancellationToken = default)
    {
        var response = await CallMessagesApiAsync(
                state,
                _systemPrompt,
                AgentToolDefinitions.CreateParentToolUnions(),
                cancellationToken
            )
            .ConfigureAwait(false);
        AppendAssistantMessage(state, response);

        if (response.StopReason is not { } sr || sr.Value() != StopReason.ToolUse)
        {
            state.TransitionReason = null;
            return false;
        }

        var (results, usedTodo) = await ExecuteParentToolCallsAsync(state, response.Content, cancellationToken)
            .ConfigureAwait(false);
        if (results.Length == 0)
        {
            state.TransitionReason = null;
            return false;
        }

        if (usedTodo)
            state.TodoPlanning.RoundsSinceUpdate = 0;
        else
        {
            TodoSessionPlanner.NoteRoundWithoutTodoUpdate(state.TodoPlanning);
            var reminder = TodoSessionPlanner.TryGetReminder(state.TodoPlanning);
            if (reminder is not null)
            {
                var withReminder = new List<ContentBlockParam>(results.Length + 1) { new TextBlockParam(reminder), };
                withReminder.AddRange(results);
                results = withReminder.ToArray();
            }
        }

        state.Messages.Add(new MessageParam { Role = Role.User, Content = new MessageParamContent(results) });
        state.TurnCount++;
        state.TransitionReason = "tool_result";
        return true;
    }

    async Task<Message> CallMessagesApiAsync(
        LoopState state,
        string systemPrompt,
        IReadOnlyList<ToolUnion> tools,
        CancellationToken cancellationToken
    )
    {
        var normalized = MessageNormalizer.NormalizeForApi(state.Messages);
        var request = new MessageCreateParams
        {
            Model = _modelId,
            MaxTokens = 8000,
            System = systemPrompt,
            Messages = normalized,
            Tools = tools,
        };

        try
        {
            return await _client.Messages.Create(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new AgentLoopException("Failed to call Anthropic Messages API.", ex);
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

    async Task<(ContentBlockParam[] Results, bool UsedTodo)> ExecuteParentToolCallsAsync(
        LoopState state,
        IReadOnlyList<ContentBlock> responseContent,
        CancellationToken cancellationToken
    )
    {
        var list = new List<ContentBlockParam>();
        var usedTodo = false;
        foreach (var block in responseContent)
        {
            if (!block.TryPickToolUse(out var tool))
                continue;

            if (tool.Name == "todo")
                usedTodo = true;

            string output;
            try
            {
                if (tool.Name == "task")
                    output = await RunTaskToolAsync(tool.Input, cancellationToken).ConfigureAwait(false);
                else
                    output = ExecuteParentTool(state, tool);
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.Message}";
            }

            output = TruncateOutput(output);
            var preview = output.Length <= ToolOutputPreviewLength ? output : output[..ToolOutputPreviewLength];
            _toolObserver.OnToolInvocation(tool.Name, DescribeToolInvocation(tool), preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return (list.ToArray(), usedTodo);
    }

    async Task<string> RunTaskToolAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        if (!input.TryGetValue("prompt", out var promptEl) || promptEl.ValueKind != JsonValueKind.String)
            return "Error: task is missing prompt.";

        var prompt = promptEl.GetString();
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: task is missing prompt.";

        return await RunSubagentAsync(prompt.Trim(), cancellationToken).ConfigureAwait(false);
    }

    async Task<string> RunSubagentAsync(string prompt, CancellationToken cancellationToken)
    {
        var subMessages = new List<MessageParam> { new() { Role = Role.User, Content = prompt } };
        var subState = new LoopState(subMessages, new TodoPlanningState());

        for (var i = 0; i < MaxSubagentTurns; i++)
        {
            var response = await CallMessagesApiAsync(
                    subState,
                    _subagentSystemPrompt,
                    AgentToolDefinitions.CreateChildToolUnions(),
                    cancellationToken
                )
                .ConfigureAwait(false);

            AppendAssistantMessage(subState, response);

            if (response.StopReason is not { } sr || sr.Value() != StopReason.ToolUse)
            {
                var text = ExtractTextFromMessage(response);
                return text.Length > 0 ? text : "(no summary)";
            }

            var results = ExecuteChildToolCalls(response.Content);
            if (results.Length == 0)
                return "(no summary)";

            subState.Messages.Add(new MessageParam { Role = Role.User, Content = new MessageParamContent(results) });
            subState.TurnCount++;
        }

        return "(no summary)";
    }

    ContentBlockParam[] ExecuteChildToolCalls(IReadOnlyList<ContentBlock> responseContent)
    {
        var list = new List<ContentBlockParam>();
        foreach (var block in responseContent)
        {
            if (!block.TryPickToolUse(out var tool))
                continue;

            string output;
            try
            {
                output = ExecuteChildTool(tool);
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.Message}";
            }

            output = TruncateOutput(output);
            var preview = output.Length <= ToolOutputPreviewLength ? output : output[..ToolOutputPreviewLength];
            _toolObserver.OnToolInvocation(tool.Name, DescribeToolInvocation(tool), preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return list.ToArray();
    }

    static string TruncateOutput(string output) =>
        output.Length <= MaxToolOutputLength ? output : output[..MaxToolOutputLength];

    static string ExtractTextFromMessage(Message response)
    {
        var sb = new StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var text))
                sb.Append(text.Text);
        }

        return sb.ToString().Trim();
    }

    string ExecuteParentTool(LoopState state, ToolUseBlock tool)
    {
        return tool.Name switch
        {
            "bash" => RunBashTool(tool.Input),
            "read_file" => RunReadFileTool(tool.Input),
            "write_file" => RunWriteFileTool(tool.Input),
            "edit_file" => RunEditFileTool(tool.Input),
            "todo" => RunTodoTool(state.TodoPlanning, tool.Input),
            _ => $"Unknown tool: {tool.Name}",
        };
    }

    string ExecuteChildTool(ToolUseBlock tool)
    {
        return tool.Name switch
        {
            "bash" => RunBashTool(tool.Input),
            "read_file" => RunReadFileTool(tool.Input),
            "write_file" => RunWriteFileTool(tool.Input),
            "edit_file" => RunEditFileTool(tool.Input),
            _ => $"Unknown tool: {tool.Name}",
        };
    }

    static string RunTodoTool(TodoPlanningState todoState, IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("items", out var itemsEl))
            return "Error: todo is missing items.";

        return TodoSessionPlanner.Update(todoState, itemsEl);
    }

    string RunBashTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("command", out var cmdEl))
            throw new AgentLoopException("bash tool call is missing the command field.");

        var command = cmdEl.GetString();
        if (string.IsNullOrEmpty(command))
            throw new AgentLoopException("bash tool command is invalid.");

        return _bash.Run(command);
    }

    string RunReadFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: read_file is missing path.";

        var limit = TryGetOptionalPositiveInt(input, "limit");
        return _workspace.ReadFile(path, limit);
    }

    string RunWriteFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: write_file is missing path.";

        if (!TryGetStringContent(input, "content", out var content))
            return "Error: write_file is missing content.";

        return _workspace.WriteFile(path, content);
    }

    string RunEditFileTool(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!TryGetRequiredString(input, "path", out var path))
            return "Error: edit_file is missing path.";

        if (!TryGetRequiredString(input, "old_text", out var oldText))
            return "Error: edit_file is missing old_text.";

        if (!TryGetRequiredString(input, "new_text", out var newText))
            return "Error: edit_file is missing new_text.";

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
            "todo" => "todo",
            "task" => DescribeTaskInvocation(tool.Input),
            _ => "",
        };
    }

    static string DescribeTaskInvocation(IReadOnlyDictionary<string, JsonElement> input)
    {
        var desc = GetOptionalString(input, "description") ?? "subtask";
        var prompt = "";
        if (input.TryGetValue("prompt", out var p) && p.ValueKind == JsonValueKind.String)
            prompt = p.GetString() ?? "";

        if (prompt.Length > 80)
            prompt = prompt[..80];

        return $"{desc}: {prompt}";
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
