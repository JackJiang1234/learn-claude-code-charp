using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Configuration;
using AgentLoop.Agent;
using AgentLoop.Bash;
using AgentLoop.Domain;

try
{
    await RunAsync().ConfigureAwait(false);
}
catch (AgentConfigurationException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task RunAsync()
{
    var cwd = Directory.GetCurrentDirectory();
    var basePath = AppContext.BaseDirectory;

    var environmentName =
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    var configuration = new ConfigurationBuilder()
        .SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
        .AddUserSecrets<Program>()
        .AddEnvironmentVariables()
        .Build();

    var anthropicSection = configuration.GetSection("Anthropic");
    var modelId = FirstNonEmpty(
        anthropicSection["ModelId"],
        Environment.GetEnvironmentVariable("MODEL_ID")
    );
    if (string.IsNullOrWhiteSpace(modelId))
    {
        throw new AgentConfigurationException(
            "缺少 Anthropic:ModelId（请在 appsettings.json 的 Anthropic 节中设置，"
                + "或使用环境变量 Anthropic__ModelId / MODEL_ID 覆盖）。"
        );
    }

    var baseUrl = FirstNonEmpty(
        anthropicSection["BaseUrl"],
        Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL")
    );
    var authToken = FirstNonEmpty(
        anthropicSection["AuthToken"],
        Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN")
    );

    using var client = CreateAnthropicClient(baseUrl, authToken);

    var systemPrompt =
        $"You are a coding agent at {cwd}. "
        + "Use Windows cmd to inspect and change the workspace. Act first, then report clearly.";

    var bash = new BashRunner(cwd);
    var engine = new AgentLoopEngine(client, bash, modelId, systemPrompt);

    var history = new List<MessageParam>();

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("agent >> ");
        Console.ResetColor();

        var query = Console.ReadLine();
        if (query is null)
            break;

        var trimmed = query.Trim();
        if (trimmed.Length == 0
            || string.Equals(trimmed, "q", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase))
            break;

        history.Add(new MessageParam { Role = Role.User, Content = trimmed });

        var state = new LoopState(history);
        try
        {
            await engine.RunAgentLoopAsync(state).ConfigureAwait(false);
        }
        catch (AgentLoopException ex)
        {
            Console.Error.WriteLine(ex.Message);
            if (ex.InnerException is not null)
                Console.Error.WriteLine(ex.InnerException);
            history.RemoveAt(history.Count - 1);
            continue;
        }

        if (history.Count == 0)
            continue;

        var last = history[^1];
        var finalText = MessageContentExtensions.ExtractText(last.Content);
        if (finalText.Length > 0)
            Console.WriteLine(finalText);
        Console.WriteLine();
    }
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

static AnthropicClient CreateAnthropicClient(string? baseUrl, string? authToken)
{
    var trimmedBase = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
    var trimmedToken = string.IsNullOrWhiteSpace(authToken) ? null : authToken;

    if (trimmedBase is null && trimmedToken is null)
        return new AnthropicClient();

    if (trimmedBase is null)
        return new AnthropicClient { AuthToken = trimmedToken };

    return new AnthropicClient { BaseUrl = trimmedBase, AuthToken = trimmedToken };
}
