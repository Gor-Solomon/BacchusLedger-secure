using System.Threading.Channels;

namespace BacchusLedger.Application.Services;

/// Fan-out of live messages to every open SSE stream.
public interface ILiveHub
{
    /// A reader of the SSE messages published from now on; call dispose when the stream closes.
    (ChannelReader<string> Reader, Action Dispose) Subscribe();

    void Publish(string type, object payload);

    /// The payload as it is sent on the stream (snake_case JSON).
    string ToJson(object payload);
}
