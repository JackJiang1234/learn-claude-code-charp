using AgentLoop.Agent;

namespace AgentLoop.Tests;

public sealed class WorkspaceFileOperationsTests
{
    public static TheoryData<string> EscapePaths =>
        new()
        {
            "..\\secret.txt",
            "..\\..\\outside.txt",
        };

    [Theory]
    [MemberData(nameof(EscapePaths))]
    public void ReadFile_rejects_paths_outside_workspace(string relative)
    {
        using var dir = new TempWorkspace();
        var ws = new WorkspaceFileOperations(dir.Path);
        var r = ws.ReadFile(relative, null);
        Assert.StartsWith("Error:", r, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_read_edit_roundtrip_and_first_occurrence_only()
    {
        using var dir = new TempWorkspace();
        var ws = new WorkspaceFileOperations(dir.Path);

        Assert.Contains("Wrote", ws.WriteFile("sub/foo.txt", "hello"), StringComparison.Ordinal);
        Assert.Equal("hello", ws.ReadFile("sub/foo.txt", null));

        Assert.Contains("Edited", ws.EditFile("sub/foo.txt", "hello", "world"), StringComparison.Ordinal);
        Assert.Equal("world", ws.ReadFile("sub/foo.txt", null));

        Assert.Contains("Wrote", ws.WriteFile("dup.txt", "aa bb aa"), StringComparison.Ordinal);
        Assert.Contains("Edited", ws.EditFile("dup.txt", "aa", "xx"), StringComparison.Ordinal);
        Assert.Equal("xx bb aa", ws.ReadFile("dup.txt", null));
    }

    [Fact]
    public void ReadFile_limit_truncates_lines()
    {
        using var dir = new TempWorkspace();
        var ws = new WorkspaceFileOperations(dir.Path);
        File.WriteAllText(Path.Combine(dir.Path, "many.txt"), "a\nb\nc\nd\n");

        var r = ws.ReadFile("many.txt", 2);
        Assert.Contains("a", r, StringComparison.Ordinal);
        Assert.Contains("b", r, StringComparison.Ordinal);
        Assert.Contains("more lines", r, StringComparison.Ordinal);
        Assert.DoesNotContain("d", r, StringComparison.Ordinal);
    }

    sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
