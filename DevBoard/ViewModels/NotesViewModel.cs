using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBoard.Models;

namespace DevBoard.ViewModels;

public partial class NotesViewModel : ObservableObject
{
    public static string NotesFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DevBoard", "Notes");

    private const string NewNoteTemplate = "# Новая заметка\n\n";

    private readonly List<NoteItem> _master = new();
    private bool _suppressDirty;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private NoteItem? _selectedNote;

    [ObservableProperty]
    private string _noteContent = "";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _statusText = "Готово";

    [ObservableProperty]
    private int _totalCount;

    public ObservableCollection<NoteItem> Notes { get; } = new();

    public bool HasSelection => SelectedNote is not null;

    public NotesViewModel()
    {
        LoadNotes();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedNoteChanged(NoteItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        _ = LoadSelectedAsync();
    }

    partial void OnNoteContentChanged(string value)
    {
        if (!_suppressDirty) IsDirty = true;
    }

    [RelayCommand]
    private void Refresh() => LoadNotes(keepSelection: true);

    [RelayCommand]
    private async Task NewNoteAsync()
    {
        try
        {
            Directory.CreateDirectory(NotesFolder);

            var name = "Заметка.md";
            var counter = 1;
            while (File.Exists(Path.Combine(NotesFolder, name)))
                name = $"Заметка ({++counter}).md";

            await File.WriteAllTextAsync(Path.Combine(NotesFolder, name), NewNoteTemplate);
            LoadNotes();

            var created = _master.FirstOrDefault(n => n.FileName == name);
            if (created is not null) SelectedNote = created;

            StatusText = $"Создана · {name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка создания: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync()
    {
        if (SelectedNote is null) return;

        try
        {
            if (File.Exists(SelectedNote.FilePath))
                File.Delete(SelectedNote.FilePath);

            _master.Remove(SelectedNote);
            ApplyFilter();

            SelectedNote = null;
            SetContentQuiet("");
            StatusText = "Заметка удалена";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка удаления: {ex.Message}";
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        if (SelectedNote is null) return;

        try
        {
            await File.WriteAllTextAsync(SelectedNote.FilePath, NoteContent);
            IsDirty = false;

            SelectedNote.ModifiedAt = File.GetLastWriteTime(SelectedNote.FilePath);
            SelectedNote.Preview = BuildPreview(NoteContent);
            SortMaster();
            ApplyFilter();

            StatusText = $"Сохранено · {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка сохранения: {ex.Message}";
        }
    }

    private async Task LoadSelectedAsync()
    {
        if (SelectedNote is null)
        {
            SetContentQuiet("");
            return;
        }

        try
        {
            var text = File.Exists(SelectedNote.FilePath)
                ? await File.ReadAllTextAsync(SelectedNote.FilePath)
                : "";
            SetContentQuiet(text);
            StatusText = $"Открыта · {SelectedNote.FileName}";
        }
        catch (Exception ex)
        {
            SetContentQuiet("");
            StatusText = $"Ошибка чтения: {ex.Message}";
        }
    }

    private void LoadNotes(bool keepSelection = false)
    {
        var selectedName = keepSelection ? SelectedNote?.FileName : null;

        _master.Clear();

        try
        {
            Directory.CreateDirectory(NotesFolder);

            foreach (var file in Directory.GetFiles(NotesFolder, "*.md"))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { text = ""; }

                _master.Add(new NoteItem
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    Preview = BuildPreview(text),
                    ModifiedAt = File.GetLastWriteTime(file)
                });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки: {ex.Message}";
        }

        SortMaster();
        TotalCount = _master.Count;
        ApplyFilter();

        if (selectedName is not null)
            SelectedNote = _master.FirstOrDefault(n => n.FileName == selectedName);
    }

    private void SortMaster() =>
        _master.Sort((a, b) => b.ModifiedAt.CompareTo(a.ModifiedAt));

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? "";
        var filtered = query.Length == 0
            ? _master.ToList()
            : _master.Where(n =>
                    n.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    n.Preview.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Notes.Clear();
        foreach (var note in filtered) Notes.Add(note);
    }

    private static string BuildPreview(string text)
    {
        var line = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        line = line.TrimStart('#', ' ', '\t');
        return line.Length > 90 ? line[..90] + "…" : line;
    }

    private void SetContentQuiet(string text)
    {
        _suppressDirty = true;
        NoteContent = text;
        _suppressDirty = false;
        IsDirty = false;
    }
}
