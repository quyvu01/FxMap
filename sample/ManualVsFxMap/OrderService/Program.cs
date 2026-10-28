using System.Diagnostics;
using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.Grpc.Extensions;
using OrderService;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://localhost:5200");

const string userRest = "http://localhost:5100";
builder.Services.AddHttpClient("users", c =>
{
    c.BaseAddress = new Uri(userRest);
    c.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddFxMap(cfg =>
{
    cfg.AddProfilesFromAssemblyContaining<OrderResponseProfile>();
    cfg.AddGrpcClients(c => c.AddGrpcHosts("http://localhost:5101"));
    // The same 2 s budget as the hand-written HttpClient above
    cfg.SetRequestTimeOut(TimeSpan.FromSeconds(2));
    // By default a failed request leaves the fields empty; --Strict=true makes MapDataAsync throw instead
    if (builder.Configuration.GetValue<bool>("Strict")) cfg.ThrowIfException();
});

var app = builder.Build();

// 1. Hand-written, one call per order
app.MapGet("/orders/per-order", (IHttpClientFactory http) => Measure(http, async () =>
{
    var client = http.CreateClient("users");
    var orders = Orders.Load();
    foreach (var order in orders)
    {
        var response = await client.GetAsync($"/users/{order.UserId}");
        if (!response.IsSuccessStatusCode) continue; // missing user: leave the fields empty
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        order.UserName = user!.Name;
        order.UserEmail = user.Email;
    }
    return orders;
}));

// 2. Hand-written, batched: collect the distinct ids, one call, build a lookup, fill
app.MapGet("/orders/batched", (IHttpClientFactory http) => Measure(http, async () =>
{
    var client = http.CreateClient("users");
    var orders = Orders.Load();
    var ids = orders.Select(o => o.UserId).Distinct();
    var users = await client.GetFromJsonAsync<List<UserDto>>($"/users?ids={string.Join(',', ids)}");
    var byId = users!.ToDictionary(u => u.Id);
    foreach (var order in orders)
    {
        if (!byId.TryGetValue(order.UserId, out var user)) continue;
        order.UserName = user.Name;
        order.UserEmail = user.Email;
    }
    return orders;
}));

// 3. FxMap: the profile says where the fields come from
app.MapGet("/orders/fxmap", (IHttpClientFactory http, IDistributedMapper mapper) => Measure(http, async () =>
{
    var orders = Orders.Load();
    await mapper.MapDataAsync(orders);
    return orders;
}));

app.Run();

// Resets the user service's request counter, runs the approach, and reports how many requests it took.
// If the approach throws (the user service is down, for example), the error and the time it took are reported instead.
static async Task<IResult> Measure(IHttpClientFactory http, Func<Task<List<OrderResponse>>> run)
{
    var client = http.CreateClient("users");
    try { await client.PostAsync("/stats/reset", null); }
    catch (HttpRequestException) { /* the user service is down: nothing to count */ }

    var watch = Stopwatch.StartNew();
    List<OrderResponse> orders;
    try
    {
        orders = await run();
    }
    catch (Exception e)
    {
        return Results.Json(new { error = $"{e.GetType().Name}: {e.Message.Split(", DebugException")[0]}", elapsedMs = watch.ElapsedMilliseconds },
            statusCode: StatusCodes.Status502BadGateway);
    }
    watch.Stop();

    Stats? stats = null;
    try { stats = await client.GetFromJsonAsync<Stats>("/stats"); }
    catch (HttpRequestException) { /* the user service is down */ }
    return Results.Ok(new
    {
        orders = orders.Count,
        distinctUsers = orders.Select(o => o.UserId).Distinct().Count(),
        requestsToUserService = stats?.Requests,
        elapsedMs = watch.ElapsedMilliseconds,
        filled = orders.Count(o => o.UserName is not null),
        first = orders[0],
        missingUser = orders[^1]
    });
}

internal sealed record Stats(int Requests);
