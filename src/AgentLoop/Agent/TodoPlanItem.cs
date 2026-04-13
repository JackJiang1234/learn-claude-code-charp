namespace AgentLoop.Agent;

/// <summary>One item in the session todo plan (Python <c>PlanItem</c>).</summary>
public sealed record TodoPlanItem(string Content, string Status, string ActiveForm);
