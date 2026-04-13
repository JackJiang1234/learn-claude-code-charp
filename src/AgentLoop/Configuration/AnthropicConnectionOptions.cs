using AgentLoop.Domain;
using Microsoft.Extensions.Configuration;

namespace AgentLoop.Configuration;

/// <summary>Anthropic API connection options from configuration and environment variables.</summary>
public sealed record AnthropicConnectionOptions(string ModelId, string? BaseUrl, string? AuthToken)
{
    public static AnthropicConnectionOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Anthropic");
        var modelId = FirstNonEmpty(section["ModelId"], Environment.GetEnvironmentVariable("MODEL_ID"));
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new AgentConfigurationException(
                "Missing Anthropic:ModelId. Set it in appsettings.json under the Anthropic section, "
                    + "or override via environment variables Anthropic__ModelId or MODEL_ID."
            );
        }

        var baseUrl = FirstNonEmpty(
            section["BaseUrl"],
            Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL")
        );
        var authToken = FirstNonEmpty(
            section["AuthToken"],
            Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN")
        );

        return new AnthropicConnectionOptions(
            modelId.Trim(),
            string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/'),
            string.IsNullOrWhiteSpace(authToken) ? null : authToken
        );
    }

    static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }
}
