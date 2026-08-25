using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBoard.Models;
using DevBoard.Services;

namespace DevBoard.ViewModels;

public partial class GitViewModel : ObservableObject
{
    private const string FieldSeparator = "\u001f";

    private TopLevel? _topLevel;

    [ObservableProperty]
    private string _repoPath = "";

    [ObservableProperty]
    private bool _isRepo;

    [ObservableProperty]
    private string _branchName = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Укажите путь к репозиторию";

    [ObservableProperty]
    private string _commitMessage = "";

    [ObservableProperty]
    private GitFileEntry? _selectedChange;

    [ObservableProperty]
    private string _diffText = "";

    [ObservableProperty]
    private string _diffTitle = "";

    public ObservableCollection<GitFileEntry> Changes { get; } = new();
    public ObservableCollection<GitCommitInfo> Commits { get; } = new();

    public bool HasChanges => Changes.Count > 0;
    public bool HasDiff => DiffText.Length > 0;

    public GitViewModel()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--repo", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = args[i + 1];
            if (Directory.Exists(path)) RepoPath = path;
            return;
        }
    }

    public void Initialize(TopLevel topLevel) => _topLevel = topLevel;

    partial void OnSelectedChangeChanged(GitFileEntry? value) => _ = LoadDiffAsync(value);

    partial void OnDiffTextChanged(string value) => OnPropertyChanged(nameof(HasDiff));

    private async Task LoadDiffAsync(GitFileEntry? entry)
    {
        if (entry is null || !IsRepo)
        {
            DiffTitle = "";
            DiffText = "";
            return;
        }

        DiffTitle = entry.Path;

        var result = await GitService.RunAsync(
            RepoPath, $"diff HEAD -- {GitService.Quote(entry.Path)}");

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            DiffText = result.Output.TrimEnd();
            return;
        }

        DiffText = entry.IndexStatus == '?'
            ? $"Новый файл: {entry.Path}\nФайл ещё не добавлен в индекс — нажмите \"+ Добавить\"."
            : "";
    }

    partial void OnRepoPathChanged(string value)
    {
        if (Directory.Exists(value))
            _ = RefreshAsync();
        else
        {
            IsRepo = false;
            Changes.Clear();
            Commits.Clear();
        }
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (_topLevel is null) return;

        var folders = await _topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку репозитория",
            AllowMultiple = false
        });

        if (folders.Count > 0)
            RepoPath = folders[0].TryGetLocalPath() ?? "";
    }

    [RelayCommand]
    private Task RefreshRepoAsync() => RefreshAsync();

    [RelayCommand]
    private async Task StageAsync()
    {
        if (SelectedChange is null || !IsRepo || IsBusy) return;
        await RunGitAsync($"add -- {GitService.Quote(SelectedChange.Path)}", "Файл добавлен в индекс");
    }

    [RelayCommand]
    private async Task UnstageAsync()
    {
        if (SelectedChange is null || !IsRepo || IsBusy) return;
        await RunGitAsync($"reset -q HEAD -- {GitService.Quote(SelectedChange.Path)}", "Изменение убрано из индекса");
    }

    [RelayCommand]
    private async Task StageAllAsync()
    {
        if (!IsRepo || IsBusy) return;
        await RunGitAsync("add -A", "Все изменения добавлены в индекс");
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (!IsRepo || IsBusy) return;

        var message = CommitMessage.Trim();
        if (message.Length == 0)
        {
            StatusText = "Введите сообщение коммита";
            return;
        }

        var escaped = message.Replace("\"", "\\\"");
        var result = await GitService.RunAsync(RepoPath, $"commit -m \"{escaped}\"");

        if (result.Success)
        {
            CommitMessage = "";
            StatusText = "Коммит создан";
            await RefreshCoreAsync();
        }
        else
        {
            StatusText = FirstErrorLine(result.Error) is { Length: > 0 } e
                ? $"Ошибка: {e}"
                : "Ошибка коммита";
        }
    }

    private async Task RunGitAsync(string arguments, string successStatus)
    {
        var result = await GitService.RunAsync(RepoPath, arguments);
        StatusText = result.Success ? successStatus : $"Ошибка: {FirstErrorLine(result.Error)}";
        await RefreshCoreAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(RepoPath) || !Directory.Exists(RepoPath))
        {
            IsRepo = false;
            return;
        }

        IsBusy = true;
        try
        {
            var check = await GitService.RunAsync(RepoPath, "rev-parse --is-inside-work-tree");
            IsRepo = check.Success && check.Output.Trim() == "true";

            if (!IsRepo)
            {
                BranchName = "";
                Changes.Clear();
                Commits.Clear();
                StatusText = "Это не Git-репозиторий";
                return;
            }

            await RefreshCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCoreAsync()
    {
        SelectedChange = null;

        var branch = await GitService.RunAsync(RepoPath, "branch --show-current");
        BranchName = branch.Output.Trim();

        var status = await GitService.RunAsync(RepoPath, "status --porcelain=v1");
        Changes.Clear();

        if (status.Success)
        {
            foreach (var line in status.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length < 4) continue;

                var pathPart = trimmed[3..];
                var arrowIndex = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrowIndex >= 0)
                    pathPart = pathPart[(arrowIndex + 4)..];

                Changes.Add(new GitFileEntry
                {
                    IndexStatus = trimmed[0],
                    WorkTreeStatus = trimmed[1],
                    Path = pathPart.Trim('"')
                });
            }
        }

        var log = await GitService.RunAsync(
            RepoPath,
            $"log -n 40 --date=format:\"%d.%m.%Y %H:%M\" --pretty=format:\"%h{FieldSeparator}%an{FieldSeparator}%ad{FieldSeparator}%s\"");

        Commits.Clear();
        if (log.Success && log.Output.Length > 0)
        {
            foreach (var line in log.Output.Split('\n'))
            {
                var parts = line.TrimEnd('\r').Split(FieldSeparator);
                if (parts.Length < 4) continue;

                Commits.Add(new GitCommitInfo
                {
                    Hash = parts[0],
                    Author = parts[1],
                    Date = parts[2],
                    Subject = parts[3]
                });
            }
        }

        OnPropertyChanged(nameof(HasChanges));

        if (Changes.Count > 0)
            SelectedChange ??= Changes[0];

        StatusText = $"{BranchName} · изменений: {Changes.Count} · коммитов: {Commits.Count}";
    }

    private static string FirstErrorLine(string error)
    {
        var line = error.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return line?.Trim() ?? "";
    }
}
