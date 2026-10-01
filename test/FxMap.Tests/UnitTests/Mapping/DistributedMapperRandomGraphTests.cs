using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// Oracle based tests. Random graphs (trees, shared references, cycles, every container kind, null ids, ids the
/// remote does not know) are generated from a seed, mapped, and checked against an independent computation of
/// what every node must look like. Request shape is checked against the set of ids reachable in the graph.
/// </summary>
public class DistributedMapperRandomGraphTests
{
    private const string GhostPrefix = "ghost-";

    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        for (var seed = 1; seed <= 40; seed++) data.Add(seed);
        return data;
    }

    private sealed class GraphBuilder(int seed, int maxDepth)
    {
        private readonly Random _rnd = new(seed);
        public readonly List<GNode> All = [];

        private string RandomUserId() => _rnd.Next(10) switch
        {
            0 => null,
            1 => $"{GhostPrefix}{_rnd.Next(3)}",
            _ => $"u{_rnd.Next(9)}"
        };

        private GNode NewNode()
        {
            var node = new GNode { Id = $"n{All.Count}", UserId = RandomUserId() };
            All.Add(node);
            return node;
        }

        // Existing nodes are re-used on purpose: that creates shared references and cycles.
        private GNode Pick(int depth) => _rnd.Next(5) == 0 && All.Count > 1
            ? All[_rnd.Next(All.Count)]
            : Build(depth + 1);

        public GNode Build(int depth)
        {
            var node = NewNode();
            if (depth >= maxDepth) return node;
            if (_rnd.Next(2) == 0) node.Left = Pick(depth);
            if (_rnd.Next(2) == 0) node.Right = Pick(depth);
            for (var i = _rnd.Next(4); i > 0; i--) node.Children.Add(Pick(depth));
            for (var i = _rnd.Next(3); i > 0; i--) node.Named[$"k{i}"] = Pick(depth);
            return node;
        }
    }

    private static IEnumerable<GNode> Reachable(GNode root)
    {
        var seen = new HashSet<GNode>(ReferenceEqualityComparer.Instance as IEqualityComparer<GNode>);
        var stack = new Stack<GNode>([root]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is null || !seen.Add(node)) continue;
            yield return node;
            stack.Push(node.Left);
            stack.Push(node.Right);
            foreach (var c in node.Children) stack.Push(c);
            foreach (var c in node.Named.Values) stack.Push(c);
        }
    }

    private static MappingHarness CreateHarness() => MappingHarness.Create(remote: r =>
        r.Setup<UserKey>(MappingHarness.Standard("user"), id => !id.StartsWith(GhostPrefix))
            .Setup<ProvinceKey>(MappingHarness.Standard("province")));

    private static bool Known(string userId) => userId is not null && !userId.StartsWith(GhostPrefix);

    private static void AssertMapped(GNode node)
    {
        if (!Known(node.UserId))
        {
            node.UserName.ShouldBeNull($"{node.Id}: unknown or missing user must stay untouched");
            node.ProvinceId.ShouldBeNull();
            node.ProvinceName.ShouldBeNull();
            return;
        }

        node.UserName.ShouldBe($"user-name:{node.UserId}", $"{node.Id}");
        node.ProvinceId.ShouldBe($"province-id:{node.UserId}", $"{node.Id}");
        node.ProvinceName.ShouldBe($"province-name:province-id:{node.UserId}", $"{node.Id}");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Every_reachable_node_is_mapped_exactly_as_the_oracle_predicts(int seed)
    {
        using var h = CreateHarness();
        var root = new GraphBuilder(seed, maxDepth: 5).Build(0);
        var nodes = Reachable(root).ToList();

        await h.Map(root).WaitAsync(TimeSpan.FromSeconds(30));

        foreach (var node in nodes) AssertMapped(node);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Requests_carry_exactly_the_reachable_ids_without_nulls_or_duplicates(int seed)
    {
        using var h = CreateHarness();
        var root = new GraphBuilder(seed, maxDepth: 5).Build(0);
        var nodes = Reachable(root).ToList();

        await h.Map(root);

        var userIds = nodes.Select(n => n.UserId).Where(id => id is not null).Distinct().OrderBy(x => x).ToList();
        var userCalls = h.Remote.NonEmptyCallsOf<UserKey>();
        if (userIds.Count == 0)
        {
            userCalls.ShouldBeEmpty();
            return;
        }

        var userCall = userCalls.ShouldHaveSingleItem();
        userCall.Ids.ShouldNotContain((string)null);
        userCall.Ids.Length.ShouldBe(userCall.Ids.Distinct().Count());
        userCall.Ids.OrderBy(x => x).ShouldBe(userIds);
        userCall.Expressions.OrderBy(x => x).ShouldBe(["Name", "ProvinceId"]);

        var provinceIds = userIds.Where(Known).Select(id => $"province-id:{id}").OrderBy(x => x).ToList();
        var provinceCalls = h.Remote.NonEmptyCallsOf<ProvinceKey>();
        if (provinceIds.Count == 0)
        {
            provinceCalls.ShouldBeEmpty();
            return;
        }

        var provinceCall = provinceCalls.ShouldHaveSingleItem();
        provinceCall.Ids.OrderBy(x => x).ShouldBe(provinceIds);
        provinceCall.Expressions.ShouldBe(["Name"]);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Mapping_twice_is_idempotent_and_asks_for_the_same_data(int seed)
    {
        using var h = CreateHarness();
        var root = new GraphBuilder(seed, maxDepth: 4).Build(0);
        var nodes = Reachable(root).ToList();

        await h.Map(root);
        var firstCalls = h.Remote.NonEmptyCallsOf<UserKey>().Select(c => c.Ids.OrderBy(x => x).ToArray()).ToList();
        await h.Map(root);

        foreach (var node in nodes) AssertMapped(node);
        var allUserCalls = h.Remote.NonEmptyCallsOf<UserKey>().Select(c => c.Ids.OrderBy(x => x).ToArray()).ToList();
        allUserCalls.Count.ShouldBe(firstCalls.Count * 2);
        allUserCalls.Skip(firstCalls.Count).ShouldBe(firstCalls);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task Splitting_the_graph_into_a_list_of_roots_gives_the_same_result_as_mapping_the_root(int seed)
    {
        using var whole = CreateHarness();
        using var split = CreateHarness();
        var rootA = new GraphBuilder(seed, maxDepth: 4).Build(0);
        var rootB = new GraphBuilder(seed, maxDepth: 4).Build(0);
        var nodesA = Reachable(rootA).ToList();
        var nodesB = Reachable(rootB).ToList();

        await whole.Map(rootA);
        await split.Map(nodesB);

        nodesA.Count.ShouldBe(nodesB.Count);
        for (var i = 0; i < nodesA.Count; i++)
        {
            nodesB[i].UserName.ShouldBe(nodesA[i].UserName);
            nodesB[i].ProvinceId.ShouldBe(nodesA[i].ProvinceId);
            nodesB[i].ProvinceName.ShouldBe(nodesA[i].ProvinceName);
        }
    }
}
