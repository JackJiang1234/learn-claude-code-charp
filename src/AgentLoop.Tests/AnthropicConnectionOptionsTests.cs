using AgentLoop.Configuration;
using AgentLoop.Domain;
using Microsoft.Extensions.Configuration;

namespace AgentLoop.Tests;

public sealed class AnthropicConnectionOptionsTests
{
    public static TheoryData<Dictionary<string, string?>, string, string?, string?> ValidCases =>
        new()
        {
            {
                new Dictionary<string, string?> { ["Anthropic:ModelId"] = "  claude-test  " },
                "claude-test",
                null,
                null
            },
            {
                new Dictionary<string, string?>
                {
                    ["Anthropic:ModelId"] = "m1",
                    ["Anthropic:BaseUrl"] = "https://example.com/api/ ",
                    ["Anthropic:AuthToken"] = " tok ",
                },
                "m1",
                "https://example.com/api",
                "tok"
            },
        };

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void FromConfiguration_parses_expected_options(
        Dictionary<string, string?> keys,
        string expectedModel,
        string? expectedBase,
        string? expectedToken
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(keys)
            .Build();

        var options = AnthropicConnectionOptions.FromConfiguration(configuration);

        Assert.Equal(expectedModel, options.ModelId);
        Assert.Equal(expectedBase, options.BaseUrl);
        Assert.Equal(expectedToken, options.AuthToken);
    }

    [Fact]
    public void FromConfiguration_throws_when_model_id_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Anthropic:BaseUrl"] = "https://x" })
            .Build();

        var ex = Assert.Throws<AgentConfigurationException>(() =>
            AnthropicConnectionOptions.FromConfiguration(configuration)
        );

        Assert.Contains("ModelId", ex.Message, StringComparison.Ordinal);
    }
}
