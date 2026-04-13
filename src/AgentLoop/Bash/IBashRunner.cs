namespace AgentLoop.Bash;

/// <summary>Runs a shell command in the current working directory (Python <c>run_bash</c>).</summary>
public interface IBashRunner
{
    /// <param name="command">Full command string; on Windows runs via <c>cmd.exe /c</c>.</param>
    string Run(string command);
}
