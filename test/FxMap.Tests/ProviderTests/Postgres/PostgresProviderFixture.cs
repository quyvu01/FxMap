using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Models;
using FxMap.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FxMap.Tests.ProviderTests.Postgres;

/// <summary>
/// A real PostgreSQL (<c>FXMAP_TEST_POSTGRES</c>, a connection string without a database) with its own database for
/// the test class, seeded with the provider rows, behind the real registration
/// (<c>AddFxMap(...).AddEntityFrameworkCore(...)</c>).
/// </summary>
public sealed class PostgresProviderFixture : IProviderRunner, IDisposable
{
    private readonly Lazy<State> _state = new(() => new State());

    public Task<ItemsResponse<DataResponse>> RunAsync<TKey>(string[] ids, params string[] expressions)
        where TKey : IDistributedKey => _state.Value.RunAsync<TKey>(ids, expressions);

    public Task MapAsync(object value) => _state.Value.MapAsync(value);

    public void Dispose()
    {
        if (_state.IsValueCreated) _state.Value.Dispose();
    }

    private sealed class State : IDisposable
    {
        private readonly string _server = ProviderEnvironment.Get(ProviderEnvironment.Postgres);
        private readonly string _database = $"fxmap_test_{Guid.NewGuid():N}";
        private readonly ServiceProvider _provider;

        public State()
        {
            using (var connection = new NpgsqlConnection(_server))
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText = $"CREATE DATABASE \"{_database}\"";
                create.ExecuteNonQuery();
            }

            var connectionString = new NpgsqlConnectionStringBuilder(_server) { Database = _database }.ToString();
            var services = new ServiceCollection();
            services.AddDbContext<ProviderDbContext>(o => o.UseNpgsql(connectionString));
            services.AddFxMap(cfg =>
                {
                    cfg.AddEntityConfigs(typeof(ProviderCustomerConfig), typeof(ProviderDeviceConfig),
                        typeof(ProviderTicketConfig), typeof(ProviderLabConfig));
                    cfg.AddProfileConfigs(typeof(ProviderMappingViewProfile), typeof(ProviderLabViewProfile));
                    cfg.ThrowIfException();
                })
                .AddEntityFrameworkCore(c => c.AddDbContexts(typeof(ProviderDbContext)));
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProviderDbContext>();
            db.Database.EnsureCreated();
            db.Customers.AddRange(ProviderData.Customers());
            db.Devices.Add(ProviderData.Device());
            db.Tickets.Add(ProviderData.Ticket());
            db.Labs.AddRange(ProviderData.Labs());
            db.SaveChanges();
        }

        public async Task<ItemsResponse<DataResponse>> RunAsync<TKey>(string[] ids, string[] expressions)
            where TKey : IDistributedKey
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IDistributedMapper>()
                .FetchDataAsync<TKey>(new DistributedMapRequest(ids, expressions));
        }

        public async Task MapAsync(object value)
        {
            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(value);
        }

        public void Dispose()
        {
            _provider.Dispose();
            NpgsqlConnection.ClearAllPools();
            using var connection = new NpgsqlConnection(_server);
            connection.Open();
            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)";
            drop.ExecuteNonQuery();
        }
    }
}
