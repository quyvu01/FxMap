using System.Reflection;
using FxMap.Helpers;

namespace FxMap.Extensions;

/// <summary>
/// Provides extension methods for Type reflection and introspection.
/// </summary>
/// <remarks>
/// These utilities support the FxMap framework's reflection-based property discovery
/// and type analysis capabilities.
/// </remarks>
public static class TypeExtensions
{
    /// <param name="type"></param>
    extension(Type type)
    {
        private IEnumerable<PropertyInfo> GetAllProperties() => type.GetTypeInfo().GetAllProperties();
        internal bool IsPrimitiveType() => GeneralHelpers.IsPrimitiveType(type);
    }

    /// <summary>
    /// Gets all properties from a TypeInfo, including inherited properties.
    /// </summary>
    /// <param name="typeInfo">The TypeInfo to inspect.</param>
    /// <returns>An enumerable of all properties including those from base types.</returns>
    public static IEnumerable<PropertyInfo> GetAllProperties(this TypeInfo typeInfo)
    {
        if (typeInfo.BaseType != null)
        {
            foreach (var prop in typeInfo.BaseType.GetAllProperties())
                yield return prop;
        }

        var specialGetPropertyNames = typeInfo.DeclaredMethods
            .Where(x => x.IsSpecialName && x.Name.StartsWith("get_") && !x.IsStatic)
            .Select(x => x.Name["get_".Length..]).Distinct();

        var properties = typeInfo.DeclaredProperties
            .Where(x => specialGetPropertyNames.Contains(x.Name))
            .ToList();

        if (typeInfo.IsInterface)
        {
            var sourceProperties = properties
                .Concat(typeInfo.ImplementedInterfaces.SelectMany(x => x.GetProperties(BindingFlags.DeclaredOnly |
                    BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)));

            foreach (var prop in sourceProperties)
                yield return prop;

            yield break;
        }

        foreach (var info in properties)
            yield return info;
    }

    /// <param name="type">The type to check</param>
    extension(Type type)
    {
        public IEnumerable<Type> GetAllInterfaces()
        {
            if (type.IsInterface) yield return type;

            foreach (var interfaceType in type.GetInterfaces())
                yield return interfaceType;
        }

        public IEnumerable<PropertyInfo> GetAllStaticProperties()
        {
            var info = type.GetTypeInfo();

            if (type.BaseType != null)
            {
                foreach (var prop in type.BaseType.GetAllStaticProperties())
                    yield return prop;
            }

            var props = info.DeclaredMethods
                .Where(x => x.IsSpecialName && x.Name.StartsWith("get_") && x.IsStatic)
                .Select(x => info.GetDeclaredProperty(x.Name.Substring("get_".Length)));

            foreach (var propertyInfo in props)
                if (propertyInfo != null)
                    yield return propertyInfo;
        }
    }

    /// <param name="provider">An attribute provider, which can be a MethodInfo, PropertyInfo, Type, etc.</param>
    extension(ICustomAttributeProvider provider)
    {
        /// <summary>
        /// Returns the first attribute of the specified type for the object specified
        /// </summary>
        /// <typeparam name="T">The type of attribute</typeparam>
        /// <returns>The attribute instance if found, or null</returns>
        public IEnumerable<T> GeTDistributedKey<T>() where T : Attribute =>
            provider.GetCustomAttributes(typeof(T), true)
                .Cast<T>();

        /// <summary>
        /// Determines if the target has the specified attribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasAttribute<T>() where T : Attribute => provider.GeTDistributedKey<T>().Any();
    }
}