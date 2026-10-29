using BacchusLedger.Application.Contracts.Cellar;

namespace BacchusLedger.Application.Services;

public interface ICellarService
{
    /// Litres per vessel on a date (default today), replaying the real (non-copy) batches.
    Task<CellarResponse> GetAsync(string? date);
}
