namespace AgentLoop.Agent;

/// <summary>在本地执行工具后接收通知（用于日志或 UI，与具体控制台解耦）。</summary>
public interface IToolInvocationObserver
{
    /// <param name="toolName">工具名，如 <c>bash</c>、<c>read_file</c>。</param>
    /// <param name="detail">便于阅读的摘要（如 shell 命令或文件路径）。</param>
    /// <param name="outputPreview">输出截断预览。</param>
    void OnToolInvocation(string toolName, string detail, string outputPreview);
}
