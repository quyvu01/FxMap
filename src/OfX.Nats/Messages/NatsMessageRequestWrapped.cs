using System.Text;
using System.Text.Json;
using OfX.Abstractions;
using OfX.Extensions;
using OfX.Implementations;

namespace OfX.Nats.Messages;

public class NatsMessageRequestWrapped<TDistributedKey> where TDistributedKey : IDistributedKey
{
    public Dictionary<string, string> Headers { get; set; }
    public OfXQueryRequest<TDistributedKey> Query { get; set; }
    public byte[] GetMessageSerialize() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this));

    public RequestContext<TDistributedKey> GetMessageDeserialize(byte[] message)
    {
        var messageData = Encoding.UTF8.GetString(message);
        var messageWrapped = JsonSerializer.Deserialize<NatsMessageRequestWrapped<TDistributedKey>>(messageData);
        return new RequestContextImpl<TDistributedKey>(messageWrapped.Query, messageWrapped.Headers, CancellationToken.None);
    }

    public string Subject => typeof(TDistributedKey).GetAssemblyName();
}