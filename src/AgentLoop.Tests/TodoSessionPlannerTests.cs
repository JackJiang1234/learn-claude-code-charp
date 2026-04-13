using System.Text.Json;
using AgentLoop.Agent;

namespace AgentLoop.Tests;

public sealed class TodoSessionPlannerTests
{
    public static TheoryData<string, string> UpdateRenderCases
    {
        get
        {
            var d = new TheoryData<string, string>();
            d.Add(
                """[{"content":"Step A","status":"pending"}]""",
                "[ ] Step A\n\n(0/1 completed)"
            );
            d.Add(
                """[{"content":"A","status":"completed"},{"content":"B","status":"in_progress","activeForm":"writing tests"}]""",
                "[x] A\n[>] B (writing tests)\n\n(1/2 completed)"
            );
            return d;
        }
    }

    [Theory]
    [MemberData(nameof(UpdateRenderCases))]
    public void Update_TableDriven_RendersLikePython(string itemsJson, string expected)
    {
        var state = new TodoPlanningState();
        var items = JsonDocument.Parse(itemsJson).RootElement;
        var output = TodoSessionPlanner.Update(state, items);
        Assert.Equal(expected, output);
    }

    public static TheoryData<string, string> UpdateErrorCases
    {
        get
        {
            var d = new TheoryData<string, string>();
            d.Add("""[{"content":"","status":"pending"}]""", "content required");
            d.Add("""[{"content":"x","status":"unknown"}]""", "invalid status");
            d.Add(
                """[{"content":"a","status":"in_progress"},{"content":"b","status":"in_progress"}]""",
                "Only one plan item"
            );
            return d;
        }
    }

    [Theory]
    [MemberData(nameof(UpdateErrorCases))]
    public void Update_TableDriven_ReturnsErrorPrefixWhenValidationFails(string itemsJson, string expectedFragment)
    {
        var state = new TodoPlanningState();
        var items = JsonDocument.Parse(itemsJson).RootElement;
        var output = TodoSessionPlanner.Update(state, items);
        Assert.StartsWith("Error:", output);
        Assert.Contains(expectedFragment, output);
    }

    [Fact]
    public void Update_RejectsMoreThanTwelveItems()
    {
        var arr = Enumerable.Range(0, 13)
            .Select(i => new { content = $"c{i}", status = "pending" })
            .ToArray();
        var json = JsonSerializer.Serialize(arr);
        var state = new TodoPlanningState();
        var output = TodoSessionPlanner.Update(state, JsonDocument.Parse(json).RootElement);
        Assert.Contains("max 12", output);
    }

    [Fact]
    public void Render_WithNoItems_ShowsPlaceholder()
    {
        var state = new TodoPlanningState();
        Assert.Equal("No session plan yet.", TodoSessionPlanner.Render(state));
    }

    [Fact]
    public void TryGetReminder_AfterThreeRoundsWithoutTodo_ReturnsReminder()
    {
        var state = new TodoPlanningState();
        var items = JsonDocument.Parse("""[{"content":"x","status":"pending"}]""").RootElement;
        TodoSessionPlanner.Update(state, items);

        Assert.Null(TodoSessionPlanner.TryGetReminder(state));

        TodoSessionPlanner.NoteRoundWithoutTodoUpdate(state);
        TodoSessionPlanner.NoteRoundWithoutTodoUpdate(state);
        Assert.Null(TodoSessionPlanner.TryGetReminder(state));

        TodoSessionPlanner.NoteRoundWithoutTodoUpdate(state);
        Assert.Equal(TodoSessionPlanner.ReminderText, TodoSessionPlanner.TryGetReminder(state));
    }

    [Fact]
    public void TryGetReminder_WithNoPlanItems_ReturnsNull()
    {
        var state = new TodoPlanningState();
        for (var i = 0; i < 10; i++)
            TodoSessionPlanner.NoteRoundWithoutTodoUpdate(state);
        Assert.Null(TodoSessionPlanner.TryGetReminder(state));
    }
}
