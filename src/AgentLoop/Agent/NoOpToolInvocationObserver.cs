namespace AgentLoop.Agent;

public sealed class NoOpToolInvocationObserver : IToolInvocationObserver
{
    public static readonly NoOpToolInvocationObserver Instance = new();

    NoOpToolInvocationObserver() { }

    public void OnToolInvocation(string command, string outputPreview) { }
}
