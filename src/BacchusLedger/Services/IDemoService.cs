using BacchusLedger.Contracts.Demo;

namespace BacchusLedger.Services;

/// The guided demo players. Each step really writes its data into the database.
public interface IDemoService
{
    Task<DemoStepsDto> StepsAsync(string? script);

    /// Writes every step up to n (once) and returns the card of step n.
    Task<ServiceResult<DemoCardDto>> StepAsync(int n, string? script);

    /// Re-creates the demo batch from seed (the only place where demo rows are removed).
    Task<DemoStepsDto> ResetAsync(string? script);

    /// Selftest only: applies every step not written yet, inside the running (rolled-back) transaction.
    Task ReplayAllAsync(string script);

    /// "Try: W03 filtration with dip 35.0 cm": a fresh copy of W03 batch 3 up to the stock check.
    Task<PracticeCopyResponse> CreatePracticeCopyAsync();

    /// Third player: a clean live-demo batch on F-2 (or a new empty qvevri). The presenter types every value.
    Task<LiveDemoStartResponse> StartLiveReadingsAsync(string? vessel);
}
