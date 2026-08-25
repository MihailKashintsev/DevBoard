using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBoard.Services;

namespace DevBoard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _selectedSection = "markdown";

    [ObservableProperty]
    private string _sectionTitle = "Markdown";

    [ObservableProperty]
    private string _sectionSubtitle = "Просмотр и редактирование Markdown файлов";

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private string _updateVersion = "";

    [ObservableProperty]
    private string _updateNotes = "";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _updateStatus = "";

    public string CurrentVersion => UpdateService.GetCurrentVersion().ToString();

    public MainViewModel()
    {
        Navigate(GetInitialSection());
        _ = CheckForUpdateAsync();
    }

    private static string GetInitialSection()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--section", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = args[i + 1].ToLowerInvariant();
            return value is "markdown" or "notes" or "git" or "terminal" ? value : "markdown";
        }
        return "markdown";
    }

    public bool IsMarkdownActive => SelectedSection == "markdown";
    public bool IsNotesActive => SelectedSection == "notes";
    public bool IsGitActive => SelectedSection == "git";
    public bool IsTerminalActive => SelectedSection == "terminal";

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (string.IsNullOrEmpty(section)) return;

        SelectedSection = section.ToLower();

        (CurrentView, SectionTitle, SectionSubtitle) = SelectedSection switch
        {
            "markdown" => (new MarkdownViewModel(), "Markdown", "Просмотр и редактирование Markdown файлов"),
            "notes"    => (new NotesViewModel(),    "Заметки",  "Управление заметками и документацией"),
            "git"      => (new GitViewModel(),      "Git",      "Управление Git репозиториями"),
            "terminal" => (new TerminalViewModel(), "Терминал", "Встроенный терминал для команд"),
            _ => (CurrentView, SectionTitle, SectionSubtitle)
        };

        OnPropertyChanged(nameof(IsMarkdownActive));
        OnPropertyChanged(nameof(IsNotesActive));
        OnPropertyChanged(nameof(IsGitActive));
        OnPropertyChanged(nameof(IsTerminalActive));
    }

    private async Task CheckForUpdateAsync()
    {
        IsCheckingUpdate = true;
        try
        {
            var update = await UpdateService.CheckForUpdateAsync();
            if (update != null)
            {
                HasUpdate = true;
                UpdateVersion = update.TagName;
                UpdateNotes = update.ReleaseNotes;
            }
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DismissUpdateAsync()
    {
        HasUpdate = false;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsUpdating) return;

        IsUpdating = true;
        UpdateStatus = "Скачивание...";

        try
        {
            var update = await UpdateService.CheckForUpdateAsync();
            if (update == null)
            {
                UpdateStatus = "Обновление не найдено";
                IsUpdating = false;
                return;
            }

            await UpdateService.DownloadAndInstallAsync(update, status =>
            {
                UpdateStatus = status;
            });
        }
        catch
        {
            UpdateStatus = "Ошибка обновления";
            IsUpdating = false;
        }
    }
}
