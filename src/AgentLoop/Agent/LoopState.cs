using Anthropic.Models.Messages;

namespace AgentLoop.Agent;

/// <summary>与 Python <c>LoopState</c> 对应：历史消息、轮次与继续原因。</summary>
public sealed class LoopState
{
    public LoopState(List<MessageParam> messages)
    {
        Messages = messages;
    }

    public List<MessageParam> Messages { get; }
    public int TurnCount { get; set; } = 1;
    public string? TransitionReason { get; set; }
}
