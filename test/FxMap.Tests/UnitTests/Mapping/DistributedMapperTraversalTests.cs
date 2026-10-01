using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// Characterization tests for how the mapper WALKS an object graph (Discover step): roots, containers,
/// shared references, cycles, runtime types and values that must never be walked into.
/// </summary>
public class DistributedMapperTraversalTests
{
    private static string Name(string id) => $"user-name:{id}";

    #region Roots and containers

    public static TheoryData<string> RootShapes() => new() { "single", "list", "array", "hashset", "lazy", "nested-list" };

    [Theory]
    [MemberData(nameof(RootShapes))]
    public async Task Any_root_shape_is_walked(string shape)
    {
        using var h = MappingHarness.Create();
        var a = new FlatDto { UserId = "a" };
        var b = new FlatDto { UserId = "b" };
        object root = shape switch
        {
            "single" => a,
            "list" => new List<FlatDto> { a, b },
            "array" => new[] { a, b },
            "hashset" => new HashSet<FlatDto> { a, b },
            "lazy" => new[] { a, b }.Select(x => x),
            "nested-list" => new List<List<FlatDto>> { new() { a }, new() { b } },
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        await h.Map(root);

        a.UserName.ShouldBe(Name("a"));
        if (shape != "single") b.UserName.ShouldBe(Name("b"));
    }

    [Fact]
    public async Task Root_dictionary_is_walked_through_its_values()
    {
        using var h = MappingHarness.Create();
        var a = new FlatDto { UserId = "a" };
        var b = new FlatDto { UserId = "b" };

        await h.Map(new Dictionary<string, FlatDto> { ["x"] = a, ["y"] = b });

        a.UserName.ShouldBe(Name("a"));
        b.UserName.ShouldBe(Name("b"));
    }

    [Fact]
    public async Task Null_and_primitive_elements_inside_a_root_collection_are_ignored()
    {
        using var h = MappingHarness.Create();
        var a = new FlatDto { UserId = "a" };

        await h.Map(new List<object> { null, 5, "text", Role.Admin, a, DateTime.UtcNow });

        a.UserName.ShouldBe(Name("a"));
    }

    [Fact]
    public async Task Primitive_or_null_root_does_nothing_and_does_not_call_the_remote()
    {
        using var h = MappingHarness.Create();

        await h.Map("text");
        await h.Map(42);
        await h.Map(new List<FlatDto>());

        h.Remote.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Object_without_any_profile_or_mappable_content_is_a_no_op()
    {
        using var h = MappingHarness.Create();

        await h.Map(new object());
        await h.Map(new { Name = "anonymous" });

        h.Remote.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_container_property_of_an_object_is_walked()
    {
        using var h = MappingHarness.Create();
        ItemDto Item(string id) => new() { ProductId = id };
        var order = new OrderDto
        {
            UserId = "u1",
            Items = [Item("p1"), Item("p2")],
            Main = Item("p3"),
            Array = [Item("p4")],
            ByCode = new Dictionary<string, ItemDto> { ["a"] = Item("p5") },
            Lazy = new[] { Item("p6") }.Select(x => x)
        };

        await h.Map(order);

        order.UserName.ShouldBe(Name("u1"));
        var items = order.Items.Concat([order.Main]).Concat(order.Array).Concat(order.ByCode.Values)
            .Concat(order.Lazy).ToList();
        items.Count.ShouldBe(6);
        items.ShouldAllBe(i => i.ProductName == $"product-name:{i.ProductId}");
        items.ShouldAllBe(i => i.CategoryName == $"category-name:category-id:{i.ProductId}");
        h.Remote.CallsOf<ProductKey>().ShouldHaveSingleItem().Ids.Length.ShouldBe(6);
    }

    [Fact]
    public async Task Null_containers_and_null_members_are_tolerated()
    {
        using var h = MappingHarness.Create();
        var order = new OrderDto
            { UserId = "u1", Items = null, Main = null, Array = null, ByCode = null, Lazy = null };

        await h.Map(order);

        order.UserName.ShouldBe(Name("u1"));
    }

    [Fact]
    public async Task Null_elements_inside_a_container_property_are_tolerated()
    {
        using var h = MappingHarness.Create();
        var order = new OrderDto
        {
            UserId = "u1",
            Items = [null, new ItemDto { ProductId = "p1" }, null],
            ByCode = new Dictionary<string, ItemDto> { ["a"] = null }
        };

        await h.Map(order);

        order.Items[1].ProductName.ShouldBe("product-name:p1");
    }

    [Fact]
    public async Task Objects_inside_a_virtual_profile_holder_are_walked_by_their_runtime_type()
    {
        using var h = MappingHarness.Create();
        var holder = new HolderDto
        {
            Item = new DerivedItem { UserId = "a" },
            Items = [new DerivedItem { UserId = "b" }, new BaseItem { Tag = "plain" }],
            Boxed = new DerivedItem { UserId = "c" }
        };

        await h.Map(holder);

        ((DerivedItem)holder.Item).UserName.ShouldBe(Name("a"));
        ((DerivedItem)holder.Items[0]).UserName.ShouldBe(Name("b"));
        ((DerivedItem)holder.Boxed).UserName.ShouldBe(Name("c"));
    }

    #endregion

    #region Shared references and cycles

    [Fact]
    public async Task Instance_shared_by_two_parents_is_mapped_and_requested_once()
    {
        using var h = MappingHarness.Create();
        var shared = new Node { UserId = "s" };
        var root = new Node { UserId = "r", Left = shared, Right = shared, Children = [shared] };

        await h.Map(root);

        shared.UserName.ShouldBe(Name("s"));
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.OrderBy(x => x).ShouldBe(["r", "s"]);
    }

    [Fact]
    public async Task Self_reference_does_not_loop()
    {
        using var h = MappingHarness.Create();
        var node = new Node { UserId = "a" };
        node.Next = node;
        node.Children.Add(node);

        await h.Map(node).WaitAsync(TimeSpan.FromSeconds(10));

        node.UserName.ShouldBe(Name("a"));
    }

    [Fact]
    public async Task Two_node_cycle_does_not_loop()
    {
        using var h = MappingHarness.Create();
        var a = new Node { UserId = "a" };
        var b = new Node { UserId = "b", Next = a };
        a.Next = b;

        await h.Map(a).WaitAsync(TimeSpan.FromSeconds(10));

        a.UserName.ShouldBe(Name("a"));
        b.UserName.ShouldBe(Name("b"));
    }

    [Fact]
    public async Task Cycle_through_containers_does_not_loop()
    {
        using var h = MappingHarness.Create();
        var parent = new Node { UserId = "p" };
        var child = new Node { UserId = "c" };
        parent.Children.Add(child);
        child.Named["up"] = parent;

        await h.Map(parent).WaitAsync(TimeSpan.FromSeconds(10));

        parent.UserName.ShouldBe(Name("p"));
        child.UserName.ShouldBe(Name("c"));
    }

    [Fact]
    public async Task The_same_instance_listed_twice_in_the_root_is_mapped_once()
    {
        using var h = MappingHarness.Create();
        var a = new FlatDto { UserId = "a" };

        await h.Map(new List<FlatDto> { a, a, a });

        a.UserName.ShouldBe(Name("a"));
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["a"]);
    }

    [Fact]
    public async Task Long_linked_chain_is_fully_mapped()
    {
        using var h = MappingHarness.Create();
        var head = new Node { UserId = "n0" };
        var nodes = new List<Node> { head };
        for (var i = 1; i < 1000; i++)
        {
            var next = new Node { UserId = $"n{i}" };
            nodes[^1].Next = next;
            nodes.Add(next);
        }

        await h.Map(head).WaitAsync(TimeSpan.FromSeconds(30));

        nodes.ShouldAllBe(n => n.UserName == Name(n.UserId));
        nodes.ShouldAllBe(n => n.ProvinceName == $"province-name:province-id:{n.UserId}");
    }

    [Fact]
    public async Task Wide_tree_with_thousands_of_objects_is_fully_mapped_with_deduplicated_requests()
    {
        using var h = MappingHarness.Create();
        var root = new Node { UserId = "root" };
        for (var i = 0; i < 3000; i++)
            root.Children.Add(new Node { UserId = $"u{i % 11}", Children = [new Node { UserId = $"u{i % 5}" }] });

        await h.Map(root);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.Length.ShouldBe(12);
        root.Children.ShouldAllBe(n => n.UserName == Name(n.UserId) && n.Children[0].UserName == Name(n.Children[0].UserId));
    }

    #endregion

    #region Runtime types

    [Fact]
    public async Task Property_declared_as_base_type_is_mapped_with_the_profile_of_the_runtime_type()
    {
        using var h = MappingHarness.Create();
        var holder = new HolderDto { Item = new DerivedItem { UserId = "x", Tag = "t" } };

        await h.Map(holder);

        var item = (DerivedItem)holder.Item;
        item.UserName.ShouldBe(Name("x"));
        item.Tag.ShouldBe("t");
    }

    [Fact]
    public async Task Same_declared_type_with_different_runtime_types_in_one_list_works()
    {
        using var h = MappingHarness.Create();
        var items = new List<BaseItem>
        {
            new DerivedItem { UserId = "a" }, new BaseItem { Tag = "plain" }, new DerivedItem { UserId = "b" }
        };

        await h.Map(items);

        ((DerivedItem)items[0]).UserName.ShouldBe(Name("a"));
        ((DerivedItem)items[2]).UserName.ShouldBe(Name("b"));
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.OrderBy(x => x).ShouldBe(["a", "b"]);
    }

    #endregion

    #region Values that are "not primitive" but must be left alone

    [Fact]
    public async Task Large_byte_array_and_plain_collections_do_not_break_or_get_modified()
    {
        using var h = MappingHarness.Create();
        var payload = new byte[300_000];
        new Random(1).NextBytes(payload);
        var copy = payload.ToArray();
        var dto = new BlobDto
        {
            UserId = "u1",
            Payload = payload,
            Tags = ["a", "b"],
            Labels = new Dictionary<string, string> { ["k"] = "v" },
            Numbers = [1, 2, 3]
        };

        await h.Map(dto).WaitAsync(TimeSpan.FromSeconds(30));

        dto.UserName.ShouldBe(Name("u1"));
        dto.Payload.ShouldBe(copy);
        dto.Tags.ShouldBe(["a", "b"]);
        dto.Labels["k"].ShouldBe("v");
        dto.Numbers.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Framework_class_values_are_tolerated()
    {
        using var h = MappingHarness.Create();
        var dto = new BlobDto { UserId = "u1", Link = new Uri("https://example.com/a/b?q=1") };

        await h.Map(dto);

        dto.UserName.ShouldBe(Name("u1"));
        dto.Link.ToString().ShouldBe("https://example.com/a/b?q=1");
    }

    [Fact]
    public async Task Models_inside_every_collection_shape_are_still_found()
    {
        using var h = MappingHarness.Create();
        ItemDto Item(string id) => new() { ProductId = id };
        var dto = new CollectionsDto
        {
            Grouped = new Dictionary<string, List<ItemDto>> { ["a"] = [Item("g1"), Item("g2")] },
            Matrix = [[Item("m1")], [Item("m2"), Item("m3")]],
            Jagged = [[Item("j1")], [Item("j2")]],
            Mixed = new object[] { Item("x1"), "text", 5, null, new List<ItemDto> { Item("x2") } },
            Boxed = new List<ItemDto> { Item("b1") },
            Bag = new Dictionary<string, object> { ["k"] = Item("bag1"), ["n"] = 3 },
            ReadOnly = new Dictionary<string, ItemDto> { ["r"] = Item("ro1") },
            AsInterface = new List<ItemDto> { Item("i1") }
        };

        await h.Map(dto);

        var all = dto.Grouped["a"]
            .Concat(dto.Matrix.SelectMany(x => x))
            .Concat(dto.Jagged.SelectMany(x => x))
            .Concat(dto.Mixed.OfType<ItemDto>())
            .Concat(dto.Mixed.OfType<List<ItemDto>>().SelectMany(x => x))
            .Concat((List<ItemDto>)dto.Boxed)
            .Concat(dto.Bag.Values.OfType<ItemDto>())
            .Concat(dto.ReadOnly.Values)
            .Concat(dto.AsInterface)
            .ToList();
        all.Count.ShouldBe(13);
        all.ShouldAllBe(i => i.ProductName == $"product-name:{i.ProductId}");
        all.ShouldAllBe(i => i.CategoryName == $"category-name:category-id:{i.ProductId}");
    }

    [Fact]
    public async Task Leaf_collections_are_never_enumerated_even_behind_object_or_interface_properties()
    {
        using var h = MappingHarness.Create();
        var counting = new CountingStrings();
        var boxedCounting = new CountingStrings();
        var inList = new CountingStrings();
        var dto = new LeafHolderDto
        {
            UserId = "u1",
            Names = new CountingStringsAsInterface(),
            Counting = counting,
            Boxed = boxedCounting,
            BoxedList = [inList, new byte[1000], new[] { "a" }]
        };

        await h.Map(dto);

        dto.UserName.ShouldBe(Name("u1"));
        counting.Enumerations.ShouldBe(0);
        boxedCounting.Enumerations.ShouldBe(0);
        inList.Enumerations.ShouldBe(0);
        ((CountingStringsAsInterface)dto.Names).Enumerations.ShouldBe(0);
    }

    private sealed class CountingStringsAsInterface : IEnumerable<string>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            Enumerations++;
            yield return "x";
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
