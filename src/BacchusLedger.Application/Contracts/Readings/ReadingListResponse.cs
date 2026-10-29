namespace BacchusLedger.Application.Contracts.Readings;

public sealed record ReadingListResponse(
    IReadOnlyList<ReadingRowDto> Rows,
    IReadOnlyList<InitialBrixDto> InitialBrix,
    IReadOnlyList<InitialSugarDto> InitialSugar,
    ReadingFormulasDto Formulas);
