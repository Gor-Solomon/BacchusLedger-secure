using BacchusLedger.Contracts.Health;

namespace BacchusLedger.Services;

public sealed class HealthService : IHealthService
{
    public HealthResponse Get() => new("ok", Db.Path);
}
