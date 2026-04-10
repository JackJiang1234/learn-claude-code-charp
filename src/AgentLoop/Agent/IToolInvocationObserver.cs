namespace AgentLoop.Agent;

/// <summary>在本地执行 bash 工具前后接收通知（用于日志或 UI，与具体控制台解耦）。</summary>
public interface IToolInvocationObserver
{
    void OnToolInvocation(string command, string outputPreview);
}
