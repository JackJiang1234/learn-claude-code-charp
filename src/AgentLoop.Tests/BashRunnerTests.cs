using Anthropic.Models.Messages;
using AgentLoop.Agent;
using AgentLoop.Bash;

namespace AgentLoop.Tests;

public sealed class BashRunnerTests
{
    public static TheoryData<string, Func<string, bool>> DangerousCommandCases =>
        new()
        {
            { "rm -rf /", static o => o.Contains("Dangerous", StringComparison.Ordinal) },
            { "sudo ls", static o => o.Contains("Dangerous", StringComparison.Ordinal) },
            { "shutdown now", static o => o.Contains("Dangerous", StringComparison.Ordinal) },
            { "echo x > /dev/null", static o => o.Contains("Dangerous", StringComparison.Ordinal) },
        };

    [Theory]
    [MemberData(nameof(DangerousCommandCases))]
    public void Run_blocks_dangerous_commands(string command, Func<string, bool> expect)
    {
        var runner = new BashRunner(Environment.CurrentDirectory);
        var result = runner.Run(command);
        Assert.True(expect(result), result);
    }

    [Fact]
    public void Run_echo_returns_trimmed_output()
    {
        var runner = new BashRunner(Environment.CurrentDirectory);
        var result = runner.Run("echo hi");
        Assert.Equal("hi", result);
    }

    [Fact]
    public void Run_silent_command_produces_placeholder()
    {
        var runner = new BashRunner(Environment.CurrentDirectory);
        var result = OperatingSystem.IsWindows() ? runner.Run("exit 0") : runner.Run("true");
        Assert.Equal("(no output)", result);
    }
}

public sealed class MessageContentExtensionsTests
{
    [Fact]
    public void ExtractText_from_string_content()
    {
        var mp = new MessageParam
        {
            Role = Role.User,
            Content = "  hello  ",
        };
        Assert.Equal("hello", MessageContentExtensions.ExtractText(mp.Content));
    }

    [Fact]
    public void ExtractText_joins_text_blocks()
    {
        var mp = new MessageParam
        {
            Role = Role.Assistant,
            Content = new MessageParamContent(
                new List<ContentBlockParam> { new TextBlockParam("a"), new TextBlockParam("b") }
            ),
        };
        Assert.Equal("a\nb", MessageContentExtensions.ExtractText(mp.Content));
    }
}
