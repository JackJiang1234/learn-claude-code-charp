using System.Text.Json;
using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

// Matches Python s04: parent has todo + task; child has base file/bash tools only (no recursion).
internal static class AgentToolDefinitions
{
    /// <summary>Parent agent: base tools + todo + <c>task</c> (spawn subagent).</summary>
    public static IReadOnlyList<ToolUnion> CreateParentToolUnions() =>
        CreateParentTools().Select(static t => (ToolUnion)t).ToArray();

    /// <summary>Subagent: fresh context, same filesystem, no <c>task</c> or <c>todo</c>.</summary>
    public static IReadOnlyList<ToolUnion> CreateChildToolUnions() =>
        CreateChildTools().Select(static t => (ToolUnion)t).ToArray();

    internal static string[] ParentToolNames() => CreateParentTools().Select(static t => t.Name).ToArray();

    internal static string[] ChildToolNames() => CreateChildTools().Select(static t => t.Name).ToArray();

    static Tool[] CreateParentTools() =>
    [
        CreateBashTool(),
        CreateReadFileTool(),
        CreateWriteFileTool(),
        CreateEditFileTool(),
        CreateTodoTool(),
        CreateTaskTool(),
    ];

    static Tool[] CreateChildTools() =>
        [CreateBashTool(), CreateReadFileTool(), CreateWriteFileTool(), CreateEditFileTool()];

    static Tool CreateBashTool() =>
        new()
        {
            Name = "bash",
            Description = "Run a shell command.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new { command = new { type = "string" } },
                    required = new[] { "command" },
                }
            ),
        };

    static Tool CreateReadFileTool() =>
        new()
        {
            Name = "read_file",
            Description = "Read file contents.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        limit = new { type = "integer" },
                    },
                    required = new[] { "path" },
                }
            ),
        };

    static Tool CreateWriteFileTool() =>
        new()
        {
            Name = "write_file",
            Description = "Write content to file.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        content = new { type = "string" },
                    },
                    required = new[] { "path", "content" },
                }
            ),
        };

    static Tool CreateEditFileTool() =>
        new()
        {
            Name = "edit_file",
            Description = "Replace exact text in file.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        old_text = new { type = "string" },
                        new_text = new { type = "string" },
                    },
                    required = new[] { "path", "old_text", "new_text" },
                }
            ),
        };

    static Tool CreateTodoTool() =>
        new()
        {
            Name = "todo",
            Description = "Rewrite the current session plan for multi-step work.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new
                    {
                        items = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    content = new { type = "string" },
                                    status = new
                                    {
                                        type = "string",
                                        @enum = new[] { "pending", "in_progress", "completed" },
                                    },
                                    activeForm = new
                                    {
                                        type = "string",
                                        description = "Optional present-continuous label.",
                                    },
                                },
                                required = new[] { "content", "status" },
                            },
                        },
                    },
                    required = new[] { "items" },
                }
            ),
        };

    static Tool CreateTaskTool() =>
        new()
        {
            Name = "task",
            Description =
                "Spawn a subagent with fresh context. It shares the filesystem but not conversation history.",
            InputSchema = ToInputSchema(
                new
                {
                    type = "object",
                    properties = new
                    {
                        prompt = new { type = "string" },
                        description = new
                        {
                            type = "string",
                            description = "Short description of the task.",
                        },
                    },
                    required = new[] { "prompt" },
                }
            ),
        };

    static InputSchema ToInputSchema(object anonymous)
    {
        var root = JsonSerializer.SerializeToElement(anonymous);
        var dict = new Dictionary<string, JsonElement>();
        foreach (var p in root.EnumerateObject())
            dict[p.Name] = p.Value;
        return InputSchema.FromRawUnchecked(dict);
    }
}
