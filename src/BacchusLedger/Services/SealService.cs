using BacchusLedger.Contracts.Seal;
using Microsoft.Data.Sqlite;
using QRCoder;

namespace BacchusLedger.Services;

public sealed class SealService : ISealService
{
    public Task<ServiceResult<SealResponse>> SealAsync(string? batch, string baseUrl)
    {
        var id = Db.Write((c, tx) => { Db.Exec(c, tx, Seal.Tables); var i = BatchId(c, batch); if (i != null) Seal.Ensure(c, tx, i.Value); return i; });
        if (id == null) return Task.FromResult(ServiceResult<SealResponse>.NotFound("batch not found"));
        using var c = Db.Open();
        var s = Db.Query(c, "SELECT * FROM seal_batch WHERE batch_id=$1", id)[0];
        var url = $"{baseUrl}/verify.html?batch={Uri.EscapeDataString(batch!)}";
        using var qr = new QRCodeGenerator();
        var png = new PngByteQRCode(qr.CreateQrCode(url, QRCodeGenerator.ECCLevel.M)).GetGraphic(6);
        var chain = Seal.Entries(c, id.Value).Select(e => new SealEntryDto(e.Seq, e.Tbl, e.RowId, e.Hash, e.Prev)).ToList();
        return Task.FromResult<ServiceResult<SealResponse>>(new SealResponse(batch!, s["final_hash"] as string, s["signature"] as string, s["sealed_at"] as string,
            Db.L(s["entries"]), s["key_id"] as string, "SHA-256 hash chain · ECDSA P-256 (DEMO KEY)", url, chain, "data:image/png;base64," + Convert.ToBase64String(png)));
    }

    public Task<ServiceResult<VerifyResponse>> VerifyAsync(string? batch)
    {
        using var c = Db.Open();
        var id = BatchId(c, batch);
        if (id == null) return Task.FromResult(ServiceResult<VerifyResponse>.NotFound("batch not found"));
        var r = Seal.Verify(c, id.Value);
        var dto = new VerifyResultDto(r.Ok, r.Status, r.TamperedAt, r.TamperedWhat, r.FinalHash, r.Signature, r.SealedAt, r.Entries, r.Unsealed, r.SignatureOk, r.KeyId);
        return Task.FromResult<ServiceResult<VerifyResponse>>(new VerifyResponse(batch, dto, Seal.PublicKeyPem, "DEMO KEY — not for production"));
    }

    public Task<ServiceResult<ResetSealResponse>> ResetDemoAsync(string? batch)
    {
        var r = Db.Write((c, tx) => { Db.Exec(c, tx, Seal.Tables); return BatchId(c, batch) is { } id ? Seal.ResetDemo(c, tx, id) : null; });
        return Task.FromResult(r == null ? ServiceResult<ResetSealResponse>.NotFound("batch not found") : ServiceResult<ResetSealResponse>.Ok(new ResetSealResponse(r.Restored, r.Resealed)));
    }

    public Task<ServiceResult<TamperResponse>> TamperDemoAsync(string? batch)
    {
        var t = Db.Write((c, tx) => BatchId(c, batch) is { } id ? Seal.TamperDemo(c, tx, id) : null);
        return Task.FromResult(t == null ? ServiceResult<TamperResponse>.BadRequest("no sealed lab entry to change")
            : ServiceResult<TamperResponse>.Ok(new TamperResponse(t.Entry, t.Table, t.RowId, t.Date, t.Field, t.Before, t.After)));
    }

    static long? BatchId(SqliteConnection c, string? code) =>
        code == null ? null : Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"] as long?;
}
