using System.Diagnostics;
using System.Text;

namespace Ripple.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string exe,
        string args,
        TimeSpan timeout,
        Action<string?>? onLine = null,
        Action<string?>? onErrLine = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) onLine?.Invoke(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            onLine?.Invoke(e.Data);
            onErrLine?.Invoke(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        CancellationTokenSource timeoutCts;

        if (timeout == TimeSpan.MaxValue)
        {
            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        else
        {
            timeoutCts = new CancellationTokenSource(timeout);
        }

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
        }

        catch (OperationCanceledException)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
            }

            catch { }

            if (ct.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"Процесс '{exe} {args}' не завершился за {timeout}.");
        }

        return proc.ExitCode;
    }
}
