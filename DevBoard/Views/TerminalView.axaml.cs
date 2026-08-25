using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevBoard.ViewModels;

namespace DevBoard.Views;

public partial class TerminalView : UserControl
{
    private bool _initialized;

    private TerminalViewModel? Vm => DataContext as TerminalViewModel;

    public TerminalView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized || Vm is null) return;
        _initialized = true;

        Vm.OutputAppended += OnOutputAppended;
        InputBox.Focus();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.OutputAppended -= OnOutputAppended;
        _initialized = false;
    }

    private void OnOutputAppended(object? sender, EventArgs e)
    {
        OutputScroll.ScrollToEnd();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is null) return;

        switch (e.Key)
        {
            case Key.Enter:
                Vm.ExecuteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                Vm.NavigateHistoryCommand.Execute(-1);
                InputBox.CaretIndex = InputLineLength();
                e.Handled = true;
                break;
            case Key.Down:
                Vm.NavigateHistoryCommand.Execute(1);
                InputBox.CaretIndex = InputLineLength();
                e.Handled = true;
                break;
        }
    }

    private int InputLineLength() => InputBox.Text?.Length ?? 0;
}
