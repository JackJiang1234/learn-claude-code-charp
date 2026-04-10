using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

public static class MessageContentExtensions
{
    /// <summary>从用户或助手消息的 <see cref="MessageParamContent"/> 中提取纯文本（对齐 Python <c>extract_text</c>）。</summary>
    public static string ExtractText(MessageParamContent content)
    {
        if (content.TryPickString(out var single))
            return single.Trim();

        if (!content.TryPickContentBlockParams(out var blocks))
            return "";

        var texts = new List<string>();
        foreach (var block in blocks)
        {
            if (block.TryPickText(out var tb))
                texts.Add(tb.Text);
        }

        return string.Join("\n", texts).Trim();
    }
}
