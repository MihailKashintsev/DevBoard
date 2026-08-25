using Avalonia.Controls;
using Avalonia.Interactivity;
using DevBoard.ViewModels;

namespace DevBoard.Views;

public partial class GitView : UserControl
{
    private bool _initialized;

    public GitView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized || DataContext is not GitViewModel vm) return;
        _initialized = true;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null) vm.Initialize(topLevel);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e) => _initialized = false;
}
