using System.Reflection;

namespace FxMap.Helpers;

/// <summary>
/// Creates an object whose constructor parameters come from an <see cref="IServiceProvider"/>, for the send behaviors
/// of <see cref="FxMapBuilder"/>: the public constructor with the most parameters that can all be given is used; a
/// parameter the provider does not have takes its default value when it has one.
/// </summary>
internal static class ServiceActivator
{
    internal static object Create(Type type, IServiceProvider services)
    {
        foreach (var constructor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            if (TryGetArguments(constructor, services, out var arguments)) return constructor.Invoke(arguments);
        }

        throw new InvalidOperationException(
            $"{type.Name} cannot be created: no public constructor has all its parameters in the services given to UseServices.");
    }

    private static bool TryGetArguments(ConstructorInfo constructor, IServiceProvider services, out object[] arguments)
    {
        var parameters = constructor.GetParameters();
        arguments = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var value = services.GetService(parameters[i].ParameterType);
            if (value is null && !parameters[i].HasDefaultValue) return false;
            arguments[i] = value ?? parameters[i].DefaultValue;
        }

        return true;
    }
}

/// <summary>What conditional expressions and send behaviors receive when the builder was given no services.</summary>
internal sealed class EmptyServiceProvider : IServiceProvider
{
    internal static readonly EmptyServiceProvider Instance = new();
    public object GetService(Type serviceType) => null;
}
