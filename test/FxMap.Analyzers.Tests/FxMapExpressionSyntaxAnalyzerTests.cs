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
                    For<string>(x => x.CountryName, "Countries(Active = true)[asc Name].{Id, Name}");
            """ + TestTeardown;

        await VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region Invalid Expressions

    [Fact]
    public async Task InvalidExpression_MissingClosingBracket_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.ProvinceName, {|#0:"Provinces[asc Name"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Provinces[asc Name", "Expected ']' after indexer. Got 'EndOfExpression' at position 18");

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
    public async Task InvalidExpression_MissingDotAfterIndexer_ReportsDiagnostic()
    {
        var testCode = TestSetup + """
                    For<string>(x => x.ProvinceName, {|#0:"Provinces[0 asc Name]Name"|});
            """ + TestTeardown;

        var expected = VerifyCS.Diagnostic(FxMapExpressionSyntaxAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Provinces[0 asc Name]Name",
                "Property navigation requires '.' before identifier at position 21");

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
