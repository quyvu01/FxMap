using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Nats.Extensions;
using Microsoft.EntityFrameworkCore;
using Service4;
using Service4.Contexts;
using Service4.Data;

// Service4 owns data looked up by a NON-unique key: items by Code and visits by Mrn.
// It answers FxMap requests over NATS straight from PostgreSQL through EF Core.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<IAssemblyMarker>();
        cfg.AddNats(c => c.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
        cfg.ThrowIfException();
    })
    .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(Service4Context)));

builder.Services.AddDbContextPool<Service4Context>(options =>
    options.UseNpgsql("Host=localhost;Username=postgres;Password=Abcd@2021;Database=FxMapTestService4"), 128);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
    await Service4DataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<Service4Context>());

// Raw rows, to compare with what the other services receive through FxMap.
app.MapGet("/", () => "Service4: items by Code and visits by Mrn. See /items and /visits/{mrn}");
app.MapGet("/items", async (Service4Context db) => await db.Items.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
app.MapGet("/visits", async (Service4Context db) =>
    await db.Visits.AsNoTracking().OrderBy(x => x.Mrn).ThenBy(x => x.Id).ToListAsync());
app.MapGet("/visits/{mrn}", async (string mrn, Service4Context db) =>
    await db.Visits.AsNoTracking().Where(x => x.Mrn == mrn).OrderBy(x => x.Id).ToListAsync());

app.Run();
