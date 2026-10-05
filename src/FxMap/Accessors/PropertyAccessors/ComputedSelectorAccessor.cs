using System.Linq.Expressions;
using FxMap.Helpers;

namespace FxMap.Accessors.PropertyAccessors;

/// <summary>
/// Reads the key of a mapping rule that is declared as an expression over the model
/// (<c>Of(x => x.Id + x.Email)</c>) instead of a single property. Read-only: there is no property to write to.
/// </summary>
/// <remarks>
/// A selector that goes through a null (<c>x => x.Address.Code</c> with no address) has no key, like a property that
/// is null: <see cref="Get"/> returns null instead of throwing.
/// </remarks>
internal sealed class ComputedSelectorAccessor : IPropertyAccessor
{
    private readonly Func<object, object> _getter;

    public ComputedSelectorAccessor(Type modelType, LambdaExpression selector)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var body = SelectorExpressions.Rebind(selector, Expression.Convert(instance, modelType));
        _getter = Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), instance).Compile();
    }

    public object Get(object instance)
    {
        try
        {
            return _getter(instance);
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    public void Set(object instance, object value) =>
        throw new InvalidOperationException("The key of a rule declared with an expression cannot be set.");
}