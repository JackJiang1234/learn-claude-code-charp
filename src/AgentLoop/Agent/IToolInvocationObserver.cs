namespace AgentLoop.Agent;

/// <summary>Notified after a tool runs locally (logging/UI; decoupled from the console).</summary>
public interface IToolInvocationObserver
{
    /// <param name="toolName">Tool name, e.g. <c>bash</c>, <c>read_file</c>.</param>
    /// <param name="detail">Human-readable summary (e.g. shell command or path).</param>
    /// <param name="outputPreview">Truncated output preview.</param>
    void OnToolInvocation(string toolName, string detail, string outputPreview);
}
