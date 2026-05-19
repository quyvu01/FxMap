namespace FxMap.Configuration;

public static class Constants
{
    public static readonly string Version = PackageInfo.Version;
    public const string Source = "FxMap";

    /// <summary>
    /// OpenTelemetry and diagnostic event names and tag keys used across FxMap telemetry.
    /// Following MassTransit-style naming conventions with 'fxmap.' prefix for consistency.
    /// </summary>
    public static class Telemetry
    {
        // Activity/Span operation names (lowercase following OTel conventions)
        public const string OperationRequest = "fxmap.request";
        public const string OperationProcess = "fxmap.process";

        // OpenTelemetry semantic convention tag keys (activity tags) - fxmap.* prefix
        public const string TagFxMapDistributedKey = "fxmap.distributed_key";
        public const string TagFxMapTransport = "fxmap.transport";
        public const string TagFxMapVersion = "fxmap.version";
        public const string TagFxMapExpressions = "fxmap.expressions";
        public const string TagFxMapSelectorCount = "fxmap.selector_count";
        public const string TagFxMapSelectorIds = "fxmap.selector_ids";
        public const string TagFxMapItemCount = "fxmap.item_count";

        // Messaging tags (OpenTelemetry standard - keep messaging.* prefix)
        public const string TagMessagingSystem = "messaging.system";
        public const string TagMessagingDestination = "messaging.destination";
        public const string TagMessagingMessageId = "messaging.message_id";
        public const string TagMessagingOperation = "messaging.operation";

        // Database tags (OpenTelemetry standard - keep db.* prefix)
        public const string TagDbSystem = "db.system";
        public const string TagDbName = "db.name";
        public const string TagDbStatement = "db.statement";
        public const string TagDbCollection = "db.collection";
        public const string TagDbOperation = "db.operation";

        // Exception tags (OpenTelemetry standard - keep exception.* prefix)
        public const string TagExceptionType = "exception.type";
        public const string TagExceptionMessage = "exception.message";
        public const string TagExceptionStacktrace = "exception.stacktrace";

        // Status description (OpenTelemetry standard)
        public const string TagStatusDescription = "otel.status_description";

        public const string EventException = "exception";
    }
}