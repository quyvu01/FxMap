using System.Linq.Expressions;
using FxMap.Analyzers;
using FxMap.Delegates;

namespace FxMap.Expressions.Building;

/// <summary>
/// Builds a single projection expression from multiple FxMap expression strings.
/// </summary>
/// <remarks>
/// <para>
/// This builder combines multiple expression strings into a single database query,
/// projecting all requested values as object[] in one round-trip.
/// </para>
/// <para>
/// Example input: ["Name", "Email", "Country.Name", "Orders:count"]
/// </para>
/// <para>
/// Output projection: x => new object[] {
///     x.Id,                    // [0] = Id (always first)
///     x.Name,                  // [1] = Expression null (default)
///     x.Email,                 // [2] = Expression "Email"
///     x.Country.Name,          // [3] = Expression "Country.Name"
///     x.Orders.Count()         // [4] = Expression "Orders:count"
/// }
/// </para>
/// <para>
/// The result can then be transformed to DataResponse in memory.
/// </para>
/// </remarks>
public sealed class ProjectionBuilder<TModel> where TModel : class
{
    private readonly ParameterExpression _parameter = Expression.Parameter(typeof(TModel), "x");
    private readonly GetTypeAccessor _typeAccessor;
    private readonly Func<Expression> _buildId;
    private readonly Func<Expression> _buildDefault;

    /// <summary>
    /// Creates a builder whose id and default property are given by selector lambdas, as declared with
    /// <c>Id(x => ...)</c> and <c>DefaultProperty(x => ...)</c> in an entity configuration.
    /// </summary>
    /// <param name="idSelector">
    /// The id selector, a lambda from <typeparamref name="TModel"/> to the id. It is not checked here: entity
    /// configurations validate their selectors when they are configured.
    /// </param>
    /// <param name="defaultPropertySelector">The default property selector, or null for none.</param>
    /// <param name="typeAccessor">Provides the type accessors used to resolve names in expressions.</param>
    public ProjectionBuilder(LambdaExpression idSelector, LambdaExpression defaultPropertySelector,
        GetTypeAccessor typeAccessor)
    {
        _typeAccessor = typeAccessor;
        _buildId = () => Rebind(idSelector);
        _buildDefault = () => defaultPropertySelector is null
            ? Expression.Constant(null, typeof(object))
            : Rebind(defaultPropertySelector);
    }

    // The body of a selector uses the selector's own parameter; the projection uses a single parameter of its own.
    private Expression Rebind(LambdaExpression selector) =>
        new ParameterReplacer(selector.Parameters[0], _parameter).Visit(selector.Body);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    public Expression<Func<TModel, object[]>> Build(IEnumerable<string> expressions)
    {
        var expressionList = expressions.ToList();
        var projections = new List<Expression>();

        // [0] = Id (always first)
        var idExpr = BuildIdExpression();
        projections.Add(Expression.Convert(idExpr, typeof(object)));

        // [1..n] = Expression values
        foreach (var expr in expressionList)
        {
            var valueExpr = expr is null ? BuildDefaultPropertyExpression() : BuildExpressionValue(expr);
            projections.Add(Expression.Convert(valueExpr, typeof(object)));
        }

        var arrayInit = Expression.NewArrayInit(typeof(object), projections);
        return Expression.Lambda<Func<TModel, object[]>>(arrayInit, _parameter);
    }

    /// <summary>
    /// Builds a projection expression with explicit result tracking.
    /// Returns tuple of (expression, index mapping).
    /// </summary>
    /// <param name="expressions">The expression strings to project.</param>
    /// <returns>The projection expression and metadata about each index.</returns>
    public ProjectionResult<TModel> BuildWithMetadata(IEnumerable<string> expressions)
    {
        var expressionList = expressions.ToList();
        var projections = new List<Expression>();
        var metadata = new List<ProjectionMetadata>();

        // [0] = Id (always first)
        var idExpr = BuildIdExpression();
        projections.Add(Expression.Convert(idExpr, typeof(object)));
        metadata.Add(new ProjectionMetadata(0, null, true));

        // [1..n] = Expression values
        for (var i = 0; i < expressionList.Count; i++)
        {
            var expr = expressionList[i];

            try
            {
                var valueExpr = expr is null ? BuildDefaultPropertyExpression() : BuildExpressionValue(expr);
                projections.Add(Expression.Convert(valueExpr, typeof(object)));
                metadata.Add(new ProjectionMetadata(i + 1, expr, false));
            }
            catch (Exception ex)
            {
                // Add null placeholder for failed expressions
                projections.Add(Expression.Constant(null, typeof(object)));
                metadata.Add(new ProjectionMetadata(i + 1, expr, false, ex.Message));
            }
        }

        var arrayInit = Expression.NewArrayInit(typeof(object), projections);
        var lambda = Expression.Lambda<Func<TModel, object[]>>(arrayInit, _parameter);

        return new ProjectionResult<TModel>(lambda, metadata);
    }

    private Expression BuildIdExpression() => _buildId();

    private Expression BuildDefaultPropertyExpression() => _buildDefault();

    private Expression BuildExpressionValue(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return Expression.Constant(null, typeof(object));

        // Parse the expression using our new parser
        var node = ExpressionParser.Parse(expression);

        // Build context
        var context = new ExpressionBuildContext(typeof(TModel), _parameter, _parameter, _typeAccessor);

        // Build the LINQ expression
        var builder = new LinqExpressionBuilder();
        var result = node.Accept(builder, context);

        return result.Expression;
    }
}

/// <summary>
/// Result of building a projection with metadata.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
/// <param name="Projection">The projection lambda expression.</param>
/// <param name="Metadata">Metadata about each projected value.</param>
public sealed record ProjectionResult<TModel>(
    Expression<Func<TModel, object[]>> Projection,
    IReadOnlyList<ProjectionMetadata> Metadata) where TModel : class;

/// <summary>
/// Metadata about a single projected value.
/// </summary>
/// <param name="Index">The index in the result array.</param>
/// <param name="Expression">The original expression string (null for default/Id).</param>
/// <param name="IsId">Whether this is the Id field.</param>
/// <param name="Error">Error message if expression building failed.</param>
public sealed record ProjectionMetadata(int Index, string Expression, bool IsId, string Error = null)
{
    public bool HasError => Error != null;
}