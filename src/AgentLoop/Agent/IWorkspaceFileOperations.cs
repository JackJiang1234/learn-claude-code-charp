namespace AgentLoop.Agent;

/// <summary>Read/write/edit under the workspace (Python <c>run_read</c>/<c>run_write</c>/<c>run_edit</c>).</summary>
public interface IWorkspaceFileOperations
{
    string ReadFile(string path, int? limitLines);
    string WriteFile(string path, string content);
    string EditFile(string path, string oldText, string newText);
}
