using System.Linq.Expressions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Delegates;
using FxMap.Expressions.Building;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Expressions;

/// <summary>The projection takes its id and default property from selector lambdas, as <c>Id(x => ...)</c> declares them.</summary>
public class ProjectionBuilderSelectorTests
{
    public class Staff
    {
        public string Code { get; set; } = "";
        public string Facility { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
    }

    private static readonly GetTypeAccessor Accessors = type => new TypeAccessor(type, _ => null!);

    private static readonly Staff Anna = new() { Code = "A", Facility = "F1", Name = "Anna", Level = 3 };

    private static object[] Run(ProjectionBuilder<Staff> builder, params string[] expressions) =>
        builder.Build(expressions!).Compile()(Anna);

    private static ProjectionBuilder<Staff> Of(Expression<Func<Staff, object>> id,
        Expression<Func<Staff, object>> @default = null) => new(id, @default!, Accessors);

    [Fact]
    public void A_property_selector_gives_the_id_as_the_first_value()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, string>>)(x => x.Code), null!, Accessors);

        var row = Run(builder, "Name");

        row.ShouldBe(["A", "Anna"]);
    }

    [Fact]
    public void A_computed_selector_gives_a_computed_id()
    {
        var builder = new ProjectionBuilder<Staff>(
            (Expression<Func<Staff, string>>)(x => x.Code + ":" + x.Facility), null!, Accessors);

        Run(builder, "Name")[0].ShouldBe("A:F1");
    }

    [Fact]
    public void The_default_property_selector_answers_a_null_expression()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, string>>)(x => x.Code),
            (Expression<Func<Staff, string>>)(x => x.Name), Accessors);

        Run(builder, [null, "Facility"]).ShouldBe(["A", "Anna", "F1"]);
    }

    [Fact]
    public void A_computed_default_property_works_too()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, string>>)(x => x.Code),
            (Expression<Func<Staff, string>>)(x => x.Name + " L" + x.Level), Accessors);

        Run(builder, [null]).ShouldBe(["A", "Anna L3"]);
    }

    [Fact]
    public void Without_a_default_property_a_null_expression_gives_null()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, string>>)(x => x.Code), null!, Accessors);

        Run(builder, [null]).ShouldBe(["A", null]);
    }

    [Fact]
    public void Value_type_ids_are_boxed()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, int>>)(x => x.Level), null!, Accessors);

        Run(builder, "Name")[0].ShouldBe(3);
    }

    [Fact]
    public void Metadata_is_built_with_selectors_as_well()
    {
        var builder = new ProjectionBuilder<Staff>((Expression<Func<Staff, string>>)(x => x.Code),
            (Expression<Func<Staff, string>>)(x => x.Name), Accessors);

        var result = builder.BuildWithMetadata([null, "Facility"]);

        result.Projection.Compile()(Anna).ShouldBe(["A", "Anna", "F1"]);
        result.Metadata.Select(m => m.IsId).ShouldBe([true, false, false]);
    }

    [Fact]
    public void The_projection_uses_one_parameter_so_it_can_be_translated()
    {
        var builder = new ProjectionBuilder<Staff>(
            (Expression<Func<Staff, string>>)(x => x.Code + x.Facility),
            (Expression<Func<Staff, string>>)(x => x.Name), Accessors);

        var projection = builder.Build([null, "Facility"]);

        projection.Parameters.ShouldHaveSingleItem();
        new ParameterCounter().Count(projection).ShouldBe(1, "every parameter use refers to the lambda parameter");
    }

    private sealed class ParameterCounter : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _seen = [];

        public int Count(Expression expression)
        {
            Visit(expression);
            return _seen.Count;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _seen.Add(node);
            return node;
        }
    }
}
