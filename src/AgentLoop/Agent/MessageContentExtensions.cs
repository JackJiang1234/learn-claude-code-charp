using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

public static class MessageContentExtensions
{
    /// <summary>Extracts plain text from user or assistant <see cref="MessageParamContent"/> (Python <c>extract_text</c>).</summary>
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
