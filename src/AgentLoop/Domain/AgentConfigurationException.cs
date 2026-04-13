namespace AgentLoop.Domain;

/// <summary>Thrown when environment or startup configuration is invalid.</summary>
public sealed class AgentConfigurationException : AgentLoopException
{
    public AgentConfigurationException(string message)
        : base(message) { }

    public AgentConfigurationException(string message, Exception innerException)
        : base(message, innerException) { }
}
