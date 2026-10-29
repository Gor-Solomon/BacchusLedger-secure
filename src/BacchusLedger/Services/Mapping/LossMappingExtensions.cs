using BacchusLedger.Contracts.Batches;

namespace BacchusLedger.Services.Mapping;

public static class LossMappingExtensions
{
    public static StageDto ToDto(this LossEngine.Stage s) => new(
        s.OpId, s.Date, s.Type, s.StageKey, s.From, s.To, s.FromLabel, s.ToLabel, s.BeforeL, s.AfterL, s.RemainderL, s.LossL, s.LossPct,
        s.NormPct, s.NormL, s.ExcessL, s.Status, s.Bottles, s.BottleMl, s.Method, s.DipCm, s.Note, s.Source, s.Person,
        s.Other is { } o ? new StageComparisonDto(o.Winery, o.Batch, o.LossL, o.LossPct, o.Status, o.Source) : null);

    public static CheckDto ToDto(this LossEngine.Check c) => new(c.Key, c.Label, c.Status, c.Detail, c.Source);

    public static List<StageDto> ToDtos(this IEnumerable<LossEngine.Stage> stages) => stages.Select(ToDto).ToList();

    public static List<CheckDto> ToDtos(this IEnumerable<LossEngine.Check> checks) => checks.Select(ToDto).ToList();
}
