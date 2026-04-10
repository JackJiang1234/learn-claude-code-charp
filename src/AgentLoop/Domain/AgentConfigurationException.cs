namespace AgentLoop.Domain;

/// <summary>环境变量或启动配置无效时抛出。</summary>
public sealed class AgentConfigurationException : AgentLoopException
{
    public AgentConfigurationException(string message)
        : base(message) { }
}
