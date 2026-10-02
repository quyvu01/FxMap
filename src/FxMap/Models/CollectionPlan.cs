using System.Collections;
using System.Linq.Expressions;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Fluent.Rules;

namespace FxMap.Models;

/// <summary>One rule of a collection element with the compiled accessor of the element property it fills.</summary>
/// <param name="Rule">The rule as declared (expression or conditional expression).</param>
/// <param name="Accessor">Accessor of the element property.</param>
/// <param name="PropertyType">Type the response value is deserialized to.</param>
internal sealed record ItemRulePlan(PropertyMappingRule Rule, IPropertyAccessor Accessor, Type PropertyType);

/// <summary>
/// Everything the mapper needs to turn the rows of one selector value into a collection property, compiled once when
/// the profile is built: how to create an element, how to fill it, and how to hand the elements to the property.
/// </summary>
internal sealed class CollectionPlan
{
    private CollectionPlan(CollectionRule rule, ItemRulePlan[] rules, Func<object> createItem,
        Func<IList> createList, Func<IList, object> toContainer)
    {
        Rule = rule;
        Rules = rules;
        CreateItem = createItem;
        CreateList = createList;
        ToContainer = toContainer;
    }

    internal CollectionRule Rule { get; }

    internal ItemRulePlan[] Rules { get; }

    /// <summary>Maximum number of elements kept for one selector value, or null.</summary>
    internal int? Limit => Rule.Limit;

    internal Func<object> CreateItem { get; }

    internal Func<IList> CreateList { get; }

    /// <summary>Converts the filled list to the type of the property (the list itself, or an array).</summary>
    internal Func<IList, object> ToContainer { get; }

    internal static CollectionPlan Build(CollectionRule rule, Type propertyType)
    {
        var itemType = rule.ItemType;
        var rules = rule.Rules
            .Select(r => new ItemRulePlan(r, CreateAccessor(itemType, r.TargetPropertyInfo),
                r.TargetPropertyInfo.PropertyType))
            .ToArray();

        var createItem = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(itemType), typeof(object)))
            .Compile();
        var listType = typeof(List<>).MakeGenericType(itemType);
        var createList = Expression.Lambda<Func<IList>>(Expression.Convert(Expression.New(listType), typeof(IList)))
            .Compile();

        Func<IList, object> toContainer = propertyType.IsArray
            ? list =>
            {
                var array = Array.CreateInstance(itemType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }
            : list => list;

        return new CollectionPlan(rule, rules, createItem, createList, toContainer);
    }

    private static IPropertyAccessor CreateAccessor(Type type, System.Reflection.PropertyInfo property)
    {
        var accessorType = typeof(PropertyAccessor<,>).MakeGenericType(type, property.PropertyType);
        return (IPropertyAccessor)Activator.CreateInstance(accessorType, property)!;
    }
}
