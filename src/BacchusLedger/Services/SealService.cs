using BacchusLedger.Contracts.Seal;
using BacchusLedger.Domain.Seal;
using BacchusLedger.Models;
using BacchusLedger.Providers;
using BacchusLedger.Repositories;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace BacchusLedger.Services;

public sealed class SealService(
    ISealRepository seals, IBatchRepository batches, ISigningKeyProvider key, IUnitOfWork unitOfWork, ILogger<SealService> logger) : ISealService
{
    /// Version 2 added deliveries to the chain (older seals are rebuilt once).
    const string Version = "2";

    public async Task<ServiceResult<SealResponse>> SealAsync(string? batch, string baseUrl)
    {
        var id = await unitOfWork.WriteAsync(async () =>
        {
            await seals.EnsureTablesAsync();
            var i = batch == null ? null : await batches.FindIdAsync(batch);
            if (i != null) await EnsureAsync(i.Value);
            return i;
        });
        if (id == null) return ServiceResult<SealResponse>.NotFound("batch not found");
        var s = (await seals.GetAsync(id.Value))!;
        var url = $"{baseUrl}/verify.html?batch={Uri.EscapeDataString(batch!)}";
        using var qr = new QRCodeGenerator();
        var png = new PngByteQRCode(qr.CreateQrCode(url, QRCodeGenerator.ECCLevel.M)).GetGraphic(6);
        var chain = (await seals.ChainAsync(id.Value)).Select(e => new SealEntryDto(e.Seq, e.Tbl, e.RowId, e.Hash, e.Prev)).ToList();
        return new SealResponse(batch!, s.FinalHash, s.Signature, s.SealedAt, s.Entries, s.KeyId, "SHA-256 hash chain · ECDSA P-256 (DEMO KEY)", url, chain,
            "data:image/png;base64," + Convert.ToBase64String(png));
    }

    public async Task<ServiceResult<VerifyResponse>> VerifyAsync(string? batch)
    {
        if ((batch == null ? null : await batches.FindIdAsync(batch)) is not { } id) return ServiceResult<VerifyResponse>.NotFound("batch not found");
        return new VerifyResponse(batch, await CheckAsync(id), key.PublicKeyPem, "DEMO KEY — not for production");
    }

    public async Task<ServiceResult<ResetSealResponse>> ResetDemoAsync(string? batch)
    {
        var r = await unitOfWork.WriteAsync(async () =>
        {
            await seals.EnsureTablesAsync();
            if ((batch == null ? null : await batches.FindIdAsync(batch)) is not { } id) return null;
            var t = await seals.TamperRecordsAsync(id);
            foreach (var x in t) await seals.SetLabTemperatureAsync(x.RowId, x.OldValue);
            await seals.DeleteTamperRecordsAsync(id);
            bool resealed = false;
            if (!(await CheckAsync(id)).Ok) { await DropAsync(id); await EnsureAsync(id); resealed = true; }
            return new ResetSealResponse(t.Count, resealed);
        });
        return r == null ? ServiceResult<ResetSealResponse>.NotFound("batch not found") : r;
    }

    public async Task<ServiceResult<TamperResponse>> TamperDemoAsync(string? batch)
    {
        var t = await unitOfWork.WriteAsync(async () => (batch == null ? null : await batches.FindIdAsync(batch)) is { } id ? await TamperAsync(id) : null);
        return t == null ? ServiceResult<TamperResponse>.BadRequest("no sealed lab entry to change") : t;
    }

    public Task SealAllAsync() => unitOfWork.WriteAsync(async () =>
    {
        await seals.EnsureTablesAsync();
        if (await seals.GetVersionAsync() != Version)
        {
            logger.LogInformation("seal: chain format v{Version} (deliveries first) — sealing all batches again", Version);
            await seals.ClearAllAsync();
            await seals.SetVersionAsync(Version);
        }
        foreach (var id in await batches.ListIdsAsync()) await EnsureAsync(id);
        return 0;
    });

    /// Entries already in the chain are never rehashed, so a changed row stays detectable. A batch whose sealed rows are all gone (demo reset) starts a fresh chain.
    public async Task EnsureAsync(long batchId)
    {
        var code = (await batches.GetAsync(batchId))!.Code;
        var chain = await seals.ChainAsync(batchId);
        var seal = await seals.GetAsync(batchId);
        if (chain.Count > 0 && (seal?.BatchCode != code || await AllRowsGoneAsync(chain)))
        {
            await seals.DeleteChainAndSealAsync(batchId);
            chain.Clear(); seal = null;
        }
        var have = chain.Select(e => (e.Tbl, e.RowId)).ToHashSet();
        var hashes = chain.Count > 0 ? new HashChain(chain[^1].Hash) : HashChain.Start(code);
        long seq = chain.Count;
        int added = 0;
        foreach (var e in (await seals.CurrentEntriesAsync(batchId)).Where(e => !have.Contains((e.Table, e.RowId))))
        {
            var prev = hashes.Head;
            var h = hashes.Append(e.Table, (await seals.GetRowAsync(e.Table, e.RowId))!);
            await seals.AddEntryAsync(batchId, ++seq, e.Table, e.RowId, prev, h);
            added++;
        }
        if (added == 0 && seal != null) return;
        await seals.SaveAsync(new SealRecord(batchId, code, hashes.Head, key.Sign(hashes.Head), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), seq, key.KeyId));
    }

    public async Task DropAsync(long batchId)
    {
        await seals.EnsureTablesAsync();
        await seals.DeleteChainAndSealAsync(batchId);
        await seals.DeleteTamperRecordsAsync(batchId);
    }

    public async Task<VerifyResultDto> CheckAsync(long batchId)
    {
        var seal = await seals.GetAsync(batchId);
        if (seal == null) return new(false, "not sealed", null, null, null, null, null, 0, 0, false, key.KeyId);
        var chain = await seals.ChainAsync(batchId);
        var hashes = HashChain.Start(seal.BatchCode);
        foreach (var e in chain)
        {
            var row = await seals.GetRowAsync(e.Tbl, e.RowId);
            var what = $"{e.Tbl} #{e.RowId}" + ((row?.GetValueOrDefault("date") ?? row?.GetValueOrDefault("taken_at")) is string d ? $" ({d})" : "");
            if (row == null) return Bad(e.Seq, what + " — deleted");
            var prev = hashes.Head;
            if (hashes.Append(e.Tbl, row) != e.Hash || e.Prev != prev) return Bad(e.Seq, what + " — content changed");
        }
        bool sigOk = CheckSignature(seal.FinalHash, seal.Signature);
        if (hashes.Head != seal.FinalHash || !sigOk) return Bad(chain.Count, sigOk ? "final hash does not match the seal" : "signature invalid", sigOk);
        var have = chain.Select(e => (e.Tbl, e.RowId)).ToHashSet();
        int unsealed = (await seals.CurrentEntriesAsync(batchId)).Count(e => !have.Contains((e.Table, e.RowId)));
        return new(true, "verified", null, null, seal.FinalHash, seal.Signature, seal.SealedAt, chain.Count, unsealed, true, key.KeyId);

        VerifyResultDto Bad(long seq, string what, bool? s = null) =>
            new(false, "tampered", seq, what, seal.FinalHash, seal.Signature, seal.SealedAt, chain.Count, 0, s ?? CheckSignature(seal.FinalHash, seal.Signature), key.KeyId);
    }

    /// Changes a lab temperature (no trigger on lab), so ledger balances stay as they are.
    public async Task<TamperResponse?> TamperAsync(long batchId)
    {
        if (await seals.FirstSealedLabTemperatureAsync(batchId) is not { } e) return null;
        var after = Math.Round(e.TempC + 1.5, 1);
        await seals.SetLabTemperatureAsync(e.LabId, after);
        if (!await seals.TamperRecordedAsync(e.LabId)) await seals.AddTamperRecordAsync(e.LabId, e.TempC, batchId);
        return new TamperResponse(e.Seq, "lab", e.LabId, e.Date, "temp_c", e.TempC, after);
    }

    public bool CheckSignature(string finalHash, string signature) => key.Verify(finalHash, signature);

    async Task<bool> AllRowsGoneAsync(List<SealChainEntry> chain)
    {
        foreach (var e in chain) if (await seals.GetRowAsync(e.Tbl, e.RowId) != null) return false;
        return true;
    }
}
