using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace FxMap.Nats.Configuration;

public sealed class FxNatsOpts
{
    private static readonly NatsOpts Defaults = new();

    public string Url { get; set; } = Defaults.Url;
    public string Name { get; set; } = Defaults.Name;
    public string InboxPrefix { get; set; } = Defaults.InboxPrefix;
    public bool Echo { get; set; } = Defaults.Echo;
    public bool Verbose { get; set; } = Defaults.Verbose;
    public bool Headers { get; set; } = Defaults.Headers;
    public bool NoRandomize { get; set; } = Defaults.NoRandomize;
    public bool WaitUntilSent { get; set; } = Defaults.WaitUntilSent;
    public bool UseThreadPoolCallback { get; set; } = Defaults.UseThreadPoolCallback;
    public bool IgnoreAuthErrorAbort { get; set; } = Defaults.IgnoreAuthErrorAbort;
    public int MaxPingOut { get; set; } = Defaults.MaxPingOut;
    public int MaxReconnectRetry { get; set; } = Defaults.MaxReconnectRetry;
    public int ObjectPoolSize { get; set; } = Defaults.ObjectPoolSize;
    public int ReaderBufferSize { get; set; } = Defaults.ReaderBufferSize;
    public int WriterBufferSize { get; set; } = Defaults.WriterBufferSize;
    public int SubPendingChannelCapacity { get; set; } = Defaults.SubPendingChannelCapacity;
    public TimeSpan CommandTimeout { get; set; } = Defaults.CommandTimeout;
    public TimeSpan ConnectTimeout { get; set; } = Defaults.ConnectTimeout;
    public TimeSpan PingInterval { get; set; } = Defaults.PingInterval;
    public TimeSpan ReconnectJitter { get; set; } = Defaults.ReconnectJitter;
    public TimeSpan ReconnectWaitMax { get; set; } = Defaults.ReconnectWaitMax;
    public TimeSpan ReconnectWaitMin { get; set; } = Defaults.ReconnectWaitMin;
    public TimeSpan RequestTimeout { get; set; } = Defaults.RequestTimeout;
    public TimeSpan SubscriptionCleanUpInterval { get; set; } = Defaults.SubscriptionCleanUpInterval;
    public BoundedChannelFullMode SubPendingChannelFullMode { get; set; } = Defaults.SubPendingChannelFullMode;
    public Encoding HeaderEncoding { get; set; } = Defaults.HeaderEncoding;
    public Encoding SubjectEncoding { get; set; } = Defaults.SubjectEncoding;
    public ILoggerFactory LoggerFactory { get; set; } = Defaults.LoggerFactory;
    public INatsSerializerRegistry SerializerRegistry { get; set; } = Defaults.SerializerRegistry;
    public NatsAuthOpts AuthOpts { get; set; } = Defaults.AuthOpts;
    public NatsTlsOpts TlsOpts { get; set; } = Defaults.TlsOpts;
    public NatsWebSocketOpts WebSocketOpts { get; set; } = Defaults.WebSocketOpts;

    internal NatsOpts ToNatsOpts() => new()
    {
        Url = Url,
        Name = Name,
        InboxPrefix = InboxPrefix,
        Echo = Echo,
        Verbose = Verbose,
        Headers = Headers,
        NoRandomize = NoRandomize,
        WaitUntilSent = WaitUntilSent,
        UseThreadPoolCallback = UseThreadPoolCallback,
        IgnoreAuthErrorAbort = IgnoreAuthErrorAbort,
        MaxPingOut = MaxPingOut,
        MaxReconnectRetry = MaxReconnectRetry,
        ObjectPoolSize = ObjectPoolSize,
        ReaderBufferSize = ReaderBufferSize,
        WriterBufferSize = WriterBufferSize,
        SubPendingChannelCapacity = SubPendingChannelCapacity,
        CommandTimeout = CommandTimeout,
        ConnectTimeout = ConnectTimeout,
        PingInterval = PingInterval,
        ReconnectJitter = ReconnectJitter,
        ReconnectWaitMax = ReconnectWaitMax,
        ReconnectWaitMin = ReconnectWaitMin,
        RequestTimeout = RequestTimeout,
        SubscriptionCleanUpInterval = SubscriptionCleanUpInterval,
        SubPendingChannelFullMode = SubPendingChannelFullMode,
        HeaderEncoding = HeaderEncoding,
        SubjectEncoding = SubjectEncoding,
        LoggerFactory = LoggerFactory,
        SerializerRegistry = SerializerRegistry,
        AuthOpts = AuthOpts,
        TlsOpts = TlsOpts,
        WebSocketOpts = WebSocketOpts,
    };
}
