namespace AgentLoop.Agent;

public sealed class ConsoleToolInvocationObserver : IToolInvocationObserver
{
    public void OnToolInvocation(string toolName, string detail, string outputPreview)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (toolName == "bash")
            Console.WriteLine("$ " + detail);
        else
            Console.WriteLine($"> {toolName}: {detail}");
        Console.ResetColor();
        Console.WriteLine(outputPreview);
    }
}
