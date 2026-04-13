namespace AgentLoop.Agent;

/// <summary>Resolves paths under the workspace root (Python <c>safe_path</c> and file tools).</summary>
public sealed class WorkspaceFileOperations : IWorkspaceFileOperations
{
    public const int MaxReadChars = 50_000;

    private readonly string _workspaceRoot;

    public WorkspaceFileOperations(string workspaceRoot)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    public string ReadFile(string path, int? limitLines)
    {
        if (!TryResolveSafePath(path, out var full, out var pathError))
            return pathError!;

        try
        {
            var text = File.ReadAllText(full);
            var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
            if (limitLines is { } lim && lim < lines.Length)
            {
                var head = lines.AsSpan(0, lim).ToArray();
                var rest = lines.Length - lim;
                var combined = string.Join("\n", head) + $"\n... ({rest} more lines)";
                return combined.Length <= MaxReadChars ? combined : combined[..MaxReadChars];
            }

            return text.Length <= MaxReadChars ? text : text[..MaxReadChars];
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public string WriteFile(string path, string content)
    {
        if (!TryResolveSafePath(path, out var full, out var pathError))
            return pathError!;

        try
        {
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(full, content);
            return $"Wrote {content.Length} bytes to {path}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public string EditFile(string path, string oldText, string newText)
    {
        if (!TryResolveSafePath(path, out var full, out var pathError))
            return pathError!;

        try
        {
            var content = File.ReadAllText(full);
            var idx = content.IndexOf(oldText, StringComparison.Ordinal);
            if (idx < 0)
                return $"Error: Text not found in {path}";

            var updated = content[..idx] + newText + content[(idx + oldText.Length)..];
            File.WriteAllText(full, updated);
            return $"Edited {path}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    bool TryResolveSafePath(string relativePath, out string fullPath, out string? error)
    {
        fullPath = "";
        error = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "Error: Path is empty.";
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
        if (!IsStrictSubPath(_workspaceRoot, combined))
        {
            error = $"Error: Path escapes workspace: {relativePath}";
            return false;
        }

        fullPath = combined;
        return true;
    }

    static bool IsStrictSubPath(string root, string fullPath)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var p = Path.GetFullPath(fullPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(r, p, comparison))
            return true;

        var prefix = r + Path.DirectorySeparatorChar;
        return p.StartsWith(prefix, comparison);
    }
}
