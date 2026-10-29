using BacchusLedger.Models;
using BacchusLedger.Domain.Losses;

namespace BacchusLedger;

/// Ledger report: one batch from vineyard to bottle as a double-entry ledger. Read-only (SELECTs only).
/// Litres and losses come from LossEngine.Analyze; this class only books them as debit/credit pairs.
/// Simulated live events are listed separately and never enter the ledger.
public static class LedgerReport
{
    public const string Reception = "Winery reception (grapes)", Losses = "Losses", Unexplained = "Unexplained difference",
        Bottled = "Bottled wine", Warehouse = "Finished goods warehouse";

    /// One movement: debit = the place that receives, credit = the place that gives. Same amount on both lines,
    /// except a transformation (pressing: kg of grapes → litres of must), which carries its own credit amount and unit.
    public sealed record Entry(int No, string Date, string Kind, string Text, string Debit, string Credit, double Amount, string Unit,
        double? CreditAmount = null, string? CreditUnit = null, string? Ratio = null, string? Source = null, long? OpId = null, string? Ref = null);

    public sealed record Account(string Name, string Unit, double Debit, double Credit)
    {
        public double Balance => Math.Round(Debit - Credit, 2);
    }

    public sealed record Balance(double? GrapesKg, double HarvestedKg, double PressedKg, double GrapesLeftKg, double? KgGap,
        double InL, double BottledL, double InVesselsL, double LossesL, double GapL, bool HasLitres, bool EngineMatches, bool Balanced);

    public sealed record Ledger(List<Entry> Entries, List<Account> Accounts, Balance Balance, LossResult Engine);

