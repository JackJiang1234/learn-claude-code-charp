using AgentLoop.Agent;
using AgentLoop.Domain;

namespace AgentLoop.Tests;

public sealed class AgentTemplateTests
{
    public sealed record ParseCase(string FileContent, string? ExpectedNameOverride, string ExpectedPrompt);

    public static TheoryData<ParseCase> ParseCases =>
        new()
        {
            new ParseCase("Plain body only.\nLine two.", null, "Plain body only.\nLine two."),
            new ParseCase(
                "---\nname: my-agent\n---\nSystem instructions here.\nSecond line.\n",
                "my-agent",
                "System instructions here.\nSecond line."
            ),
        };

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void Parses_markdown_like_python_template(ParseCase c)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agent-template-test-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, c.FileContent);

        try
        {
            var t = new AgentTemplate(path);
            var expectedName = c.ExpectedNameOverride ?? Path.GetFileNameWithoutExtension(path);
            Assert.Equal(expectedName, t.Name);
            Assert.Equal(c.ExpectedPrompt, t.SystemPrompt.Trim());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_path_throws_configuration_exception()
    {
        Assert.Throws<AgentConfigurationException>(() => new AgentTemplate(" "));
    }
}
