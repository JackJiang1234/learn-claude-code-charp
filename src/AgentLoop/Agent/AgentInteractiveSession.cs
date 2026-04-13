using Anthropic.Models.Messages;
using AgentLoop.Domain;

namespace AgentLoop.Agent;

/// <summary>Console REPL: reads user input and drives <see cref="AgentLoopEngine"/>.</summary>
public sealed class AgentInteractiveSession
{
    private readonly AgentLoopEngine _engine;
    private readonly TodoPlanningState _sessionTodoPlanning = new();

    public AgentInteractiveSession(AgentLoopEngine engine)
    {
        _engine = engine;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var history = new List<MessageParam>();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("agent >> ");
            Console.ResetColor();

            var query = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (query is null)
                break;

            var trimmed = query.Trim();
            if (trimmed.Length == 0
                || string.Equals(trimmed, "q", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase))
                break;

            history.Add(new MessageParam { Role = Role.User, Content = trimmed });

            var state = new LoopState(history, _sessionTodoPlanning);
            try
            {
                await _engine.RunAgentLoopAsync(state, cancellationToken).ConfigureAwait(false);
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
}
