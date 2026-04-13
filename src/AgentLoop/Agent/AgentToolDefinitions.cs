using System.Text.Json;
using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

// Matches Python s02: read_file may be parallel; write_file/edit_file are sequential (executed in order here).
internal static class AgentToolDefinitions
{
    /// <summary>Tool list for the Messages API (SDK expects <see cref="ToolUnion"/>).</summary>
    public static IReadOnlyList<ToolUnion> CreateToolUnions() =>
        CreateTools().Select(static t => (ToolUnion)t).ToArray();

    static Tool[] CreateTools() =>
        [CreateBashTool(), CreateReadFileTool(), CreateWriteFileTool(), CreateEditFileTool(), CreateTodoTool()];

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

    static InputSchema ToInputSchema(object anonymous)
    {
        var root = JsonSerializer.SerializeToElement(anonymous);
        var dict = new Dictionary<string, JsonElement>();
        foreach (var p in root.EnumerateObject())
            dict[p.Name] = p.Value;
        return InputSchema.FromRawUnchecked(dict);
    }
}
