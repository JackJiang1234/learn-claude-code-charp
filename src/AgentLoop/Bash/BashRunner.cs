using System.Diagnostics;
using System.Text;
using AgentLoop.Domain;

namespace AgentLoop.Bash;

public sealed class BashRunner : IBashRunner
{
    public const int DefaultTimeoutSeconds = 120;
    public const int MaxOutputLength = 50_000;

    private static readonly string[] DangerousFragments =
    [
        "rm -rf /",
        "sudo",
        "shutdown",
        "reboot",
        "> /dev/",
    ];

    private readonly string _workingDirectory;
    private readonly int _timeoutSeconds;

    public BashRunner(string workingDirectory, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        _workingDirectory = workingDirectory;
        _timeoutSeconds = timeoutSeconds;
    }

    public string Run(string command)
    {
        if (ContainsDangerousFragment(command))
            return "Error: Dangerous command blocked";

        try
        {
            var psi = CreateProcessStartInfo(command);
            return RunProcessAndCollectOutput(psi);
        }
        catch (Exception ex) when (ex is not AgentLoopException)
        {
            return $"Error: {ex.Message}";
        }
    }

    static bool ContainsDangerousFragment(string command)
    {
        foreach (var fragment in DangerousFragments)
        {
            if (command.Contains(fragment, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    ProcessStartInfo CreateProcessStartInfo(string command)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = _workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = "/c " + command;
        }
        else
        {
            psi.FileName = "/bin/sh";
            psi.Arguments = "-c " + command;
        }

        return psi;
    }

    string RunProcessAndCollectOutput(ProcessStartInfo psi)
    {
        using var proc = Process.Start(psi) ?? throw new AgentLoopException("Failed to start shell process.");

        var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
        if (!proc.WaitForExit(_timeoutSeconds * 1000))
        {
            TryKillProcessTree(proc);
            return $"Error: Timeout ({_timeoutSeconds}s)";
        }

        var output = new StringBuilder();
        output.Append(stdoutTask.GetAwaiter().GetResult());
        output.Append(stderrTask.GetAwaiter().GetResult());
        return NormalizeOutput(output.ToString());
    }

    static void TryKillProcessTree(Process proc)
    {
        try
        {
            proc.Kill(entireProcessTree: true);
        }
        catch
        {
            /* ignore */
        }
    }

    static string NormalizeOutput(string raw)
    {
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text))
            return "(no output)";
        return text.Length <= MaxOutputLength ? text : text[..MaxOutputLength];
    }
}
