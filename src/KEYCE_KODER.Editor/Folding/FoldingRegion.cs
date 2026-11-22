namespace KEYCE_KODER.Editor.Folding;

public sealed record FoldingRegion(
    int StartOffset,
    int EndOffset,
    string Title
);
