namespace OfX.HotChocolate.Constants;

/// <summary>
/// Constants used for storing OfX context data in HotChocolate resolver context.
/// </summary>
internal static class GraphQlConstants
{
    private const string ContextFieldContextHeader = "ofx.field.context";

    internal static string GetContextFieldContextHeader(string methodPath) =>
        $"{ContextFieldContextHeader}.{methodPath}";
}