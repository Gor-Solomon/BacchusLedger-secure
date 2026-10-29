using System.Text.Json;
using System.Threading.Channels;
using BacchusLedger.Application.Services;

namespace BacchusLedger.Infrastructure.Live;

public sealed class LiveHub : ILiveHub
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    readonly object gate = new();
    readonly List<Channel<string>> subs = [];

    public (ChannelReader<string> Reader, Action Dispose) Subscribe()
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (gate) subs.Add(ch);
        return (ch.Reader, () => { lock (gate) subs.Remove(ch); ch.Writer.TryComplete(); });
    }

    public void Publish(string type, object payload)
    {
        var msg = $"event: {type}\ndata: {ToJson(payload)}\n\n";
        lock (gate) foreach (var s in subs) s.Writer.TryWrite(msg);
    }

    public string ToJson(object payload) => JsonSerializer.Serialize(payload, Json);
}
