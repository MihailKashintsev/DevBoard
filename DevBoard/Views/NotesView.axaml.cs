using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevBoard.ViewModels;

namespace DevBoard.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
    }

    private void OnNoteEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NotesViewModel vm) return;

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.SaveNoteCommand.Execute(null);
            e.Handled = true;
        }
    }
}
