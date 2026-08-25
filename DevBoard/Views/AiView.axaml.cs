using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DevBoard.ViewModels;

namespace DevBoard.Views;

public partial class AiView : UserControl
{
    public AiView()
    {
        InitializeComponent();
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is AiViewModel vm)
        {
            vm.SendMessageCommand.Execute(null);
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MessagesScroller is ScrollViewer sv)
                sv.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
