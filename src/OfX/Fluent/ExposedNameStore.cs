using System.Reflection;

namespace OfX.Fluent;

public sealed record ExposedNameStore(PropertyInfo PropertyInfo, string ExposedPropertyName);