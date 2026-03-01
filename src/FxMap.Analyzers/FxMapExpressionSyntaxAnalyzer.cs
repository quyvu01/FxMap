using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using FxMap.Expressions.Parsing;

namespace FxMap.Analyzers;

/// <summary>
/// Roslyn analyzer that validates FxMap expression syntax at compile-time.
/// Detects expression strings passed to FluentAPI methods: .For(), .Expression(), and .Else().
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FxMapExpressionSyntaxAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OFX001";
    private const string Category = "Syntax";

    private static readonly LocalizableString Title = "FxMap Expression syntax is invalid";
    private static readonly LocalizableString MessageFormat = "Expression '{0}' is invalid: {1}";

    private static readonly LocalizableString Description =
        "FxMap Expression must follow valid syntax rules including balanced brackets, braces, parentheses, and proper use of operators.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var methodName = GetMethodName(invocation);
        if (methodName is null) return;

        // Determine which argument contains the expression string
        var expressionLiteral = methodName switch
        {
            // .For(x => x.Prop, "expression") — second argument
            "For" => GetStringLiteralArgument(invocation, argumentIndex: 1),
            // .Expression("expression") or .Else("expression") — first argument
            "Expression" or "Else" => GetStringLiteralArgument(invocation, argumentIndex: 0),
            _ => null
        };

        if (expressionLiteral is null) return;

        var expressionValue = expressionLiteral.Token.ValueText;
        if (string.IsNullOrWhiteSpace(expressionValue)) return;

        var validationError = ValidateExpressionSyntax(expressionValue);
        if (validationError is null) return;

        var diagnostic = Diagnostic.Create(Rule, expressionLiteral.GetLocation(), expressionValue, validationError);
        context.ReportDiagnostic(diagnostic);
    }

    private static string GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => null
        };
    }

    private static LiteralExpressionSyntax GetStringLiteralArgument(
        InvocationExpressionSyntax invocation, int argumentIndex)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args.Value.Count <= argumentIndex) return null;

        return args.Value[argumentIndex].Expression as LiteralExpressionSyntax;
    }

    /// <summary>
    /// Validate expression syntax using ExpressionParser.
    /// </summary>
    private static string ValidateExpressionSyntax(string expression)
    {
        try
        {
            ExpressionParser.Parse(expression);
            return null;
        }
        catch (ExpressionParseException ex)
        {
            return ex.Message;
        }
        catch (Exception ex)
        {
            return $"Unexpected error: {ex.Message}";
        }
    }
}
