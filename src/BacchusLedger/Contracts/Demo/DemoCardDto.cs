namespace BacchusLedger.Contracts.Demo;

/// One demo step card. Extra is null or one of FermentationChartExtra, ButtonsExtra, OnePercentExtra, QvevriDipExtra.
public sealed record DemoCardDto(
    string Script,
    int N,
    int Total,
    string Title,
    string? Date,
    int[] Journals,
    IReadOnlyList<DemoFieldDto> Received,
    IReadOnlyList<DemoDidDto> Did,
    object? Extra,
    long? BatchId,
    string BatchCode,
    DemoTotalsDto Totals);
