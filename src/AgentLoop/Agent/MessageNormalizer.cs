using Anthropic.Models.Messages;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

/// <summary>在每次调用 Messages API 前规范化消息列表，对齐 Python <c>normalize_messages</c>。</summary>
public static class MessageNormalizer
{
    public static List<MessageParam> NormalizeForApi(IReadOnlyList<MessageParam> messages)
    {
        var cleaned = new List<MessageParam>(messages.Count);
        foreach (var msg in messages)
            cleaned.Add(CloneMessage(msg));

        var existingResults = CollectToolResultIds(cleaned);
        AppendPlaceholderToolResults(cleaned, existingResults);

        return MergeConsecutiveSameRole(cleaned);
    }

    static HashSet<string> CollectToolResultIds(List<MessageParam> messages)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role != Role.User)
                continue;

            if (!msg.Content.TryPickContentBlockParams(out var blocks))
                continue;

            foreach (var block in blocks)
            {
                if (block.TryPickToolResult(out var tr))
                    ids.Add(tr.ToolUseID);
            }
        }

        return ids;
    }

    static void AppendPlaceholderToolResults(List<MessageParam> cleaned, HashSet<string> existingResults)
    {
        var originalCount = cleaned.Count;
        for (var i = 0; i < originalCount; i++)
        {
            var msg = cleaned[i];
            if (msg.Role != Role.Assistant)
                continue;

            if (!msg.Content.TryPickContentBlockParams(out var blocks))
                continue;

            foreach (var block in blocks)
            {
                if (!block.TryPickToolUse(out var tu))
                    continue;

                if (existingResults.Contains(tu.ID))
                    continue;

                cleaned.Add(
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = new MessageParamContent(
                            new List<ContentBlockParam>
                            {
                                new ToolResultBlockParam(tu.ID)
                                {
                                    Content = new ToolResultBlockParamContent("(cancelled)"),
                                },
                            }
                        ),
                    }
                );
                existingResults.Add(tu.ID);
            }
        }
    }

    static List<MessageParam> MergeConsecutiveSameRole(List<MessageParam> cleaned)
    {
        if (cleaned.Count == 0)
            return cleaned;

        var merged = new List<MessageParam> { cleaned[0] };
        foreach (var msg in cleaned.Skip(1))
        {
            var last = merged[^1];
            if (msg.Role != last.Role)
            {
                merged.Add(msg);
                continue;
            }

            var prevBlocks = ToBlockList(last.Content);
            var currBlocks = ToBlockList(msg.Content);
            merged[^1] = new MessageParam
            {
                Role = last.Role,
                Content = new MessageParamContent(prevBlocks.Concat(currBlocks).ToList()),
            };
        }

        return merged;
    }

    static List<ContentBlockParam> ToBlockList(MessageParamContent content)
    {
        if (content.TryPickString(out var single))
            return [new TextBlockParam(single)];

        if (content.TryPickContentBlockParams(out var blocks))
            return blocks.Select(CloneBlock).ToList();

        return [new TextBlockParam("")];
    }

    static MessageParam CloneMessage(MessageParam msg) =>
        new() { Role = msg.Role, Content = CloneContent(msg.Content) };

    static MessageParamContent CloneContent(MessageParamContent content)
    {
        if (content.TryPickString(out var single))
            return single;

        if (content.TryPickContentBlockParams(out var blocks))
            return new MessageParamContent(blocks.Select(CloneBlock).ToList());

        return new MessageParamContent("");
    }

    static ContentBlockParam CloneBlock(ContentBlockParam block)
    {
        if (block.TryPickText(out var text))
            return new TextBlockParam(text.Text);

        if (block.TryPickToolUse(out var tu))
            return new ToolUseBlockParam { ID = tu.ID, Name = tu.Name, Input = tu.Input };

        if (block.TryPickToolResult(out var tr))
            return new ToolResultBlockParam(tr.ToolUseID) { Content = tr.Content };

        if (block.TryPickThinking(out var thinking))
        {
            return new ThinkingBlockParam
            {
                Signature = thinking.Signature,
                Thinking = thinking.Thinking,
            };
        }

        if (block.TryPickRedactedThinking(out var red))
            return new RedactedThinkingBlockParam { Data = red.Data };

        throw new AgentLoopException(
            "消息规范化遇到不支持的内容块类型，请扩展 MessageNormalizer.CloneBlock。"
        );
    }
}
