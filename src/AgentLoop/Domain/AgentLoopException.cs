namespace AgentLoop.Domain;

/// <summary>代理循环相关错误的基类。</summary>
public class AgentLoopException : Exception
{
    public AgentLoopException(string message)
        : base(message) { }

    public AgentLoopException(string message, Exception innerException)
        : base(message, innerException) { }
}
