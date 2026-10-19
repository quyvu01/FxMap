using System.Diagnostics;
using FxMap.Abstractions;
using FxMap.Telemetry;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Telemetry;

public sealed class TelemetryProbeKey : IDistributedKey;

/// <summary>
/// The activities of FxMap (OpenTelemetry: AddSource("FxMap")). Each test names its operation, so the activities that other tests
/// start at the same time are not mixed up with the ones it looks at.
/// </summary>
public class FxMapActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _started = [];

    public FxMapActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "FxMap",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => { lock (_started) _started.Add(a); }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static string Name() => "test." + Guid.NewGuid().ToString("N");

    private static string TagOf(Activity activity, string key) => activity.GetTagItem(key)?.ToString();

    [Fact]
    public void A_client_activity_carries_the_key_the_transport_and_the_version()
    {
        var name = Name();

        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", name);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe(name);
        activity.Kind.ShouldBe(ActivityKind.Client);
        TagOf(activity, "fxmap.distributed_key").ShouldBe(nameof(TelemetryProbeKey));
        TagOf(activity, "fxmap.transport").ShouldBe("nats");
        TagOf(activity, "fxmap.version").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void A_client_activity_has_a_default_name_when_none_is_given()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("grpc");

        activity.OperationName.ShouldBe("fxmap.request");
    }

    [Fact]
    public void A_server_activity_continues_the_trace_of_the_client()
    {
        using var client = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", Name());
        var name = Name();

        using var server = FxMapActivitySource.StartServerActivity(nameof(TelemetryProbeKey), client.Context, name);

        server.Kind.ShouldBe(ActivityKind.Server);
        server.TraceId.ShouldBe(client.TraceId);
        server.ParentSpanId.ShouldBe(client.SpanId);
        TagOf(server, "fxmap.distributed_key").ShouldBe(nameof(TelemetryProbeKey));
    }

    [Fact]
    public void A_server_activity_without_a_parent_starts_a_trace()
    {
        using var server = FxMapActivitySource.StartServerActivity(nameof(TelemetryProbeKey), default, Name());

        server.ParentId.ShouldBeNull();
        server.TraceId.ToString().ShouldNotBe(default(ActivityTraceId).ToString());
    }

    [Fact]
    public void A_database_activity_carries_the_system_of_the_database()
    {
        using var activity = FxMapActivitySource.StartDatabaseActivity<TelemetryProbeKey>("postgresql", Name());

        activity.Kind.ShouldBe(ActivityKind.Client);
        TagOf(activity, "db.system").ShouldBe("postgresql");
        TagOf(activity, "fxmap.distributed_key").ShouldBe(nameof(TelemetryProbeKey));
    }

    [Fact]
    public void Messaging_tags_are_set_only_for_the_values_that_are_given()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("kafka", Name());

        activity.SetMessagingTags("kafka", destination: "topic-a");

        TagOf(activity, "messaging.system").ShouldBe("kafka");
        TagOf(activity, "messaging.destination").ShouldBe("topic-a");
        activity.GetTagItem("messaging.message_id").ShouldBeNull();
        activity.GetTagItem("messaging.operation").ShouldBeNull();

        activity.SetMessagingTags("kafka", "topic-a", "m-1", "process");
        TagOf(activity, "messaging.message_id").ShouldBe("m-1");
        TagOf(activity, "messaging.operation").ShouldBe("process");
    }

    [Fact]
    public void Database_tags_are_set_only_for_the_values_that_are_given()
    {
        using var activity = FxMapActivitySource.StartDatabaseActivity<TelemetryProbeKey>("mongodb", Name());

        activity.SetDatabaseTags("mongodb", dbName: "shop", collection: "orders");

        TagOf(activity, "db.name").ShouldBe("shop");
        TagOf(activity, "db.collection").ShouldBe("orders");
        activity.GetTagItem("db.statement").ShouldBeNull();
        activity.GetTagItem("db.operation").ShouldBeNull();
    }

    [Fact]
    public void FxMap_tags_count_the_selectors_and_name_only_the_first_five()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", Name());

        activity.SetFxMapTags(["Name", "Email"], ["1", "2", "3", "4", "5", "6", "7"], itemCount: 6);

        TagOf(activity, "fxmap.selector_count").ShouldBe("7");
        TagOf(activity, "fxmap.selector_ids").ShouldBe("1,2,3,4,5");
        TagOf(activity, "fxmap.item_count").ShouldBe("6");
        ((string[])activity.GetTagItem("fxmap.expressions")).ShouldBe(["Name", "Email"]);
    }

    [Fact]
    public void FxMap_tags_without_selectors_set_no_selector_tag()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", Name());

        activity.SetFxMapTags(["Name"], []);

        activity.GetTagItem("fxmap.selector_count").ShouldBeNull();
        activity.GetTagItem("fxmap.selector_ids").ShouldBeNull();
        activity.GetTagItem("fxmap.item_count").ShouldBeNull();
    }

    [Fact]
    public void An_exception_is_recorded_as_an_event_and_marks_the_activity_as_an_error()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", Name());
        Exception error;
        try { throw new InvalidOperationException("boom"); }
        catch (Exception e) { error = e; }

        activity.RecordException(error);

        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("boom");
        var recorded = activity.Events.ShouldHaveSingleItem();
        recorded.Name.ShouldBe("exception");
        recorded.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString())["exception.type"].ShouldBe(typeof(InvalidOperationException).FullName);
        recorded.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString())["exception.message"].ShouldBe("boom");
        recorded.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString())["exception.stacktrace"].ShouldContain(nameof(An_exception_is_recorded_as_an_event_and_marks_the_activity_as_an_error));
    }

    [Fact]
    public void An_exception_that_was_never_thrown_can_be_recorded()
    {
        using var activity = FxMapActivitySource.StartClientActivity<TelemetryProbeKey>("nats", Name());

        Should.NotThrow(() => activity.RecordException(new TimeoutException("slow")));

        activity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public void The_helpers_do_nothing_for_an_activity_that_was_not_started()
    {
        // When nobody listens (no OpenTelemetry), StartActivity gives null and the code of FxMap still calls the helpers
        Activity none = null;
        var failures = new List<string>();
        void Try(string name, Action action)
        {
            try { action(); }
            catch (Exception e) { failures.Add($"{name}: {e.GetType().Name} {e.StackTrace?.Split('\n')[0].Trim()}"); }
        }

        Try("SetMessagingTags", () => none.SetMessagingTags("nats", "subject", "id", "op"));
        Try("SetDatabaseTags", () => none.SetDatabaseTags("mongodb", "db", "stmt", "col", "op"));
        Try("SetFxMapTags", () => none.SetFxMapTags(["x"], ["1"], 1));
        // (SetStatus is not here: Activity has its own SetStatus, which wins over the extension of FxMap, and FxMap calls it as activity?.SetStatus)
        Try("RecordException", () => none.RecordException(new Exception("x")));

        failures.ShouldBeEmpty();
    }

    public void Dispose() => _listener.Dispose();
}
