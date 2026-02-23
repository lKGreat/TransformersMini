using System.Diagnostics;
using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.Infrastructure.Runtime;

public sealed class SystemProbe : ISystemProbe
{
    public bool IsCudaAvailable()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "nvidia-smi";
            process.StartInfo.Arguments = "--query-gpu=name --format=csv,noheader";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            if (!process.Start())
            {
                return false;
            }

            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
