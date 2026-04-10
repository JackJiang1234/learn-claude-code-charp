using Anthropic;

namespace AgentLoop.Configuration;

public static class AnthropicClientFactory
{
    public static AnthropicClient Create(AnthropicConnectionOptions options)
    {
        if (options.BaseUrl is null && options.AuthToken is null)
            return new AnthropicClient();

        if (options.BaseUrl is null)
            return new AnthropicClient { AuthToken = options.AuthToken };

        return new AnthropicClient { BaseUrl = options.BaseUrl, AuthToken = options.AuthToken };
    }
}
