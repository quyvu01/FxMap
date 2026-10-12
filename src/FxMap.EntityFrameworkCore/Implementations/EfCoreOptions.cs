namespace FxMap.EntityFrameworkCore.Implementations;

/// <summary>The settings of the EF Core data provider, as given to <c>AddEntityFrameworkCore</c>.</summary>
internal sealed record EfCoreOptions(bool IsolateExpressionErrors);
