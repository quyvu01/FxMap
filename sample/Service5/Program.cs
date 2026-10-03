using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.Nats.Extensions;
using Microsoft.EntityFrameworkCore;
using Service5;
using Service5.Contexts;
using Service5.Data;
using Service5.Responses;

// Service5 has its own PostgreSQL database (patients) and fills their visits from Service4 over NATS.
// The visits key is not unique, so the profiles use Collection(...) to get one element per row.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFxMap(cfg =>
{
    cfg.AddProfilesFromAssemblyContaining<IAssemblyMarker>();
    cfg.AddNats(c => c.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
    cfg.ThrowIfException();
});

builder.Services.AddDbContext<Service5Context>(options =>
    options.UseNpgsql("Host=localhost;Username=postgres;Password=Abcd@2021;Database=FxMapTestService5"));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
    await Service5DataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<Service5Context>());

app.MapGet("/", () => "Service5: /patients, /patients/{mrn}, /item-groups?codes=A,B,C");

// Patients come from Service5's own database; their visits come from Service4.
app.MapGet("/patients", async (Service5Context db, IDistributedMapper mapper) =>
{
    var patients = await db.Patients.AsNoTracking().OrderBy(x => x.Mrn)
        .Select(x => new PatientResponse { Mrn = x.Mrn, Name = x.Name }).ToListAsync();
    await mapper.MapDataAsync(patients);
    return patients;
});

app.MapGet("/patients/{mrn}", async (string mrn, Service5Context db, IDistributedMapper mapper) =>
{
    var patient = await db.Patients.AsNoTracking().Where(x => x.Mrn == mrn)
        .Select(x => new PatientResponse { Mrn = x.Mrn, Name = x.Name }).FirstOrDefaultAsync();
    if (patient is null) return Results.NotFound();
    await mapper.MapDataAsync(patient);
    return Results.Ok(patient);
});

// The scenario with three items: code A once, code B twice. Try /item-groups?codes=A,B,C and /item-groups?codes=B,B
app.MapGet("/item-groups", async (string codes, IDistributedMapper mapper) =>
{
    var groups = (codes ?? "A,B,C").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(code => new ItemGroupResponse { Code = code }).ToList();
    await mapper.MapDataAsync(groups);
    return groups;
});

app.Run();
