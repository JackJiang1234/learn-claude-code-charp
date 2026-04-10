namespace AgentLoop.Agent;

public sealed class ConsoleToolInvocationObserver : IToolInvocationObserver
{
    public void OnToolInvocation(string command, string outputPreview)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("$ " + command);
        Console.ResetColor();
        Console.WriteLine(outputPreview);
    }
}
