namespace ComicDeck.Core.Library;

public sealed record ScanProgress(int Processed, int Total, string CurrentFile);

public sealed record ScanFailure(string FilePath, string Reason);

public sealed record ScanResults(
    int Added,
    int Updated,
    int Unchanged,
    int Removed,
    IReadOnlyList<ScanFailure> Failures);