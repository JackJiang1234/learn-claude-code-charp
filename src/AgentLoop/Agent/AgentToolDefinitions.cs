using System.Text.Json;
using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

// 与 Python s02 对齐：read_file 可并行；write_file / edit_file 需串行（当前实现为顺序执行）。
internal static class AgentToolDefinitions
{
    /// <summary>注册到 Messages API 的工具列表（SDK 要求 <see cref="ToolUnion"/>）。</summary>
    public static IReadOnlyList<ToolUnion> CreateToolUnions() =>
        CreateTools().Select(static t => (ToolUnion)t).ToArray();

    static Tool[] CreateTools() =>
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

    static InputSchema ToInputSchema(object anonymous)
    {
        var root = JsonSerializer.SerializeToElement(anonymous);
        var dict = new Dictionary<string, JsonElement>();
        foreach (var p in root.EnumerateObject())
            dict[p.Name] = p.Value;
        return InputSchema.FromRawUnchecked(dict);
    }
}
