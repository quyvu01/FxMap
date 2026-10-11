# Contributing to FxMap

Thanks for helping make FxMap better. Bug reports, questions, docs fixes and code are all welcome.

## Ask a question or share an idea

- **Discord:** https://discord.gg/XJNzPbqg7 is the fastest place to get help.
- **GitHub Discussions:** for longer questions and ideas.
- **GitHub Issues:** for bugs and concrete feature requests (please use the templates).

## Report a bug

Open an issue with the bug template. A good report has the FxMap version (all `FxMap.*` packages must share one
version), the .NET version, the transport and data provider you use, and a minimal profile / entity config that
reproduces it.

## Send a pull request

1. Fork the repository and create a branch from `main`.
2. Build and run the tests:

   ```bash
   dotnet restore
   dotnet build --no-restore
   dotnet test test/FxMap.Tests/FxMap.Tests.csproj
   dotnet test test/FxMap.Analyzers.Tests/FxMap.Analyzers.Tests.csproj
   ```

   The provider tests (`test/FxMap.Tests/ProviderTests`) run the MongoDB and EF Core providers against real
   databases and are **skipped** unless you point them to one:

   ```bash
   export FXMAP_TEST_MONGO="mongodb://localhost:27017"
   export FXMAP_TEST_POSTGRES="Host=localhost;Username=postgres;Password=<password>"   # no database: each run creates its own
   dotnet test test/FxMap.Tests/FxMap.Tests.csproj --filter "FullyQualifiedName~ProviderTests"
   ```

   The transport tests (`test/FxMap.Tests/TransportTests`) run two services in one process, an owner of the data and a
   consumer, that only talk through the real transport. gRPC needs nothing (the owner is a Kestrel server on a free port);
   the others are skipped unless you point them to a broker:

   ```bash
   export FXMAP_TEST_NATS="nats://localhost:4222"
   export FXMAP_TEST_RABBITMQ="localhost"              # host[:amqpPort[:managementPort]], user and password guest
   export FXMAP_TEST_SQS="http://localhost:4566"        # LocalStack with SQS
   dotnet test test/FxMap.Tests/FxMap.Tests.csproj --filter "FullyQualifiedName~TransportTests"
   ```

   Any MongoDB and PostgreSQL will do, for example
   `docker run -d -p 27017:27017 mongo:7` and `docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:17`
   (or `podman run ...`). CI runs them with service containers.

3. Add or update tests for what you change. Tests use xUnit and Shouldly and live in
   `test/FxMap.Tests` (`UnitTests`, `ContractTests`, `IntegrationTests`).
4. Keep the change focused and describe the why in the pull request.

Look for issues labelled **good first issue** if you want somewhere to start.

## Documentation

The website lives in a separate repository (https://github.com/quyvu01/fxmapper). Fix a page there, or open an issue
here if you are not sure where a change belongs.

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By taking part you agree to uphold it.

## License

By contributing you agree that your contributions are licensed under the Apache License 2.0.
