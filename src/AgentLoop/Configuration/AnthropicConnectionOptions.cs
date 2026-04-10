using AgentLoop.Domain;
using Microsoft.Extensions.Configuration;

namespace AgentLoop.Configuration;

/// <summary>从配置与环境变量解析出的 Anthropic API 连接参数。</summary>
public sealed record AnthropicConnectionOptions(string ModelId, string? BaseUrl, string? AuthToken)
{
    public static AnthropicConnectionOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Anthropic");
        var modelId = FirstNonEmpty(section["ModelId"], Environment.GetEnvironmentVariable("MODEL_ID"));
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new AgentConfigurationException(
                "缺少 Anthropic:ModelId（请在 appsettings.json 的 Anthropic 节中设置，"
                    + "或使用环境变量 Anthropic__ModelId / MODEL_ID 覆盖）。"
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
