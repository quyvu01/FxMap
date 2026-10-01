using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>The mapper and the per-type caches behind it are shared; many calls at once must not see each other.</summary>
public class DistributedMapperConcurrencyTests
{
    [Fact]
    public async Task Many_parallel_calls_each_get_their_own_correct_result()
    {
        using var h = MappingHarness.Create(remote: r => r.OnRequest = _ => Task.Delay(1));

        var work = Enumerable.Range(0, 64).Select(i => Task.Run(async () =>
        {
            var dtos = Enumerable.Range(0, 25).Select(j => new ChainDto { UserId = $"c{i}-u{j % 4}" }).ToList();
            await h.Map(dtos);
            return (i, dtos);
        })).ToList();

        foreach (var (i, dtos) in await Task.WhenAll(work))
        foreach (var d in dtos)
        {
            d.UserName.ShouldBe($"user-name:{d.UserId}", $"call {i}");
            d.ProvinceName.ShouldBe($"province-name:province-id:{d.UserId}", $"call {i}");
            d.CountryName.ShouldBe($"country-name:country-id:province-id:{d.UserId}", $"call {i}");
        }
    }

    [Fact]
    public async Task Parallel_calls_of_different_shapes_do_not_interfere()
    {
        using var h = MappingHarness.Create();

        var tasks = Enumerable.Range(0, 40).Select(i => Task.Run(async () =>
        {
            switch (i % 4)
            {
                case 0:
                {
                    var d = new FlatDto { UserId = $"f{i}" };
                    await h.Map(d);
                    d.UserName.ShouldBe($"user-name:f{i}");
                    break;
                }
                case 1:
                {
                    var d = new DiamondDto { UserId = $"d{i}", ProductId = $"p{i}" };
                    await h.Map(d);
                    d.CategoryName.ShouldBe($"category-name:category-id:p{i}");
                    d.ProvinceName.ShouldBe($"province-name:province-id:d{i}");
                    break;
                }
                case 2:
                {
                    var o = new OrderDto
                        { UserId = $"o{i}", Items = [new ItemDto { ProductId = $"p{i}" }] };
                    await h.Map(o);
                    o.Items[0].CategoryName.ShouldBe($"category-name:category-id:p{i}");
                    break;
                }
                default:
                {
                    var n = new Node { UserId = $"n{i}", Next = new Node { UserId = $"m{i}" } };
                    await h.Map(n);
                    n.Next.ProvinceName.ShouldBe($"province-name:province-id:m{i}");
                    break;
                }
            }
        }));

        await Task.WhenAll(tasks);
    }
}
