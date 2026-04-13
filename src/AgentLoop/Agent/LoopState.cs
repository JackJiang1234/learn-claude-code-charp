using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

/// <summary>Python <c>LoopState</c> equivalent: message history, turn count, and transition reason.</summary>
public sealed class LoopState
{
    public LoopState(List<MessageParam> messages, TodoPlanningState? todoPlanning = null)
    {
        Messages = messages;
        TodoPlanning = todoPlanning ?? new TodoPlanningState();
    }

    public List<MessageParam> Messages { get; }

    /// <summary>Lightweight session plan state (Python <c>PlanningState</c>).</summary>
    public TodoPlanningState TodoPlanning { get; }
    public int TurnCount { get; set; } = 1;
    public string? TransitionReason { get; set; }
}
