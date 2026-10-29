using BacchusLedger.Application.Events;

namespace BacchusLedger.Application.Messaging;

/// The transactional outbox: stores an event in the same database transaction as the business data.
public interface IOutbox
{
    /// Adds the event to outbox_messages inside the running IUnitOfWork.WriteAsync transaction:
    /// if that transaction rolls back, the event is gone too, and the other way round. Throws when no transaction is running.
    Task EnqueueAsync(IOutboxEvent e);
}
