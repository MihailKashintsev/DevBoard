using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevBoard.ViewModels;

public partial class TerminalViewModel : ObservableObject
{
    private const string Banner =
        "DevBoard Terminal\n" +
        "Команды выполняются через cmd.exe. cd сохраняется между командами.\n" +
        "cls - очистить экран.\n\n";

    private readonly List<string> _history = new();
    private int _historyIndex;

    private string _cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty]
    private string _output = Banner;

    [ObservableProperty]
    private string _inputLine = "";

    [ObservableProperty]
    private bool _isBusy;

    public string Prompt => _cwd + "> ";

    public ObservableCollection<string> QuickCommands { get; } = new()
    {
        "dir",
        "git status",
        "dotnet --version",
        "systeminfo | findstr /B /C:\"OS\"",
        "cls"
    };

    public TerminalViewModel()
    {
        _historyIndex = 0;
        AppendLine(Prompt);
    }

    public event EventHandler? OutputAppended;

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        var command = InputLine.Trim();
        InputLine = "";

        if (command.Length == 0)
        {
            AppendLine(Prompt);
            NotifyOutput();
            return;
        }

        _history.Add(command);
        _historyIndex = _history.Count;

        AppendLine(Prompt + command);

        if (command.Equals("cls", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Output = "";
            AppendLine(Prompt);
            NotifyOutput();
            return;
        }

        if (command.StartsWith("cd", StringComparison.OrdinalIgnoreCase) &&
            (command.Length == 2 || command[2] is ' ' or '\t'))
        {
            HandleCd(command[2..].Trim());
            AppendLine(Prompt);
            NotifyOutput();
            return;
        }

        IsBusy = true;
        try
        {
            await RunCommandAsync(command);
            AppendLine("");
            AppendLine(Prompt);
        }
        finally
        {
            IsBusy = false;
            NotifyOutput();
        }
    }

    [RelayCommand]
    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;

        var next = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        _historyIndex = next;
        InputLine = next < _history.Count ? _history[next] : "";
    }

    [RelayCommand]
    private async Task RunQuick(string? command)
    {
        if (string.IsNullOrWhiteSpace(command) || IsBusy) return;
        InputLine = command;
        await ExecuteAsync();
    }

    private void HandleCd(string target)
    {
        if (target.Length == 0)
        {
            AppendLine(_cwd);
            OnPropertyChanged(nameof(Prompt));
            return;
        }

        if (target.StartsWith("/d ", StringComparison.OrdinalIgnoreCase))
            target = target[3..].Trim();

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(_cwd, target));

            if (Directory.Exists(fullPath))
            {
                _cwd = fullPath;
                OnPropertyChanged(nameof(Prompt));
            }
            else
            {
                AppendLine("Системе не удается найти указанный путь.");
            }
        }
        catch (Exception ex)
        {
            AppendLine("Ошибка пути: " + ex.Message);
        }
    }

    private async Task RunCommandAsync(string command)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                WorkingDirectory = _cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                AppendLine("Не удалось запустить процесс");
                return;
            }

            var stdoutTask = ToBytesAsync(process.StandardOutput.BaseStream);
            var stderrTask = ToBytesAsync(process.StandardError.BaseStream);
            await process.WaitForExitAsync();

            var stdout = Decode(await stdoutTask);
            var stderr = Decode(await stderrTask);

            if (stdout.Length > 0) Append(stdout.TrimEnd());
            if (stderr.Length > 0)
            {
                if (stdout.Length > 0) AppendLine("");
                Append(stderr.TrimEnd());
            }

            if (process.ExitCode != 0)
                AppendLine($"\nКод выхода: {process.ExitCode}");
        }
        catch (Exception ex)
        {
            AppendLine("Ошибка выполнения: " + ex.Message);
        }
    }

    private void Append(string text)
    {
        Output += text;
        NotifyOutput();
    }

    private void AppendLine(string text)
    {
        Output += text + Environment.NewLine;
        NotifyOutput();
    }

    private void NotifyOutput() => OutputAppended?.Invoke(this, EventArgs.Empty);

    private static async Task<byte[]> ToBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var lastNonNul = Array.FindLastIndex(bytes, b => b != 0);
        return lastNonNul < 0 ? Array.Empty<byte>() : bytes[..(lastNonNul + 1)];
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return "";

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch
        {
            try { return Encoding.GetEncoding(866).GetString(bytes); }
            catch { return Encoding.Default.GetString(bytes); }
        }
    }
}
