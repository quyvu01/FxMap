# Changelog

All notable changes to FxMap are documented here.
Versions follow [Semantic Versioning](https://semver.org/).
Detailed per-release notes live in [`docs/changelogs/`](docs/changelogs/).

---

## [4.1.3]

### Added

- **A request no longer fails as a whole because of one expression that cannot be built.** The data provider of a service that
  owns data can be told to isolate them: `IsolateExpressionErrors()` in `AddEntityFrameworkCore(c => ...)` or `AddMongoDb(c => ...)`.
  It answers the expressions it can build and reports the others once, whatever the number of rows, in the new
  `ItemsResponse.Errors` (`ExpressionError(Expression, Message)`, e.g. `Expression 'Orders:summ(Total)' failed to build: Unknown
  function 'summ'`). The expressions that failed have no value, and the errors are kept even when no row matched. The service that
  asked decides: with `ThrowIfException()` it throws a `DistributedMapException.ExpressionsFailed` that holds all the errors;
  otherwise it logs a warning for each one and leaves the properties of these expressions alone. Without `IsolateExpressionErrors()`
  nothing changes (the request fails). The errors travel over every transport (JSON, and `ExpressionErrorGrpc` in the gRPC contract;
  a version that does not know them ignores them). An error raised by the database when the query runs (a LINQ expression that EF
  Core cannot translate) still fails the request. A data provider that builds its own projection can override
  `QueryHandlerBuilder.BuildProjectionWithErrors`.
- **gRPC:** `AddGrpcHosts` also takes a list of hosts (`IEnumerable<string>`, or an array), for the hosts read from a configuration.

### Fixed

- **RabbitMQ: under load a request failed with `JsonException: '0x01' is an invalid start of a value`.** The reply consumer gave the
  waiting request the delivery of the message (`BasicDeliverEventArgs`) and the request read its body later, but the memory of
  `ea.Body` belongs to the consumer and is reused once its handler returns: the request read the bytes of another message. The body is
  copied in the handler. A load test on the samples (3,000 requests, 64 at a time) failed for two requests out of three before the fix.

### Changed

- **The README of each NuGet package is Markdown only.** NuGet.org does not render the HTML of the GitHub header, so the page of a
  package showed `<p align="center">` as text. Each package now ships a `NUGET.md` (packed as its `README.md`): the header is in
  Markdown, the links and images are absolute, the GitHub alerts are block quotes, and there is no logo, no Discord invitation and no
  badge for what the NuGet page shows itself (version, downloads, license, frameworks, build). The README of GitHub is unchanged.

### Removed

- `FxMap.Expressions.DistributedMapExpression<TModel>` and `FxMapExpression`, public helpers that parsed an expression string into a
  LINQ lambda. Nothing in FxMap used them.

Details: [the notes of 4.1.3](docs/changelogs/v4.1.3.md).

---

## [4.1.2]

### Fixed

- **MongoDB: a `Guid` id was never answered.** The id of the response was `BsonValue.ToString()` of the stored id
  (`UuidStandard:0x0a1b...`), which the caller (and `RequestedIdAnswers`) could not match, so the rows were dropped.
- **MongoDB: values were returned as extended JSON** (`{ "$numberDecimal": "4350.00" }` for `Orders:sum(Total)`, `{ "$oid": ... }`,
  `{ "$date": ... }`), which cannot be deserialized into a `decimal`, a `string` or a `DateTime`. They are now plain JSON
  (numbers, strings, ISO dates, arrays, objects). Values that were already plain (strings, ints, booleans, doubles) are unchanged.
- **RabbitMQ: the headers of the context reached the owner as `"System.Byte[]"`.** RabbitMQ delivers string headers as UTF-8
  bytes and the server called `ToString()` on the array, so a header such as a tenant id was lost on this transport.
- **gRPC: `ThrowIfException()` had no effect on the clients when it was called after `AddGrpcClients(...)`** (the order of the
  samples): the flag was copied when the clients were registered. An unreachable owner was then answered with no rows and no
  error. It is read when a request is made.
- **Kafka: the consumer of the owner was disposed while its consume loop was still running, which crashed the whole process**
  (librdkafka) every time a service using Kafka stopped. The server now waits for the loop before it closes the consumer.
- **Kafka: the headers of the context were never sent.** Only the trace headers went with the request, so the owner never
  saw a tenant id or any other header. They are sent now.
- **Kafka: a reply could be lost, and the request then timed out.** The reply consumer started at `Latest`, but a consumer is
  assigned its partition a few seconds after it subscribes, so a reply sent by a fast owner in the meantime was skipped. The reply
  topic belongs to the client alone: it is read from the start.
- **Kafka: disposing a client waited 10 seconds and then threw.** Nothing cancelled its consume loop, and a task that had not
  completed was disposed.

Details: [the notes of 4.1.2](docs/changelogs/v4.1.2.md).

### Added

- Transport tests (`TransportTests`): an owner and a consumer in one process that only talk through the real transport, for gRPC
  (in process), NATS, RabbitMQ and SQS (skipped unless `FXMAP_TEST_NATS`, `FXMAP_TEST_RABBITMQ`, `FXMAP_TEST_SQS` are set),
  and a `transport-tests` job in the workflow with the brokers as containers.
- Transport tests for Kafka (broker as a service container) and Azure Service Bus (on its emulator, in a job of its own that does
  not block the publish yet). `test/servicebus-emulator` has the compose file and the queues it declares.
- Provider tests against real MongoDB and PostgreSQL (`FXMAP_TEST_MONGO`, `FXMAP_TEST_POSTGRES`; skipped when not set) and a
  `provider-tests` job in the workflow. The Expressions integration tests (`IntegrationTests.Expressions`) now run in CI too:
  no workflow step matched their namespace before.

### Known limits

- MongoDB: `Id(x => x.Mrn + x.Code)` (a computed id) throws `NotSupportedException` when the first request arrives, not when
  the service starts. Only a property can be the `Id` and the `DefaultProperty` of a MongoDB entity.

---

## [4.1.1]

### Changed

- **HotChocolate: conditional expressions receive `RequestServices`** (the scope of the request) instead of the scope of the
  field, so scoped services filled by your middleware for the request are visible. The fields of a query run in parallel on
  that scope: do not use a scoped `DbContext` directly in a condition. See [the notes](docs/changelogs/v4.1.1.md).

---

## [4.1.0]

### Added

- **Computed keys in profiles** — `Of(...)` accepts an expression over the DTO, the counterpart of a computed entity `Id`:
  `UseDistributedKey<UserDistributedKey>().Of(x => x.Id + x.Email)`. The value is read with `ToString()`; an expression that
  goes through a null has no key. A key that reads a property mapped by another rule is resolved after that rule; reading a
  property of its own group is not a dependency. `Of` throws `InvalidProfileSelector` when the expression does not read the
  DTO. HotChocolate reads the key of a rule through its accessor, so computed keys work there too.

### Notes

- **Conditional expressions run concurrently** (and may run once for each mapped object) on the `IServiceProvider` of the
  mapper; FxMap does not serialize them. Do not use a scoped `DbContext` directly in a condition: use
  `IDbContextFactory<T>` or your own scope. With HotChocolate the provider is the scope of the field. See
  [the notes](docs/changelogs/v4.1.0.md#things-to-know-about-conditional-expressions).

### Removed

- `IPropertyAccessor.PropertyInfo` (and `PropertyAccessor<T,TProp>.PropertyInfo`): the key of a rule can now be an expression, which has no
  property. Nothing in FxMap read it; code that implements or reads `IPropertyAccessor` must drop it.

### Fixed

- A rule that overwrote a property read by the key of another rule of the same batch (`Of(x => x.Id)` with
  `For(x => x.Id, ...)`) could make that other rule miss its row, because keys were read while values were being written.
  Keys are now read before any value is written.

---

## [4.0.0]

**Breaking release.** See the [full notes and migration guide](docs/changelogs/v4.0.0.md). 3.0.1 was never released.

### Breaking Changes

- **Entity configurations are validated when configured** — `EntityConfigureOf<T>` must call `Id(...)` exactly once, `Id` /
  `DefaultProperty` can be declared once, and their selector must read the entity (`EntityIdNotConfigured`,
  `InvalidEntitySelector`). Selectors may now be any expression over the entity (`Id(x => x.Code + ":" + x.Site)`).
- **Internal entity configurations are registered by assembly scans** (`DefinedTypes` instead of `ExportedTypes`).
- **`IDistributedMapper`** gains `MapDataAsync(value, IContext, token)`; implementers must add it.
- **Data provider API:** `IIdConverter` (non-generic) removed and `IIdConverter<TId>.ConvertIds` returns `List<TId>`;
  `MapEntityConfig` -> `IMapEntityConfig` with `IdPropertySelector` / `DefaultPropertySelector`; `QueryHandlerBuilder` is
  a contract implemented by `QueryHandlerBuilder<TModel, TId, TKey>`; `FilterContext` removed; `ProjectionBuilder` takes
  selectors; `RequestedIdAnswers.Align` is generic.

### Added

- `MapDataAsync(value, context, token)`: the caller's token and the token of the context are linked, and the headers of the
  context are sent with every request.

---

## [3.0.0]

**Breaking release.** See the [full notes and migration guide](docs/changelogs/v3.0.0.md).

### Breaking Changes

- **Pipeline behaviors take a named delegate** — `ISendPipelineBehavior<T>.HandleAsync(context, SendHandlerDelegate next)` and
  `IReceivedPipelineBehavior<T>.HandleAsync(context, ReceivedHandlerDelegate next)` replace
  `Func<Task<ItemsResponse<DataResponse>>> next`. Add `using FxMap.Delegates;` and change the type of `next`.
- **`SingleFlightBehavior<T>` and `SingleFlightGroup<TValue>` removed.** They were never registered by default.

> Releases between 2.0.0 and 3.0.0 are described on the [GitHub releases page](https://github.com/quyvu01/FxMap/releases) where one was published.

---

## [2.0.0] - 2026-04-05

**Breaking release.** Upgrade from 1.x requires code changes — see the [full notes](docs/changelogs/v2.0.0.md).

### Breaking Changes

- **`UseDistributedKey` string overload** — second `namespace` parameter is now required.
  `UseDistributedKey<TKey>("Key")` → `UseDistributedKey("Key", "Namespace")`.
  Both values are validated by regex at startup.
- **`AddHandlersFromNamespaceContaining` removed** from `MapConfigurator`.
  Pipeline behaviors are the only handler registration path.
- **gRPC extension renamed** — `MapFxMapGrpcService()` → `MapFxMapperGrpc()`.
- **gRPC delegate renamed** — `GetFxMapResponseFunc` → `GetMapperResponseFunc`.
- **`GetAssemblyName()` extension removed** — use standard `AssemblyQualifiedName`.
- **Removed helpers** — `IsInterfaceOrConcreteClass`, `IsFSharpType`, `MessageRequestWrapped.Subject`.

### New

- **Namespace-scoped string keys** — dynamic types scoped to `{namespace}.{key}` preventing
  cross-service name collisions. `IFluentEntityConfig` gains `DistributedNamespace`.
- **Centralized type lookup** — `DistributedTypeData` record and
  `ConfigurationExtensions.GetDistributedTypeData` with `ConcurrentDictionary` cache replace
  per-transport `Type.GetType()` lookups. Required because dynamic types cannot be re-resolved
  by name at runtime.
- **`DistributedMapException`** — typed exception for catching mapping-specific runtime failures.

### Improvements

- gRPC discovery and dispatch pipeline is now fully string-based (`AssemblyQualifiedName`),
  making it compatible with dynamically-emitted types.
- `SqsRequestClient`, `NatsRequestClient`, `RabbitMqRequestClient` migrated to C# primary constructors.
- Sample services wired with gRPC HTTP/2 endpoints (`MapFxMapperGrpc`) as the default transport.

---

## [1.0.3] - 2026-03-15

No breaking changes. Internal improvements only — consuming projects require no code changes.

### Improvements

- **DI-first transport configuration** — all five transport packages replace internal `static`
  configuration classes (`NatsStatics`, `AzureServiceBusStatic`, `KafkaStatics`,
  `RabbitMqStatics`, `SqsStatics`) with properly registered singleton interfaces:
  `INatsConfiguration`, `IAzureServiceBusConfiguration`, `IKafkaConfiguration`,
  `IRabbitMqConfiguration`, `ISqsConfiguration`. All `Statics/` folders removed.
- **Subject / queue name resolution** moved from standalone `Extensions.cs` helpers into the
  configuration interfaces (`GetSubject`, `GetRequestQueue`, `GetReplyQueue`, `GetRequestTopic`,
  `GetQueueName`).
- **`FxMapStatics` eliminated** — `GetProfileConfig` / `GetEntityConfig` delegates are now
  registered as singletons in `AddFxMap()`.
- **EF Core exception fix** — missing entity config now throws
  `FxMapEntityFrameworkException.EntityConfigsMustNotBeEmpty` instead of a misleading
  `DistributedMapException.AddProfilesFromAssemblyContaining`.

---

## [1.0.2] - 2026-03-07

### Improvements

- Renamed all legacy "attribute" terminology to "distributedKey" across the entire codebase
  (exceptions, telemetry constants, proto fields, transport servers, core pipeline,
  data provider extensions). No behavioral changes.
- `PropertyInformation` renamed to `Property` for brevity; expression resolution
  parallelized with `Task.WhenAll`.

---

## [1.0.1] - 2026-02-XX

- Initial public release under the FxMap name.
- Logo and branding updated.

---

## [1.0.0] - 2026-02-XX

- First stable release. FluentAPI-based distributed data mapping across NATS, gRPC,
  RabbitMQ, Kafka, AWS SQS, and Azure Service Bus transports.
- EF Core and MongoDB data providers.
- HotChocolate GraphQL integration.
- Roslyn Analyzers (`FxMap.Analyzers`) for compile-time profile/entity validation.

---

For older OfX / pre-1.0 history see [`docs/changelogs/`](docs/changelogs/).
