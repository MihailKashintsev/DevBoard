using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DevBoard.Services;
using DevBoard.ViewModels;

namespace DevBoard.Views;

public partial class MarkdownView : UserControl
{
    private bool _initialized;

    private MarkdownViewModel? Vm => DataContext as MarkdownViewModel;

    public MarkdownView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized || Vm is null) return;
        _initialized = true;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null) Vm.Initialize(topLevel);

        RefreshPreview();
        Vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.PropertyChanged -= OnVmPropertyChanged;
        _initialized = false;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MarkdownViewModel.Content))
            RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            PreviewHost.Content = MarkdownRenderer.Render(Vm?.Content);
        }
        catch (Exception ex)
        {
            PreviewHost.Content = new TextBlock
            {
                Text = "Ошибка предпросмотра: " + ex.Message,
                Foreground = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.Parse("#E06C75")),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 20)
            };
        }
    }

    // ───────────────── Hotkeys ─────────────────

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is null) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.S:
                    Vm.SaveFileCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.N:
                    Vm.NewDocumentCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.O:
                    Vm.OpenFileCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key is Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            InsertText("    ");
            e.Handled = true;
        }
    }

    // ───────────────── Drag & Drop ─────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;

        var file = e.Data.GetFiles()?.FirstOrDefault();
        var path = file?.TryGetLocalPath();

        if (path is null) return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".md" or ".markdown" or ".txt"))
        {
            Vm.StatusText = "Поддерживаются только .md / .markdown / .txt";
            return;
        }

        await Vm.LoadFromPathAsync(path);
    }

    // ───────────────── Formatting ─────────────────

    private void OnBoldClick(object? sender, RoutedEventArgs e) => WrapSelection("**", "**");

    private void OnItalicClick(object? sender, RoutedEventArgs e) => WrapSelection("*", "*");

    private void OnHeadingClick(object? sender, RoutedEventArgs e) => PrefixLines("# ");

    private void OnBulletClick(object? sender, RoutedEventArgs e) => PrefixLines("- ");

    private void OnNumberedClick(object? sender, RoutedEventArgs e) => PrefixNumberedLines();

    private void InsertText(string insertion)
    {
        if (Editor is null) return;

        var text = Editor.Text ?? string.Empty;
        var caret = Editor.CaretIndex;

        var newText = text.Insert(caret, insertion);
        Editor.Text = newText;
        Editor.CaretIndex = caret + insertion.Length;

        SyncContent(newText);
    }

    private void WrapSelection(string prefix, string suffix)
    {
        if (Editor is null) return;

        var text = Editor.Text ?? string.Empty;
        var selStart = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        var selEnd = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);

        var newText = new StringBuilder(text.Length + prefix.Length + suffix.Length)
            .Append(text, 0, selStart)
            .Append(prefix)
            .Append(text, selStart, selEnd - selStart)
            .Append(suffix)
            .Append(text[selEnd..])
            .ToString();

        Editor.Text = newText;
        Editor.CaretIndex = selEnd + prefix.Length;
        Editor.SelectionStart = selStart + prefix.Length;
        Editor.SelectionEnd = selEnd + prefix.Length;

        SyncContent(newText);
    }

    private void PrefixLines(string prefix)
    {
        if (Editor is null) return;

        var text = Editor.Text ?? string.Empty;
        var selStart = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        var selEnd = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);
        var lineStart = LineStartIndex(text, selStart);
        var lineEnd = LineEndIndex(text, selEnd);

        var sb = new StringBuilder(text.Length + prefix.Length * 4);
        sb.Append(text, 0, lineStart);

        for (var i = lineStart; i < lineEnd;)
        {
            var nextNewline = text.IndexOf('\n', i);
            if (nextNewline == -1 || nextNewline >= lineEnd) nextNewline = lineEnd;

            var lineIsEmpty = i == nextNewline;
            if (!lineIsEmpty) sb.Append(prefix);

            sb.Append(text, i, nextNewline - i);
            if (nextNewline < lineEnd) sb.Append('\n');

            i = nextNewline + 1;
        }

        sb.Append(text[lineEnd..]);
        var newText = sb.ToString();

        Editor.Text = newText;
        Editor.CaretIndex = Math.Min(selEnd + prefix.Length, newText.Length);
        Editor.SelectionStart = lineStart + prefix.Length;
        Editor.SelectionEnd = Math.Min(lineEnd + prefix.Length, newText.Length);

        SyncContent(newText);
    }

    private void PrefixNumberedLines()
    {
        if (Editor is null) return;

        var text = Editor.Text ?? string.Empty;
        var selStart = Math.Min(Editor.SelectionStart, Editor.SelectionEnd);
        var selEnd = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);
        var lineStart = LineStartIndex(text, selStart);
        var lineEnd = LineEndIndex(text, selEnd);

        var sb = new StringBuilder(text.Length + 16);
        sb.Append(text, 0, lineStart);

        var counter = 1;
        for (var i = lineStart; i < lineEnd;)
        {
            var nextNewline = text.IndexOf('\n', i);
            if (nextNewline == -1 || nextNewline >= lineEnd) nextNewline = lineEnd;

            if (i != nextNewline)
                sb.Append(counter++).Append(". ");

            sb.Append(text, i, nextNewline - i);
            if (nextNewline < lineEnd) sb.Append('\n');

            i = nextNewline + 1;
        }

        sb.Append(text[lineEnd..]);
        var newText = sb.ToString();

        Editor.Text = newText;
        Editor.CaretIndex = Math.Min(selEnd + 3, newText.Length);
        Editor.SelectionStart = lineStart;
        Editor.SelectionEnd = Math.Min(lineEnd + (counter - 1) * 3, newText.Length);

        SyncContent(newText);
    }

    private static int LineStartIndex(string text, int index)
    {
        var newline = text.LastIndexOf('\n', Math.Clamp(index - 1, 0, Math.Max(0, text.Length - 1)));
        return newline + 1;
    }

    private static int LineEndIndex(string text, int index)
    {
        if (text.Length == 0) return 0;
        var newline = text.IndexOf('\n', Math.Min(index, text.Length - 1));
        return newline == -1 ? text.Length : newline;
    }

    private void SyncContent(string newText)
    {
        if (Vm is not null && Vm.Content != newText)
            Vm.Content = newText;
    }
}
