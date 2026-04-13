using System.Text.Json;

namespace AgentLoop.Agent;

/// <summary>Session todo-plan tool logic (Python <c>TodoManager</c>); state lives in <see cref="TodoPlanningState"/>.</summary>
public static class TodoSessionPlanner
{
    public const int MaxItems = 12;
    public const int PlanReminderInterval = 3;
    public const string ReminderText = "<reminder>Refresh your current plan before continuing.</reminder>";

    public static string Update(TodoPlanningState state, JsonElement itemsRoot)
    {
        if (itemsRoot.ValueKind != JsonValueKind.Array)
            return "Error: todo items must be a JSON array.";

        var elements = itemsRoot.EnumerateArray().ToList();
        if (elements.Count > MaxItems)
            return "Error: Keep the session plan short (max 12 items)";

        var normalized = new List<TodoPlanItem>(elements.Count);
        var inProgressCount = 0;

        for (var index = 0; index < elements.Count; index++)
        {
            var raw = elements[index];
            if (raw.ValueKind != JsonValueKind.Object)
                return $"Error: Item {index}: expected object";

            var content = GetStringProperty(raw, "content").Trim();
            if (content.Length == 0)
                return $"Error: Item {index}: content required";

            var status = GetStringProperty(raw, "status").ToLowerInvariant();
            if (status is not ("pending" or "in_progress" or "completed"))
                return $"Error: Item {index}: invalid status '{status}'";

            if (status == "in_progress")
                inProgressCount++;

            var activeForm = GetOptionalActiveForm(raw);

            normalized.Add(new TodoPlanItem(content, status, activeForm));
        }

        if (inProgressCount > 1)
            return "Error: Only one plan item can be in_progress";

        state.Items = normalized;
        return Render(state);
    }

    public static void NoteRoundWithoutTodoUpdate(TodoPlanningState state) => state.RoundsSinceUpdate++;

    /// <summary>Returns reminder text after several tool rounds without a <c>todo</c> update.</summary>
    public static string? TryGetReminder(TodoPlanningState state)
    {
        if (state.Items.Count == 0)
            return null;
        if (state.RoundsSinceUpdate < PlanReminderInterval)
            return null;
        return ReminderText;
    }

    public static string Render(TodoPlanningState state)
    {
        if (state.Items.Count == 0)
            return "No session plan yet.";

        var lines = new List<string>(state.Items.Count + 1);
        foreach (var item in state.Items)
        {
            var marker = item.Status switch
            {
                "pending" => "[ ]",
                "in_progress" => "[>]",
                "completed" => "[x]",
                _ => "[ ]",
            };
            var line = $"{marker} {item.Content}";
            if (item.Status == "in_progress" && item.ActiveForm.Length > 0)
                line += $" ({item.ActiveForm})";
            lines.Add(line);
        }

        var completed = state.Items.Count(i => i.Status == "completed");
        lines.Add($"\n({completed}/{state.Items.Count} completed)");
        return string.Join("\n", lines);
    }

    static string GetStringProperty(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return "";
        return el.GetString() ?? "";
    }

    static string GetOptionalActiveForm(JsonElement raw)
    {
        if (raw.TryGetProperty("activeForm", out var af) && af.ValueKind == JsonValueKind.String)
            return (af.GetString() ?? "").Trim();
        if (raw.TryGetProperty("active_form", out var af2) && af2.ValueKind == JsonValueKind.String)
            return (af2.GetString() ?? "").Trim();
        return "";
    }
}
