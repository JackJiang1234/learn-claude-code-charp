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

        var (results, usedTodo) = ExecuteToolCalls(state, response.Content);
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
                var withReminder = new List<ContentBlockParam>(results.Length + 1)
                {
                    new TextBlockParam(reminder),
                };
                withReminder.AddRange(results);
                results = withReminder.ToArray();
            }
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

    (ContentBlockParam[] Results, bool UsedTodo) ExecuteToolCalls(
        LoopState state,
        IReadOnlyList<ContentBlock> responseContent
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
                output = ExecuteSingleTool(state, tool);
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.Message}";
            }

            var preview = output.Length <= ToolOutputPreviewLength ? output : output[..ToolOutputPreviewLength];
            _toolObserver.OnToolInvocation(tool.Name, DescribeToolInvocation(tool), preview);

            var tr = new ToolResultBlockParam(tool.ID) { Content = new ToolResultBlockParamContent(output) };
            list.Add(tr);
        }

        return (list.ToArray(), usedTodo);
    }

    string ExecuteSingleTool(LoopState state, ToolUseBlock tool)
    {
        var input = tool.Input;
        return tool.Name switch
        {
            "bash" => RunBashTool(input),
            "read_file" => RunReadFileTool(input),
            "write_file" => RunWriteFileTool(input),
            "edit_file" => RunEditFileTool(input),
            "todo" => RunTodoTool(state.TodoPlanning, input),
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
