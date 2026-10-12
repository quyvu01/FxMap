using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.MongoDb.Extensions;
using FxMap.Models;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace FxMap.Tests.ProviderTests.Mongo;

/// <summary>
/// A real MongoDB (<c>FXMAP_TEST_MONGO</c>) with its own database for the test class, seeded with the provider rows,
/// behind the real FxMap registration (<c>AddFxMap(...).AddMongoDb(...)</c>).
/// </summary>
public sealed class MongoProviderFixture : IProviderRunner, IDisposable
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
        private readonly MongoClient _client;
        private readonly string _database;
        private readonly ServiceProvider _provider;

        static State()
        {
            // The driver needs the Guid representation to be chosen once for the process.
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // already registered
            }
        }

        public State()
        {
            _client = new MongoClient(ProviderEnvironment.Get(ProviderEnvironment.Mongo));
            _database = $"fxmap_test_{Guid.NewGuid():N}";
            var db = _client.GetDatabase(_database);

            var customers = db.GetCollection<ProviderCustomer>("customers");
            var devices = db.GetCollection<ProviderDevice>("devices");
            var tickets = db.GetCollection<ProviderTicket>("tickets");
            var labs = db.GetCollection<ProviderLab>("labs");
            customers.InsertMany(ProviderData.Customers());
            devices.InsertOne(ProviderData.Device());
            tickets.InsertOne(ProviderData.Ticket());
            labs.InsertMany(ProviderData.Labs());

            var services = new ServiceCollection();
            services.AddFxMap(cfg =>
                {
                    cfg.AddEntityConfigs(typeof(ProviderCustomerConfig), typeof(ProviderDeviceConfig),
                        typeof(ProviderTicketConfig), typeof(ProviderLabConfig));
                    cfg.AddProfileConfigs(typeof(ProviderMappingViewProfile), typeof(ProviderLabViewProfile),
                        typeof(ProviderBrokenExpressionViewProfile));
                    cfg.ThrowIfException();
                })
                .AddMongoDb(c =>
                {
                    c.AddCollection(customers);
                    c.AddCollection(devices);
                    c.AddCollection(tickets);
                    c.AddCollection(labs);
                    c.IsolateExpressionErrors();
                });
            _provider = services.BuildServiceProvider();
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
            _client.DropDatabase(_database);
        }
    }
}
