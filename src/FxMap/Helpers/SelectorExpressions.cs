using System.Linq.Expressions;
using System.Reflection;

namespace FxMap.Helpers;

/// <summary>Helpers shared by the places that accept a selector lambda (entity Id, profile Of).</summary>
internal static class SelectorExpressions
{
    /// <summary>True when the selector reads its parameter (a selector such as <c>x => 5</c> reads nothing).</summary>
    public static bool ReadsParameter(LambdaExpression selector)
    {
        var visitor = new ParameterVisitor(selector.Parameters[0]);
        visitor.Visit(selector.Body);
        return visitor.Used;
    }

    /// <summary>The properties that the selector reads straight from its parameter (<c>x.Id</c> in <c>x => x.Id + x.Email</c>).</summary>
    public static IReadOnlyList<PropertyInfo> PropertiesOfParameter(LambdaExpression selector)
    {
        var visitor = new ParameterVisitor(selector.Parameters[0]);
        visitor.Visit(selector.Body);
        return visitor.Properties;
    }

    /// <summary>The body of the selector with its parameter replaced by <paramref name="replacement"/>.</summary>
    public static Expression Rebind(LambdaExpression selector, Expression replacement) =>
        new ParameterReplacer(selector.Parameters[0], replacement).Visit(selector.Body);

    private sealed class ParameterVisitor(ParameterExpression parameter) : ExpressionVisitor
    {
        private readonly List<PropertyInfo> _properties = [];

        public bool Used { get; private set; }
        public IReadOnlyList<PropertyInfo> Properties => _properties;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter) Used = true;
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == parameter && node.Member is PropertyInfo property && !_properties.Contains(property))
                _properties.Add(property);
            return base.VisitMember(node);
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
