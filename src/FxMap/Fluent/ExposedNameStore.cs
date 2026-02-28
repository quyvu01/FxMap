using System.Reflection;

namespace FxMap.Fluent;

public sealed record ExposedNameStore(PropertyInfo PropertyInfo, string ExposedPropertyName);