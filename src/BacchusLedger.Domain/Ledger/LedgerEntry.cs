namespace BacchusLedger.Domain.Ledger;

/// One movement: debit = the place that receives, credit = the place that gives. Same amount on both lines,
/// except a transformation (pressing: kg of grapes → litres of must), which carries its own credit amount and unit.
public sealed record LedgerEntry(int No, string Date, string Kind, string Text, string Debit, string Credit, double Amount, string Unit,
    double? CreditAmount = null, string? CreditUnit = null, string? Ratio = null, string? Source = null, long? OpId = null, string? Ref = null);
