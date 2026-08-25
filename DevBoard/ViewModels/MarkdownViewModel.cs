using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevBoard.ViewModels;

public partial class MarkdownViewModel : ObservableObject
{
    private const string DefaultDocument =
        "# Добро пожаловать в DevBoard\n\n" +
        "Начните печатать слева — превью обновится автоматически.\n\n" +
        "## Возможности\n\n" +
        "- Заголовки, **жирный**, *курсив*, `код`\n" +
        "- Списки, цитаты и ссылки: [Avalonia](https://avaloniaui.net)\n\n" +
        "```\n" +
        "Блоки кода тоже поддерживаются\n" +
        "```\n";

    private static readonly FilePickerFileType MarkdownFileType = new("Markdown")
    {
        Patterns = new[] { "*.md", "*.markdown" },
        MimeTypes = new[] { "text/markdown", "text/plain" }
    };

    private static readonly FilePickerFileType AllFileType = new("Все файлы")
    {
        Patterns = new[] { "*.*" }
    };

    private TopLevel? _topLevel;
    private string? _filePath;
    private bool _suppressDirty;

    [ObservableProperty]
    private string _content = DefaultDocument;

    [ObservableProperty]
    private string _fileName = "Без имени.md";

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private string _statusText = "Готово";

    [ObservableProperty]
    private string _viewMode = "split";

    [ObservableProperty]
    private int _lineCount;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private int _charCount;

    [ObservableProperty]
    private GridLength _editorWidth = new(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _previewWidth = new(1, GridUnitType.Star);

    public bool IsEditMode => ViewMode == "edit";
    public bool IsSplitMode => ViewMode == "split";
    public bool IsPreviewMode => ViewMode == "preview";

    public MarkdownViewModel()
    {
        UpdateStats();
    }

    public void Initialize(TopLevel topLevel) => _topLevel = topLevel;

    partial void OnContentChanged(string value)
    {
        if (!_suppressDirty) IsModified = true;
        UpdateStats();
    }

    partial void OnViewModeChanged(string value)
    {
        EditorWidth = ViewMode switch
        {
            "edit" => new GridLength(1, GridUnitType.Star),
            "preview" => new GridLength(0),
            _ => new GridLength(1, GridUnitType.Star)
        };

        PreviewWidth = ViewMode switch
        {
            "edit" => new GridLength(0),
            "preview" => new GridLength(1, GridUnitType.Star),
            _ => new GridLength(1, GridUnitType.Star)
        };

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(IsPreviewMode));
    }

    [RelayCommand]
    private void SetViewMode(string? mode)
    {
        if (!string.IsNullOrEmpty(mode)) ViewMode = mode;
    }

    [RelayCommand]
    private void NewDocument()
    {
        SetContentQuiet("");
        _filePath = null;
        FileName = "Без имени.md";
        IsModified = false;
        StatusText = "Новый документ";
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_topLevel is null)
        {
            StatusText = "Окно недоступно";
            return;
        }

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть Markdown",
            AllowMultiple = false,
            FileTypeFilter = new[] { MarkdownFileType, AllFileType }
        });

        if (files.Count == 0) return;

        var file = files[0];
        var path = file.TryGetLocalPath();
        if (path is null)
        {
            StatusText = "Файл недоступен";
            return;
        }
        await LoadFromPathAsync(path);
    }

    public async Task LoadFromPathAsync(string path)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path);

            SetContentQuiet(text);
            IsModified = false;

            _filePath = path;
            FileName = Path.GetFileName(path);
            StatusText = $"Открыт · {FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка чтения: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task SaveFileAsync() => _filePath is not null ? WriteToFileAsync(_filePath) : SaveFileAsCoreAsync();

    [RelayCommand]
    private async Task SaveFileAsAsync() => await SaveFileAsCoreAsync();

    private async Task SaveFileAsCoreAsync()
    {
        if (_topLevel is null)
        {
            StatusText = "Окно недоступно";
            return;
        }

        var suggested = FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? FileName
            : FileName + ".md";

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить Markdown как",
            SuggestedFileName = suggested,
            DefaultExtension = "md",
            FileTypeChoices = new[] { MarkdownFileType },
            ShowOverwritePrompt = true
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(Content);

            _filePath = file.TryGetLocalPath();
            FileName = Path.GetFileName(_filePath) ?? file.Name;
            IsModified = false;
            StatusText = $"Сохранено · {FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private async Task WriteToFileAsync(string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, Content);
            IsModified = false;
            StatusText = $"Сохранено · {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private void SetContentQuiet(string text)
    {
        _suppressDirty = true;
        Content = text;
        _suppressDirty = false;
        UpdateStats();
    }

    private void UpdateStats()
    {
        CharCount = Content?.Length ?? 0;
        LineCount = string.IsNullOrEmpty(Content) ? 0 : Content.Count(c => c == '\n') + 1;
        WordCount = string.IsNullOrEmpty(Content) ? 0 : Regex.Matches(Content, @"\S+").Count;
    }
}
