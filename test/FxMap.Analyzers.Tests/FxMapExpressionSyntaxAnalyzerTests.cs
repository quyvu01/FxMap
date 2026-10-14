using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    FxMap.Analyzers.FxMapExpressionSyntaxAnalyzer>;

namespace FxMap.Analyzers.Tests;

public class FxMapExpressionSyntaxAnalyzerTests
{
    private const string TestSetup = """
                                     using System;

                                     public class TestResponse
                                     {
                                         public string CountryId { get; set; }
                                         public string CountryName { get; set; }
                                         public string ProvinceId { get; set; }
                                         public string ProvinceName { get; set; }
                                         public string UserId { get; set; }
                                         public string UserName { get; set; }
                                         public string OrderId { get; set; }
                                         public string OrderInfo { get; set; }
                                         public string Data { get; set; }
                                     }

                                     public class TestProfile
                                     {
                                         private void For<T>(Func<TestResponse, T> selector, string expression = null) { }
                                         private void Expression(string expression) { }
                                         private void Else(string expression) { }

                                         public void Configure()
                                         {
                                     """;

    private const string TestTeardown = """
                                            }
                                        }
                                        """;

    #region Valid Expressions

    [Fact]
    public async Task ValidExpression_SimpleProperty_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, "Country.Name");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_WithProjection_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, "Country.{Id, Name}");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_WithFilter_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, "Countries(Active = true)");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_RootProjection_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.Data, "{Id, Name, Country.Name as CountryName}");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_ForWithoutExpression_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName);
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_ExpressionMethod_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    Expression("Country.Name");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_ElseMethod_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    Else("Name");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidExpression_ComplexFilterIndexerProjection_NoDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, "Countries(Active = true):orderBy(Name).{Id, Name}");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region Invalid Expressions

    [Fact]
    public async Task InvalidExpression_MissingClosingParenthesisOfOrderBy_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.ProvinceName, {|#0:"Provinces:orderBy(Name"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Provinces:orderBy(Name", "Expected ')' after orderBy argument. Got 'EndOfExpression' at position 22");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_RemovedIndexer_ReportsDiagnosticWithTheFunctionsToUse()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.ProvinceName, {|#0:"Provinces[0 asc Name].Name"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Provinces[0 asc Name].Name",
                "The indexer '[...]' at position 9 is no longer supported (removed in 5.0). Use the functions: " +
                "'[asc Date]' is ':orderBy(Date)', '[0 asc Date]' is ':orderBy(Date):first', " +
                "'[-1 asc Date]' is ':orderBy(Date):last', '[5 10 desc Date]' is ':orderBy(Date desc):offset(5):limit(10)'");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_MissingClosingBrace_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, {|#0:"Country.{Id, Name"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Country.{Id, Name", "Expected '}' after projection. Got 'EndOfExpression' at position 17");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_MissingDotBeforeProjection_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, {|#0:"Country{Id, Name}"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Country{Id, Name}", "Projection requires '.' before '{' at position 7");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_UnknownFunction_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, {|#0:"Name:invalid"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Name:invalid", "Unknown function 'invalid' at position 5");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_ComputedExpressionRequiresAlias_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.CountryName, {|#0:"{Id, (Name:upper)}"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("{Id, (Name:upper)}",
                "Expected 'as' keyword after computed expression - alias is required. Got 'CloseBrace' at position 17");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_MissingDotAfterFilter_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.OrderInfo, {|#0:"Orders(Status = 'Done')Items"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Orders(Status = 'Done')Items",
                "Property navigation requires '.' before identifier at position 23");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_ExpressionMethod_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    Expression({|#0:"Country{Id, Name}"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Country{Id, Name}", "Projection requires '.' before '{' at position 7");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task InvalidExpression_ElseMethod_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    Else({|#0:"Name:invalid"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Name:invalid", "Unknown function 'invalid' at position 5");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    #endregion

    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        return VerifyCS.VerifyAnalyzerAsync(source, expected);
    }
}
