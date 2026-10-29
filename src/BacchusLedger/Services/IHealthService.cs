using BacchusLedger.Contracts.Health;

namespace BacchusLedger.Services;

public interface IHealthService
{
    HealthResponse Get();
}
