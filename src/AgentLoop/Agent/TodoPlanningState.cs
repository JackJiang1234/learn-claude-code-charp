namespace AgentLoop.Agent;

/// <summary>Python <c>PlanningState</c>: current session plan and rounds since last todo refresh.</summary>
public sealed class TodoPlanningState
{
    public List<TodoPlanItem> Items { get; set; } = [];
    public int RoundsSinceUpdate { get; set; }
}
