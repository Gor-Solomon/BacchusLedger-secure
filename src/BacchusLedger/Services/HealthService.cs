using BacchusLedger.Contracts.Health;
using BacchusLedger.Options;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Services;

public sealed class HealthService(IOptions<StorageOptions> storage) : IHealthService
{
    public HealthResponse Get() => new("ok", storage.Value.DbPath);
}
