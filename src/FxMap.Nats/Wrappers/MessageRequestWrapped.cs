using System.Text;
using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Implementations;

namespace FxMap.Nats.Wrappers;

public class MessageRequestWrapped<TDistributedKey> where TDistributedKey : IDistributedKey
{
    public Dictionary<string, string> Headers { get; set; }
    public MapRequest<TDistributedKey> Query { get; set; }
    public byte[] GetMessageSerialize() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this));

    public RequestContext<TDistributedKey> GetMessageDeserialize(byte[] message)
    {
        var messageData = Encoding.UTF8.GetString(message);
        var messageWrapped = JsonSerializer.Deserialize<MessageRequestWrapped<TDistributedKey>>(messageData);
        return new RequestContextImpl<TDistributedKey>(messageWrapped.Query, messageWrapped.Headers, CancellationToken.None);
    }
}