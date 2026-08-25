using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.Models;

public partial class NoteItem : ObservableObject
{
    public string FilePath { get; init; } = "";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private string _preview = "";

    [ObservableProperty]
    private DateTime _modifiedAt;

    public string ModifiedText => ModifiedAt.ToString("dd.MM.yyyy HH:mm");

    partial void OnModifiedAtChanged(DateTime value) => OnPropertyChanged(nameof(ModifiedText));
}
