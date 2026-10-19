using System.Reflection;
using FxMap.Exceptions;
using FxMap.Responses;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// The exceptions of FxMap are the answer to a mistake of configuration or of use: their message must name what was wrong.
/// Each one is built with recognizable arguments, and the message must show every one of them.
/// </summary>
public class DistributedMapExceptionTests
{
    private sealed class SampleModel;

    private sealed class OtherModel;

    private static readonly Type[] Exceptions = typeof(DistributedMapException).GetNestedTypes()
        .Where(t => typeof(Exception).IsAssignableFrom(t)).OrderBy(t => t.Name).ToArray();

    public static IEnumerable<object[]> AllExceptions() => Exceptions.Select(t => new object[] { t.Name });

    private static object ArgumentFor(ParameterInfo parameter, int index) =>
        parameter.ParameterType == typeof(string) ? $"ARG{index}"
        : parameter.ParameterType == typeof(Type) ? (index % 2 == 0 ? typeof(SampleModel) : typeof(OtherModel))
        : parameter.ParameterType == typeof(Type[]) ? new[] { typeof(SampleModel), typeof(OtherModel) }
        : parameter.ParameterType == typeof(ExpressionError[]) ? new[] { new ExpressionError("Expr1", "ARG-ERROR1") }
        : throw new NotSupportedException($"No sample for {parameter.ParameterType} in {parameter.Member.DeclaringType!.Name}");

    [Fact]
    public void There_are_exceptions_to_check()
    {
        Exceptions.Length.ShouldBeGreaterThan(20);
    }

    [Theory]
    [MemberData(nameof(AllExceptions))]
    public void The_message_is_not_empty_and_shows_the_arguments(string name)
    {
        var type = Exceptions.Single(t => t.Name == name);
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = constructor.GetParameters();
        var arguments = parameters.Select(ArgumentFor).ToArray();

        var exception = (Exception)constructor.Invoke(arguments);

        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldNotContain("{", customMessage: "a placeholder that was not filled in");
        // A name that is the same as its type tells nothing: the message states the mistake
        foreach (var argument in arguments)
        {
            switch (argument)
            {
                case string text:
                    exception.Message.ShouldContain(text);
                    break;
                case Type t:
                    (exception.Message.Contains(t.Name) || exception.Message.Contains(t.FullName!)).ShouldBeTrue(
                        $"{name} does not name the type {t.Name}: {exception.Message}");
                    break;
                case Type[] types:
                    foreach (var t in types)
                        (exception.Message.Contains(t.Name) || exception.Message.Contains(t.FullName!)).ShouldBeTrue(
                            $"{name} does not name the type {t.Name}: {exception.Message}");
                    break;
                case ExpressionError[] errors:
                    exception.Message.ShouldContain(errors[0].Message);
                    break;
            }
        }
    }

    [Fact]
    public void The_distributed_key_must_be_either_a_type_or_a_name_and_the_message_says_which_case()
    {
        new DistributedMapException.InvalidDistributedKeyConfiguration(typeof(SampleModel), "Key").Message
            .ShouldContain("Cannot set both");
        new DistributedMapException.InvalidDistributedKeyConfiguration(null, null).Message
            .ShouldContain("Either DistributedKeyType or DistributedKey must be set");
        new DistributedMapException.InvalidDistributedKeyConfiguration(null, " ").Message
            .ShouldContain("Either DistributedKeyType or DistributedKey must be set");
    }

    [Fact]
    public void The_expressions_that_failed_are_kept_for_the_caller()
    {
        var errors = new[] { new ExpressionError("A:b", "bad A"), new ExpressionError("C", "bad C") };

        var exception = new DistributedMapException.ExpressionsFailed(typeof(SampleModel), errors);

        exception.Errors.ShouldBe(errors);
        exception.Message.ShouldContain("2 expression(s)");
        exception.Message.ShouldContain("bad A | bad C");
    }
}
