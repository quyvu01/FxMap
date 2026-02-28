namespace FxMap.HotChocolate.Constants;

/// <summary>
/// Constants used for storing FxMap context data in HotChocolate resolver context.
/// </summary>
internal static class GraphQlConstants
{
    private const string ContextFieldContextHeader = "fxmap.field.context";

    internal static string GetContextFieldContextHeader(string methodPath) =>
        $"{ContextFieldContextHeader}.{methodPath}";
}