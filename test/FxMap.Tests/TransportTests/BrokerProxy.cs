using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// A TCP forwarder between a service and its broker, to simulate an outage without touching the broker: <see cref="Cut"/>
/// closes every connection and refuses new ones, <see cref="Restore"/> accepts again on the same port.
/// </summary>
public sealed class BrokerProxy : IAsyncDisposable
{
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly ConcurrentDictionary<TcpClient, byte> _connections = new();
    private TcpListener _listener;
    private CancellationTokenSource _cts = new();

    public int Port { get; private set; }

    private BrokerProxy(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
    }

    public static BrokerProxy To(string host, int port)
    {
        var proxy = new BrokerProxy(host, port);
        proxy.Listen(0);
        return proxy;
    }

    private void Listen(int port)
    {
        _cts = new CancellationTokenSource();
        for (var attempt = 0;; attempt++)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                break;
            }
            catch (SocketException) when (attempt < 50)
            {
                Thread.Sleep(100); // the port is not released yet
            }
        }

        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync(_listener, _cts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch
            {
                return;
            }

            _ = ForwardAsync(client, ct);
        }
    }

    private async Task ForwardAsync(TcpClient client, CancellationToken ct)
    {
        var upstream = new TcpClient();
        try
        {
            await upstream.ConnectAsync(_targetHost, _targetPort, ct);
            _connections[client] = 0;
            _connections[upstream] = 0;
            await Task.WhenAny(Pump(client, upstream, ct), Pump(upstream, client, ct));
        }
        catch
        {
            // The connection is gone (cut, or closed by one side)
        }
        finally
        {
            _connections.TryRemove(client, out _);
            _connections.TryRemove(upstream, out _);
            client.Dispose();
            upstream.Dispose();
        }
    }

    private static async Task Pump(TcpClient from, TcpClient to, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var source = from.GetStream();
        var target = to.GetStream();
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0) await target.WriteAsync(buffer.AsMemory(0, read), ct);
    }

    /// <summary>The broker disappears: the connections are closed and nobody can connect.</summary>
    public void Cut()
    {
        _cts.Cancel();
        _listener.Stop();
        foreach (var connection in _connections.Keys) connection.Dispose();
        _connections.Clear();
    }

    /// <summary>The broker is back, on the same port.</summary>
    public void Restore() => Listen(Port);

    public ValueTask DisposeAsync()
    {
        Cut();
        return ValueTask.CompletedTask;
    }
}
