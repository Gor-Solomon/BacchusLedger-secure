using BacchusLedger.Application.Contracts.Health;

namespace BacchusLedger.Application.Services;

public interface IHealthService
{
    HealthResponse Get();
}
