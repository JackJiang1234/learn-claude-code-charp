using System.Text.Json;
using Anthropic.Models.Messages;
using AgentLoop.Agent;

namespace AgentLoop.Tests;

public sealed class MessageNormalizerTests
{
    [Fact]
    public void Merge_consecutive_same_role_user_strings()
    {
        var messages = new List<MessageParam>
        {
            new() { Role = Role.User, Content = "a" },
            new() { Role = Role.User, Content = "b" },
        };

        var normalized = MessageNormalizer.NormalizeForApi(messages);
        Assert.Single(normalized);
        Assert.True(normalized[0].Role == Role.User);
        Assert.True(normalized[0].Content.TryPickContentBlockParams(out var blocks));
        Assert.Equal(2, blocks.Count);
        Assert.True(blocks[0].TryPickText(out var t0));
        Assert.True(blocks[1].TryPickText(out var t1));
        Assert.Equal("a", t0.Text);
        Assert.Equal("b", t1.Text);
    }

    [Fact]
    public void Inserts_placeholder_tool_result_for_orphan_tool_use()
    {
        var toolInput = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("echo hi"),
        };

        var messages = new List<MessageParam>
        {
            new()
            {
                Role = Role.Assistant,
                Content = new MessageParamContent(
                    new List<ContentBlockParam>
                    {
                        new ToolUseBlockParam { ID = "tu_1", Name = "bash", Input = toolInput },
                    }
                ),
            },
        };

        var normalized = MessageNormalizer.NormalizeForApi(messages);
        Assert.Equal(2, normalized.Count);
        Assert.True(normalized[0].Role == Role.Assistant);
        Assert.True(normalized[1].Role == Role.User);
        Assert.True(normalized[1].Content.TryPickContentBlockParams(out var userBlocks));
        Assert.Single(userBlocks);
        Assert.True(userBlocks[0].TryPickToolResult(out var tr));
        Assert.Equal("tu_1", tr!.ToolUseID);
        var toolResultContent = tr.Content!;
        Assert.True(toolResultContent.TryPickString(out var body));
        Assert.Equal("(cancelled)", body);
    }
}
