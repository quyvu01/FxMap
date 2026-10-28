# Hand-written vs FxMap

An order service needs the name and email of each order's user, and the user service owns them. This sample fills the
same 100 orders three ways and counts the requests the user service receives:

| Endpoint | How |
|---|---|
| `GET /orders/per-order` | Hand-written: one HTTP call per order |
| `GET /orders/batched` | Hand-written: collect the distinct ids, one batch call, build a lookup, fill |
| `GET /orders/fxmap` | FxMap: the profile says where the fields come from, `MapDataAsync(orders)` fills them over gRPC |

The 100 orders belong to 12 users (`u-01` to `u-12`), and order 100 points to `u-99`, which does not exist.

```csharp
public sealed class OrderResponseProfile : ProfileOf<OrderResponse>
{
    protected override void Configure() =>
        UseDistributedKey("UserOf")
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email");
}
```

## Run

Needs the .NET 10 SDK. Uses ports 5100 and 5101 (user service) and 5200 (order service).

```bash
sample/ManualVsFxMap/run.sh
```

It builds and starts both services, calls each endpoint once to open the connections, then calls them again and prints
the results. Then it stops the user service and calls the two batched versions again.

## What it shows

Measured on one machine (both services on localhost, so the times only compare the approaches with each other):

| | Requests to the user service | Time | Orders filled | Order with a missing user |
|---|---|---|---|---|
| Per order | 100 | ~200 ms | 99 | fields left empty |
| Hand-written batch | 1 | ~3 ms | 99 | fields left empty |
| FxMap | 1 | ~8 ms | 99 | fields left empty |

- FxMap sends one request per distributed key, not per order: the 13 distinct ids of the 100 orders go in one gRPC
  request. A chain of keys (user, then the user's province) is one request per level.
- The hand-written batch is just as good on the wire. What FxMap removes is writing it: the id collection, the batch
  endpoint, the lookup and the filling, for every key in every endpoint.
- The first call after a start opens the gRPC connection; the numbers above are from the second call.

### When the user service is down or slow

Both clients have a 2 s budget (`HttpClient.Timeout` and `cfg.SetRequestTimeOut(TimeSpan.FromSeconds(2))`).

| | Hand-written batch | FxMap (default) | FxMap with `cfg.ThrowIfException()` |
|---|---|---|---|
| User service down | throws `HttpRequestException` | returns the orders, user fields empty | throws `RpcException` (`Unavailable`) |
| User service answers in 3 s | throws `TaskCanceledException` after 2 s | returns after 2 s, user fields empty | throws `RpcException` (`Cancelled`) after 2 s |

By default FxMap treats the remote fields as optional: a failed request leaves them empty and the response still goes
out. Call `cfg.ThrowIfException()` when a missing field must fail the request instead, and `cfg.SetRetryPolicy(...)`
to retry before giving up. To try these yourself:

```bash
dotnet run --project sample/ManualVsFxMap/UserService -- --DelayMs=3000   # every answer takes 3 s
dotnet run --project sample/ManualVsFxMap/OrderService -- --Strict=true   # ThrowIfException
curl localhost:5200/orders/fxmap
```

To see the requests themselves (one span per FxMap request), see the OpenTelemetry setup in
[README-TELEMETRY-DEMO.md](../README-TELEMETRY-DEMO.md).
