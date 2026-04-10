namespace AgentLoop.Agent;

/// <summary>工作区内读/写/编辑文件，与 Python <c>run_read</c>/<c>run_write</c>/<c>run_edit</c> 对齐。</summary>
public interface IWorkspaceFileOperations
{
    string ReadFile(string path, int? limitLines);
    string WriteFile(string path, string content);
    string EditFile(string path, string oldText, string newText);
}
