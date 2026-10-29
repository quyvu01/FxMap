namespace FxMap.Responses;

/// <summary>
/// An expression of a request that the owner of the data could not build (an unknown function, an unknown property...).
/// It does not depend on the rows: it is reported once for the response, even when no row matched.
/// </summary>
/// <param name="Expression">The expression of the request, as the caller wrote it.</param>
/// <param name="Message">What went wrong, e.g. <c>Expression 'Orders:summ(Total)' failed to build: unknown function 'summ'</c>.</param>
public sealed record ExpressionError(string Expression, string Message);
