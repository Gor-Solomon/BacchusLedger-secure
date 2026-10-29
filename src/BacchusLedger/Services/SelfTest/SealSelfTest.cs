using BacchusLedger.Dataset;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services.SelfTest;

/// Chain + signature (rolled back).
public sealed class SealSelfTest(ISealService seals, ISealRepository sealTables, IBatchRepository batches, IUnitOfWork unitOfWork) : ISelfTestSection
{
    public async Task RunAsync(SelfTestContext ctx)
    {
        const string g = "seal";
        var Add = ctx.Add;
        try
        {
            var (ok, tampered, at, sigBad) = await unitOfWork.ProbeAsync(async () =>
            {
                await sealTables.EnsureTablesAsync();
                var id = (await batches.FindIdAsync(SeedDataset.W03Batch))!.Value;
                await seals.EnsureAsync(id);
                var v1 = await seals.CheckAsync(id);
                var t = await seals.TamperAsync(id);
                var v2 = await seals.CheckAsync(id);
                return (v1, v2, t, seals.CheckSignature(v1.FinalHash + "0", v1.Signature!));
            });
            Add(g, "W03-B3-2025: chain + signature verify unchanged", "verified", $"{ok.Status}, {ok.Entries} entries, signature {(ok.SignatureOk ? "ok" : "bad")}", ok.Ok && ok.SignatureOk && ok.Entries > 0);
            Add(g, "Changing one lab value is detected at that entry", $"tampered at {at?.Entry}", $"{tampered.Status} at {tampered.TamperedAt}",
                !tampered.Ok && tampered.Status == "tampered" && at != null && tampered.TamperedAt == at.Entry);
            Add(g, "Signature does not verify for a different hash", "invalid", sigBad ? "valid" : "invalid", !sigBad);
        }
        catch (Exception e) { Add(g, "seal", "ok", e.Message, false); }
    }
}
