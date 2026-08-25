namespace DevBoard.Models;

public class GitFileEntry
{
    public char IndexStatus { get; init; }
    public char WorkTreeStatus { get; init; }
    public string Path { get; init; } = "";

    public string Badge =>
        IndexStatus == '?' ? "??" : $"{IndexStatus}{WorkTreeStatus}".Trim() + " ";

    public bool IsStaged => IndexStatus is not (' ' or '?');

    public bool IsAdded => IndexStatus is 'A' or '?';
    public bool IsModified => !IsAdded && (IndexStatus is 'M' || WorkTreeStatus is 'M');
    public bool IsDeleted => IndexStatus is 'D' || WorkTreeStatus is 'D';
    public bool IsRenamed => IndexStatus is 'R';
}

public class GitCommitInfo
{
    public string Hash { get; init; } = "";
    public string Author { get; init; } = "";
    public string Date { get; init; } = "";
    public string Subject { get; init; } = "";
}
