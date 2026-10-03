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
