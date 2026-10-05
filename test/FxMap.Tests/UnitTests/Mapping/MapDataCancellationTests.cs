using FxMap.PublicContracts;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// MapDataAsync(value, token) and MapDataAsync(value, context, token): the cancellation token(s) and the headers
/// reach the requests, and cancelling either token cancels a request that is in flight.
/// </summary>
public class MapDataCancellationTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    // A remote that never answers until the token of the request is cancelled.
    private static Action<FakeRemote> Hangs(TaskCompletionSource started) => remote =>
        remote.OnRequestWithToken = async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        };

    #region Without a context

    [Fact]
    public async Task The_caller_token_reaches_the_remote_request()
    {
        using var h = MappingHarness.Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, cts.Token);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().CancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_the_caller_token_cancels_a_request_in_flight()
    {
        var started = new TaskCompletionSource();
        using var h = MappingHarness.Create(c => c.ThrowIfException(), Hangs(started));
        using var cts = new CancellationTokenSource();

        var map = h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, cts.Token);
        await started.Task.WaitAsync(Wait);
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => map.WaitAsync(Wait));
    }

    [Fact]
    public async Task Without_any_token_or_context_nothing_is_cancelled_and_no_headers_are_sent()
    {
        using var h = MappingHarness.Create();

        await h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" });

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.CancellationRequested.ShouldBeFalse();
        call.Headers.ShouldBeEmpty();
    }

    #endregion

    #region With a context

    private static RequestContext ContextWith(CancellationToken token, params (string Key, string Value)[] headers) =>
        new(headers.ToDictionary(h => h.Key, h => h.Value), token);

    [Fact]
    public async Task The_headers_of_the_context_reach_the_remote_request()
    {
        using var h = MappingHarness.Create();
        var context = ContextWith(CancellationToken.None, ("x-tenant", "acme"), ("x-trace", "42"));

        await h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, context);

        var headers = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Headers!;
        headers["x-tenant"].ShouldBe("acme");
        headers["x-trace"].ShouldBe("42");
    }

    [Fact]
    public async Task The_token_of_the_context_reaches_the_remote_request()
    {
        using var h = MappingHarness.Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, ContextWith(cts.Token));

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().CancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_the_token_of_the_context_cancels_a_request_in_flight()
    {
        var started = new TaskCompletionSource();
        using var h = MappingHarness.Create(c => c.ThrowIfException(), Hangs(started));
        using var cts = new CancellationTokenSource();

        var map = h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, ContextWith(cts.Token));
        await started.Task.WaitAsync(Wait);
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => map.WaitAsync(Wait));
    }

    [Fact]
    public async Task When_both_are_given_cancelling_the_caller_token_cancels_the_request()
    {
        var started = new TaskCompletionSource();
        using var h = MappingHarness.Create(c => c.ThrowIfException(), Hangs(started));
        using var caller = new CancellationTokenSource();
        using var contextSource = new CancellationTokenSource();

        var map = h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, ContextWith(contextSource.Token), caller.Token);
        await started.Task.WaitAsync(Wait);
        await caller.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => map.WaitAsync(Wait));
    }

    [Fact]
    public async Task When_both_are_given_cancelling_the_context_token_cancels_the_request()
    {
        var started = new TaskCompletionSource();
        using var h = MappingHarness.Create(c => c.ThrowIfException(), Hangs(started));
        using var caller = new CancellationTokenSource();
        using var contextSource = new CancellationTokenSource();

        var map = h.Mapper.MapDataAsync(new FlatDto { UserId = "u1" }, ContextWith(contextSource.Token), caller.Token);
        await started.Task.WaitAsync(Wait);
        await contextSource.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => map.WaitAsync(Wait));
    }

    [Fact]
    public async Task A_context_without_headers_or_a_token_changes_nothing()
    {
        using var h = MappingHarness.Create();
        var dto = new FlatDto { UserId = "u1" };

        await h.Mapper.MapDataAsync(dto, ContextWith(CancellationToken.None));

        dto.UserName.ShouldBe("user-name:u1");
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().CancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task A_null_context_behaves_like_no_context()
    {
        using var h = MappingHarness.Create();
        var dto = new FlatDto { UserId = "u1" };

        await h.Mapper.MapDataAsync(dto, null!, CancellationToken.None);

        dto.UserName.ShouldBe("user-name:u1");
    }

    [Fact]
    public async Task Every_request_of_one_call_gets_the_headers_and_the_token()
    {
        using var h = MappingHarness.Create();
        var context = ContextWith(CancellationToken.None, ("x-tenant", "acme"));
        var dto = new ChainDto { UserId = "u1" }; // User -> Province -> Country: three requests

        await h.Mapper.MapDataAsync(dto, context);

        h.Remote.Calls.Count.ShouldBe(3);
        h.Remote.Calls.ShouldAllBe(c => c.Headers!["x-tenant"] == "acme");
    }

    #endregion
}
