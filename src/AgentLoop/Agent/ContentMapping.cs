using System.Collections.Immutable;
using Anthropic.Models.Messages;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

internal static class ContentMapping
{
    public static ImmutableArray<ContentBlockParam> MapAssistantBlocksToParams(
        IReadOnlyList<ContentBlock> blocks
    )
    {
        var list = ImmutableArray.CreateBuilder<ContentBlockParam>(blocks.Count);
        foreach (var block in blocks)
        {
            if (block.TryPickText(out var text))
            {
                list.Add(new TextBlockParam(text.Text));
                continue;
            }

            if (block.TryPickToolUse(out var tool))
            {
                list.Add(ToToolUseParam(tool));
                continue;
            }

            if (block.TryPickThinking(out var thinking))
            {
                list.Add(
                    new ThinkingBlockParam
                    {
                        Signature = thinking.Signature,
                        Thinking = thinking.Thinking,
                    }
                );
                continue;
            }

            throw new AgentLoopException(
                "Unsupported assistant content block type; extend ContentMapping.MapAssistantBlocksToParams."
            );
        }

        return list.MoveToImmutable();
    }

    static ToolUseBlockParam ToToolUseParam(ToolUseBlock tool) =>
        new() { ID = tool.ID, Name = tool.Name, Input = tool.Input };
}
