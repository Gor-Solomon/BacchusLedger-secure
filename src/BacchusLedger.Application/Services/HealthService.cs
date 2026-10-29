using BacchusLedger.Application.Contracts.Health;
using BacchusLedger.Application.Options;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Application.Services;

public sealed class HealthService(IOptions<StorageOptions> storage) : IHealthService
{
    public HealthResponse Get() => new("ok", storage.Value.DbPath);
}
