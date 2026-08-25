using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace DevBoard.Services;

public record GitResult(bool Success, string Output, string Error);

public static class GitService
{
    public static async Task<GitResult> RunAsync(
        string repoPath, string arguments, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null)
                return new GitResult(false, "", "Не удалось запустить процесс git");

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return new GitResult(process.ExitCode == 0, await outputTask, await errorTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new GitResult(false, "", ex.Message);
        }
    }

    public static string Quote(string path) =>
        "\"" + path.Replace("\"", "\\\"") + "\"";
}
