using AgentLoop.Agent;

namespace AgentLoop.Tests;

public sealed class AgentToolDefinitionsTests
{
    public static TheoryData<string, bool> ParentToolPresence =>
        new()
        {
            { "bash", true },
            { "read_file", true },
            { "write_file", true },
            { "edit_file", true },
            { "todo", true },
            { "task", true },
        };

    public static TheoryData<string, bool> ChildToolPresence =>
        new()
        {
            { "bash", true },
            { "read_file", true },
            { "write_file", true },
            { "edit_file", true },
            { "todo", false },
            { "task", false },
        };

    [Theory]
    [MemberData(nameof(ParentToolPresence))]
    public void Parent_tool_unions_include_expected_tools(string name, bool expected)
    {
        var names = new HashSet<string>(AgentToolDefinitions.ParentToolNames(), StringComparer.Ordinal);
        Assert.Equal(expected, names.Contains(name));
    }

    [Theory]
    [MemberData(nameof(ChildToolPresence))]
    public void Child_tool_unions_include_expected_tools(string name, bool expected)
    {
        var names = new HashSet<string>(AgentToolDefinitions.ChildToolNames(), StringComparer.Ordinal);
        Assert.Equal(expected, names.Contains(name));
    }
}
