using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Services;

/// <summary>AddStronglyTypeIdConverter: the converters of the ids that are not a string, a number or a guid.</summary>
public class StronglyTypeIdRegisterTests
{
    public readonly record struct OrderNo(int Value);

    public readonly record struct Sku(string Value);

    public sealed class OrderNoConverter : IStronglyTypeConverter<OrderNo>
    {
        public OrderNo Convert(string input) => new(int.Parse(input));
        public bool CanConvert(string input) => int.TryParse(input, out _);
    }

    public sealed class SecondOrderNoConverter : IStronglyTypeConverter<OrderNo>
    {
        public OrderNo Convert(string input) => new(-1);
        public bool CanConvert(string input) => true;
    }

    /// <summary>One class that converts two ids.</summary>
    public sealed class BothConverter : IStronglyTypeConverter<OrderNo>, IStronglyTypeConverter<Sku>
    {
        OrderNo IStronglyTypeConverter<OrderNo>.Convert(string input) => new(int.Parse(input));
        bool IStronglyTypeConverter<OrderNo>.CanConvert(string input) => int.TryParse(input, out _);
        Sku IStronglyTypeConverter<Sku>.Convert(string input) => new(input);
        bool IStronglyTypeConverter<Sku>.CanConvert(string input) => input.Length > 0;
    }

    public sealed class GenericConverter<T> : IStronglyTypeConverter<T>
    {
        public T Convert(string input) => default;
        public bool CanConvert(string input) => false;
    }

    private static ServiceProvider Build(Action<FxMap.Registries.IMapConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddFxMap(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void A_registered_converter_is_resolved_by_the_type_of_its_id()
    {
        using var provider = Build(c => c.AddStronglyTypeIdConverter(r => r.OfType<OrderNoConverter>()));

        var converter = provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>();

        converter.CanConvert("12").ShouldBeTrue();
        converter.CanConvert("x").ShouldBeFalse();
        converter.Convert("12").ShouldBe(new OrderNo(12));
    }

    [Fact]
    public void A_converter_is_a_singleton()
    {
        using var provider = Build(c => c.AddStronglyTypeIdConverter(r => r.OfType<OrderNoConverter>()));

        provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>()
            .ShouldBeSameAs(provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>());
    }

    [Fact]
    public void A_class_that_converts_two_ids_is_registered_for_both()
    {
        using var provider = Build(c => c.AddStronglyTypeIdConverter(r => r.OfType<BothConverter>()));

        provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>().Convert("5").ShouldBe(new OrderNo(5));
        provider.GetRequiredService<IStronglyTypeConverter<Sku>>().Convert("abc").ShouldBe(new Sku("abc"));
    }

    [Fact]
    public void The_first_converter_of_an_id_is_kept()
    {
        using var provider = Build(c => c.AddStronglyTypeIdConverter(r => r
            .OfType<OrderNoConverter>().OfType<SecondOrderNoConverter>()));

        provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>().Convert("7").ShouldBe(new OrderNo(7));
    }

    [Fact]
    public void Converters_of_different_ids_can_be_registered_in_several_calls()
    {
        using var provider = Build(c =>
        {
            c.AddStronglyTypeIdConverter(r => r.OfType<OrderNoConverter>());
            c.AddStronglyTypeIdConverter(r => r.OfType<BothConverter>()); // OrderNo is kept, Sku is added
        });

        provider.GetRequiredService<IStronglyTypeConverter<OrderNo>>().ShouldBeOfType<OrderNoConverter>();
        provider.GetRequiredService<IStronglyTypeConverter<Sku>>().ShouldBeOfType<BothConverter>();
    }

    [Fact]
    public void A_generic_converter_is_rejected()
    {
        Should.Throw<DistributedMapException.StronglyTypeConfigurationImplementationMustNotBeGeneric>(() =>
            Build(c => c.AddStronglyTypeIdConverter(r => r.OfType<GenericConverter<OrderNo>>())));
    }

    [Fact]
    public void No_options_are_rejected()
    {
        Should.Throw<DistributedMapException.StronglyTypeConfigurationMustNotBeNull>(() =>
            Build(c => c.AddStronglyTypeIdConverter(null)));
    }
}
