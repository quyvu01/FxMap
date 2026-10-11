using Xunit;

namespace FxMap.Tests.ProviderTests;

/// <summary>Names of the environment variables that point the provider tests to a real database.</summary>
public static class ProviderEnvironment
{
    /// <summary>MongoDB connection string, e.g. <c>mongodb://localhost:27017</c>.</summary>
    public const string Mongo = "FXMAP_TEST_MONGO";

    /// <summary>PostgreSQL connection string without a database, e.g. <c>Host=localhost;Username=postgres;Password=...</c>.</summary>
    public const string Postgres = "FXMAP_TEST_POSTGRES";

    public static string Get(string variable) => Environment.GetEnvironmentVariable(variable);

    internal static string SkipReason(string variable) => string.IsNullOrWhiteSpace(Get(variable))
        ? $"Set {variable} to run the tests of this provider against a real database."
        : null;
}

/// <summary>A fact that is skipped (not failed) when MongoDB is not configured.</summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute() => Skip = ProviderEnvironment.SkipReason(ProviderEnvironment.Mongo);
}

/// <summary>A theory that is skipped (not failed) when MongoDB is not configured.</summary>
public sealed class MongoTheoryAttribute : TheoryAttribute
{
    public MongoTheoryAttribute() => Skip = ProviderEnvironment.SkipReason(ProviderEnvironment.Mongo);
}

/// <summary>A fact that is skipped (not failed) when PostgreSQL is not configured.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() => Skip = ProviderEnvironment.SkipReason(ProviderEnvironment.Postgres);
}

/// <summary>A theory that is skipped (not failed) when PostgreSQL is not configured.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute() => Skip = ProviderEnvironment.SkipReason(ProviderEnvironment.Postgres);
}

/// <summary>The provider tests share static caches per entity type: they run one after the other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProviderTestsCollection
{
    public const string Name = "Provider tests";
}
