using BacchusLedger.Contracts.Assistant;

namespace BacchusLedger.Services.Assistant;

/// The 5 quick questions, answered by our own code (no AI, offline, read-only).
public interface IQuickAnswerService
{
    Task<AssistantAnswer> AnswerAsync(int n, string lang, IReadOnlyDictionary<string, string> settings);

    /// W01-B1-2025 loss per step, total, remainder and value (null when the batch is missing).
    Task<W01Loss?> W01LossesAsync(IReadOnlyDictionary<string, string> settings);

    /// Stages above the normal loss, real batches only (demo replays and practice copies are left out).
    Task<List<AboveNormalFlag>> AboveNormalAsync(IReadOnlyDictionary<string, string> settings);
}
