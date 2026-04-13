using System.Text.RegularExpressions;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

/// <summary>
/// Parses agent definition from markdown with YAML-like frontmatter (Python <c>AgentTemplate</c> / Claude Code <c>.claude/agents/*.md</c>).
/// </summary>
public sealed class AgentTemplate
{
    static readonly Regex FrontmatterRegex = new(
        @"^---\s*\r?\n(.*?)\r?\n---\s*\r?\n(.*)",
        RegexOptions.Singleline | RegexOptions.Compiled
    );

    readonly Dictionary<string, string> _config = new(StringComparer.OrdinalIgnoreCase);

    public AgentTemplate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new AgentConfigurationException("Agent template path is required.");

        FilePath = filePath;
        Name = System.IO.Path.GetFileNameWithoutExtension(filePath);
        SystemPrompt = "";
        Parse();
    }

    public string FilePath { get; }

    public string Name { get; private set; }

    public IReadOnlyDictionary<string, string> Config => _config;

    public string SystemPrompt { get; private set; }

    void Parse()
    {
        string text;
        try
        {
            text = File.ReadAllText(FilePath);
        }
        catch (Exception ex)
        {
            throw new AgentConfigurationException($"Failed to read agent template: {FilePath}", ex);
        }

        var match = FrontmatterRegex.Match(text);
        if (!match.Success)
        {
            SystemPrompt = text.Trim();
            return;
        }

        foreach (var line in match.Groups[1].Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0)
                continue;

            var k = line[..idx].Trim();
            var v = line[(idx + 1)..].Trim();
            _config[k] = v;
        }

        SystemPrompt = match.Groups[2].Value.Trim();
        if (_config.TryGetValue("name", out var n) && n.Length > 0)
            Name = n;
    }
}
