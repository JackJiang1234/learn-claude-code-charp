namespace AgentLoop.Bash;

/// <summary>在当前工作目录执行 shell 命令（与 Python 版 <c>run_bash</c> 对齐）。</summary>
public interface IBashRunner
{
    /// <param name="command">整条命令字符串，在 Windows 下通过 <c>cmd.exe /c</c> 执行。</param>
    string Run(string command);
}
