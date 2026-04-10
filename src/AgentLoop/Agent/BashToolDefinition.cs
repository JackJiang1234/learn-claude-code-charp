using System.Text.Json;
using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

internal static class BashToolDefinition
{
    public static Tool CreateTool() =>
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
}
