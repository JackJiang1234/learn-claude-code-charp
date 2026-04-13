using AgentLoop.Agent;
using AgentLoop.Bash;
using AgentLoop.Configuration;
using AgentLoop.Domain;
using Microsoft.Extensions.Configuration;

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

    var options = AnthropicConnectionOptions.FromConfiguration(configuration);
    using var client = AnthropicClientFactory.Create(options);

    var systemPrompt =
        $"""
        You are a coding agent at {cwd}.
        Use the todo tool for multi-step work.
        Keep exactly one step in_progress when a task has multiple steps.
        Refresh the plan as work advances. Prefer tools over prose.
        """;

    var bash = new BashRunner(cwd);
    var workspace = new WorkspaceFileOperations(cwd);
    var engine = new AgentLoopEngine(
        client,
        bash,
        workspace,
        options.ModelId,
        systemPrompt,
        new ConsoleToolInvocationObserver()
    );

    var session = new AgentInteractiveSession(engine);
    await session.RunAsync().ConfigureAwait(false);
}
