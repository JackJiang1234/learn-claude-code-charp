namespace AgentLoop.Domain;

/// <summary>Base type for agent loop errors.</summary>
public class AgentLoopException : Exception
{
    public AgentLoopException(string message)
        : base(message) { }

    public AgentLoopException(string message, Exception innerException)
        : base(message, innerException) { }
}