    static double R(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static string VesselName(string? code, string? label) => code == null ? "Vessel not recorded" : Fmt.Vessel(code, label);
    static string Grapes(string? code, string? label) => $"{VesselName(code, label)} · grapes";

    /// Typical cause per stage (the record has the litres, not the cause).
    static string Cause(LossStage s) => s.StageKey switch
    {
        "racking_first" or "racking" => "lees (racking)",
        "filtration" => "filtration",
        "bottling" => "bottling",
        "stock_check" => "evaporation / sampling (stock check)",
        _ => s.Type,
    };

    /// Books a batch: harvest deliveries (kg), pressing (kg → L) and every replayed stage (L) as debit/credit pairs.
    public static Ledger Build(double? grapesKg, IReadOnlyList<LedgerOrigin> origins, LossResult eng)
    {
        var entries = new List<Entry>();
        var acc = new Dictionary<(string, string), (double dr, double cr)>();
        void Book(string date, string kind, string text, string debit, string credit, double amount, string unit,
            double? crAmount = null, string? crUnit = null, string? ratio = null, string? source = null, long? op = null, string? reference = null)
        {
            entries.Add(new(entries.Count + 1, date, kind, text, debit, credit, R(amount), unit, crAmount == null ? null : R(crAmount.Value), crUnit, ratio, source, op, reference ?? (op != null ? $"operations:{op}" : null)));
            var d = acc.GetValueOrDefault((debit, unit)); acc[(debit, unit)] = (d.dr + amount, d.cr);
            var cu = crUnit ?? unit; var k = acc.GetValueOrDefault((credit, cu)); acc[(credit, cu)] = (k.dr, k.cr + (crAmount ?? amount));
        }
        double Bal(string name, string unit) => acc.TryGetValue((name, unit), out var x) ? x.dr - x.cr : 0;

        // Harvest: vineyard plot → winery reception (kg). Transport is not recorded separately in the data.
        var firstDate = eng.Stages.FirstOrDefault()?.Date ?? "";
        foreach (var o in origins)
            Book(o.Date, "harvest", $"Delivery from plot {o.Plot} ({o.Municipality})", Reception,
                $"Vineyard {o.Plot} · {o.Farmer ?? "farmer not recorded"}", o.Kg ?? 0, "kg", source: o.Source, reference: $"deliveries:{o.DeliveryId}");
        if (origins.Count == 0 && grapesKg is > 0)
            Book(firstDate, "harvest", "Grapes received (plots not recorded)", Reception, "Vineyard (plots not recorded)", grapesKg.Value, "kg", source: "Dataset A2");
        double harvested = acc.Where(a => a.Key.Item2 == "kg" && a.Key.Item1.StartsWith("Vineyard")).Sum(a => a.Value.cr);

        foreach (var s in eng.Stages)
        {
            void Loss(string vessel)
            {
                if (s.LossL is not { } l) return;
                if (l > 0) Book(s.Date, "loss", $"Loss at {s.StageKey.Replace('_', ' ')}: {Cause(s)}", Losses, vessel, l, "L", source: "Calculated", op: s.OpId);
                else if (l < 0) Book(s.Date, "unexplained", $"More wine than the record explains at {s.StageKey.Replace('_', ' ')}", vessel, Unexplained, -l, "L", source: "Calculated", op: s.OpId);
            }
            switch (s.Type)
            {
                case "intake":
                {
                    var kg = Bal(Reception, "kg");
                    if (kg > 0) Book(s.Date, "intake", $"Grapes into {VesselName(s.To, s.ToLabel)}" + (s.Note != null ? $" ({s.Note})" : ""), Grapes(s.To, s.ToLabel), Reception, kg, "kg", source: s.Source, op: s.OpId);
                    break;
                }
                case "separation":
                {
                    // Transformation: all grapes in the source vessel (or still at reception) become the litres measured.
                    var from = Grapes(s.From, s.FromLabel);
                    var kg = Bal(from, "kg");
                    if (kg <= 0) { from = Reception; kg = Bal(Reception, "kg"); }
                    var l = s.AfterL ?? 0;
                    var ratio = kg > 0 ? $"{l / kg * 1000:N1} L per tonne ({l / kg:0.000} L/kg)" : "no grapes recorded";
                    Book(s.Date, "transformation", $"Pressing / separation: grapes → wine into {VesselName(s.To, s.ToLabel)}", VesselName(s.To, s.ToLabel), kg > 0 ? from : "Grapes (not recorded)",
                        l, "L", Math.Max(kg, 0), "kg", ratio, s.Source, s.OpId);
                    break;
                }
                case "racking" or "filtration" or "transfer":
                    if (s.AfterL is > 0) Book(s.Date, "move", $"{char.ToUpper(s.Type[0])}{s.Type[1..]} {s.From} → {s.To}", VesselName(s.To, s.ToLabel), VesselName(s.From, s.FromLabel), s.AfterL.Value, "L", source: s.Source, op: s.OpId);
                    Loss(VesselName(s.From, s.FromLabel));
                    break;
                case "stock_check":
                    if (s.LossL is 0 or null) Book(s.Date, "stock check", $"Stock check {s.From}: measured {s.AfterL:N0} L = book {s.BeforeL:N0} L, no difference", Losses, VesselName(s.From, s.FromLabel), 0, "L", source: s.Source, op: s.OpId);
                    Loss(VesselName(s.From, s.FromLabel));
                    break;
                case "bottling":
                    Book(s.Date, "bottling", $"Bottling {s.Bottles:N0} × {(s.BottleMl ?? 750) / 1000:0.00#} L from {s.From}" + (s.RemainderL is > 0 ? $" ({s.RemainderL:N0} L stays in {s.From})" : ""),
                        Bottled, VesselName(s.From, s.FromLabel), s.AfterL ?? 0, "L", source: s.Source, op: s.OpId);
                    Loss(VesselName(s.From, s.FromLabel));
                    break;
                case "warehouse":
                {
                    var l = (s.Bottles ?? 0) * (s.BottleMl ?? 750) / 1000.0;
                    if (l > 0) Book(s.Date, "warehouse", $"{s.Bottles:N0} bottles to the warehouse", Warehouse, Bottled, l, "L", source: s.Source, op: s.OpId);
                    break;
                }
            }
        }

        var accounts = acc.Select(a => new Account(a.Key.Item1, a.Key.Item2, R(a.Value.dr), R(a.Value.cr))).ToList();
        double inL = entries.Where(e => e.Kind == "transformation").Sum(e => e.Amount);
        double bottled = R(Bal(Bottled, "L") + Bal(Warehouse, "L"));
        var special = new[] { Bottled, Warehouse, Losses, Unexplained };
        var vesselAcc = accounts.Where(a => a.Unit == "L" && !special.Contains(a.Name)).ToList();
        double inVessels = R(vesselAcc.Sum(a => a.Balance));
        double losses = R(Bal(Losses, "L"));
        double gap = R(inL - bottled - inVessels - losses);
        double pressed = entries.Where(e => e.Kind == "transformation").Sum(e => e.CreditAmount ?? 0);
        double left = R(accounts.Where(a => a.Unit == "kg" && !a.Name.StartsWith("Vineyard")).Sum(a => a.Balance));
        double? kgGap = grapesKg == null || origins.Count == 0 ? null : R(grapesKg.Value - harvested);
        // Cross-check with LossEngine: same total loss (positive losses minus unexplained gains) and the same litres per vessel.
        bool engineOk = Math.Abs(losses + gap - eng.TotalLossL) < 0.01
            && Math.Abs(inVessels - eng.LitresNow) < 0.01;
        bool hasL = inL > 0;
        var bal = new Balance(grapesKg, R(harvested), R(pressed), left, kgGap, R(inL), bottled, inVessels, losses, gap, hasL, engineOk,
            Math.Abs(gap) < 0.005 && (kgGap == null || Math.Abs(kgGap.Value) < 0.005) && engineOk);
        return new(entries, accounts, bal, eng);
    }
}
