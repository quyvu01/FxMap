using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Grpc.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserService;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// 5100: plain HTTP for the hand-written endpoints, 5101: HTTP/2 without TLS for gRPC
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenLocalhost(5100, o => o.Protocols = HttpProtocols.Http1);
    k.ListenLocalhost(5101, o => o.Protocols = HttpProtocols.Http2);
});

// An in-memory SQLite database that lives as long as this connection
var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
builder.Services.AddDbContext<UserDb>(o => o.UseSqlite(connection));

builder.Services.AddGrpc();
builder.Services.AddFxMap(cfg => cfg.AddEntitiesFromAssemblyContaining<UserConfig>())
    .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(UserDb)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDb>();
    db.Database.EnsureCreated();
    db.Users.AddRange(UserDb.Seed());
    db.SaveChanges();
}

// Counts every request this service receives (REST and gRPC), so the order service can show how many it made.
// --DelayMs=3000 makes every answer that slow, to see what the order service does with a slow dependency.
var requests = 0;
var delay = app.Configuration.GetValue<int>("DelayMs");
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/stats"))
    {
        Interlocked.Increment(ref requests);
        if (delay > 0) await Task.Delay(delay);
    }
    await next();
});
app.MapGet("/stats", () => new { requests });
app.MapPost("/stats/reset", () => { Interlocked.Exchange(ref requests, 0); return Results.NoContent(); });

// Hand-written: one user
app.MapGet("/users/{id}", async (string id, UserDb db) =>
    await db.Users.FindAsync(id) is { } user ? Results.Ok(user) : Results.NotFound());

// Hand-written: a batch endpoint, /users?ids=u-01,u-02
app.MapGet("/users", async (string ids, UserDb db) =>
{
    var list = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
    return await db.Users.Where(u => list.Contains(u.Id)).ToListAsync();
});

// FxMap
app.MapFxMapperGrpc();

app.Run();
