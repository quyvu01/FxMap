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
        public const string TagFxMapAttribute = "fxmap.attribute";
        public const string TagFxMapTransport = "fxmap.transport";
        public const string TagFxMapVersion = "fxmap.version";
        public const string TagFxMapExpressions = "fxmap.expressions";
        public const string TagFxMapSelectorCount = "fxmap.selector_count";
        public const string TagFxMapSelectorIds = "fxmap.selector_ids";
        public const string TagFxMapItemCount = "fxmap.item_count";
        public const string TagFxMapStatus = "fxmap.status";
        public const string TagFxMapErrorType = "fxmap.error_type";

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

        // Metric label/dimension names (following MassTransit pattern: fxmap.*)
        public const string LabelFxMapAttribute = "fxmap.attribute";
        public const string LabelFxMapTransport = "fxmap.transport";
        public const string LabelFxMapStatus = "fxmap.status";
        public const string LabelFxMapErrorType = "fxmap.error_type";
        public const string LabelFxMapDestination = "fxmap.destination";
        public const string LabelFxMapSource = "fxmap.source";
        public const string LabelFxMapDirection = "fxmap.direction";
        public const string LabelFxMapDbSystem = "fxmap.db_system";
        public const string LabelFxMapOperation = "fxmap.operation";
        public const string LabelFxMapComplexity = "fxmap.complexity";

        // Metric label values
        public const string StatusSuccess = "success";
        public const string StatusError = "error";
        public const string DirectionSend = "send";
        public const string DirectionReceive = "receive";

        // Complexity levels
        public const string ComplexitySimple = "simple";
        public const string ComplexityMedium = "medium";
        public const string ComplexityComplex = "complex";

        // DiagnosticSource event names (lowercase following OTel conventions)
        public const string EventRequestStart = "fxmap.request.start";
        public const string EventRequestStop = "fxmap.request.stop";
        public const string EventRequestError = "fxmap.request.error";
        public const string EventMessageSend = "fxmap.message.send";
        public const string EventMessageReceive = "fxmap.message.receive";
        public const string EventDatabaseQueryStart = "fxmap.database.query.start";
        public const string EventDatabaseQueryStop = "fxmap.database.query.stop";
        public const string EventDatabaseQueryError = "fxmap.database.query.error";
        public const string EventExpressionParse = "fxmap.expression.parse";
        public const string EventCacheLookup = "fxmap.cache.lookup";
        public const string EventException = "exception";

        // Metric names (following MassTransit pattern: fxmap.*)
        public const string MetricRequestCount = "fxmap.request.count";
        public const string MetricRequestErrors = "fxmap.request.errors";
        public const string MetricItemsReturned = "fxmap.items.returned";
        public const string MetricMessagesSent = "fxmap.messages.sent";
        public const string MetricMessagesReceived = "fxmap.messages.received";
        public const string MetricRequestDuration = "fxmap.request.duration";
        public const string MetricItemsPerRequest = "fxmap.items.per_request";
        public const string MetricMessageSize = "fxmap.message.size";
        public const string MetricDatabaseQueryDuration = "fxmap.database.query.duration";
        public const string MetricExpressionParsingDuration = "fxmap.expression.parsing.duration";
        public const string MetricRequestsActive = "fxmap.request.active";

        // Metric units
        public const string UnitMilliseconds = "ms";
        public const string UnitBytes = "bytes";

        // Metric descriptions
        public const string DescriptionRequestCount = "Total number of FxMap requests";
        public const string DescriptionRequestErrors = "Total number of FxMap request errors";
        public const string DescriptionItemsReturned = "Total number of items returned by FxMap requests";
        public const string DescriptionMessagesSent = "Total number of messages sent";
        public const string DescriptionMessagesReceived = "Total number of messages received";
        public const string DescriptionRequestDuration = "Duration of FxMap requests";
        public const string DescriptionItemsPerRequest = "Number of items returned per request";
        public const string DescriptionMessageSize = "Size of messages";
        public const string DescriptionDatabaseQueryDuration = "Duration of database queries";
        public const string DescriptionExpressionParsingDuration = "Duration of expression parsing";
        public const string DescriptionRequestsActive = "Current number of active FxMap requests";
    }
}