namespace BacchusLedger.Application.Repositories;

/// Progress of the demo players and the rows they wrote.
public interface IDemoRepository
{
    Task<HashSet<long>> DoneStepsAsync(string script);
    Task<bool> IsDoneAsync(string script, int step);
    Task MarkDoneAsync(string script, int step);
    Task TrackAsync(string script, string table, long id);
    Task<List<long>> TrackedAsync(string script, string table);
    Task ForgetAsync(string script);
}
