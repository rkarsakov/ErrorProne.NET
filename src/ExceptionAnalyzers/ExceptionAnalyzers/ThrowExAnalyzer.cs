﻿using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Exceptions
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ThrowExAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.IncorrectExceptionPropagation;

        internal const string Title = "Incorrect exception propagation";
        public const string MessageFormat = "Incorrect exception propagation. Use throw; instead.";
        internal const string Category = "CodeSmell";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        // Called when Roslyn encounters a catch clause.
        private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = context.Node as CatchClauseSyntax;
            if (catchClause?.Declaration == null)
            {
                return;
            }

            // Looking for "ex" in "catch(Exception ex)"
            var exceptionDeclarationIdentifier = catchClause.Declaration.Identifier;

            // Exception identifier is optional in catch clause. It could be "catch(Exception)"
            if (exceptionDeclarationIdentifier.Kind() == SyntaxKind.None)
            {
                return;
            }

            foreach (var throwStatement in catchClause.DescendantNodes().OfType<ThrowStatementSyntax>())
            {
                var identifier =
                    throwStatement.Expression as IdentifierNameSyntax;

                if (identifier == null)
                    continue;

                var symbol = context.SemanticModel.GetSymbolInfo(identifier);
                if (symbol.Symbol == null)
                    continue;

                if (symbol.Symbol.ExceptionFromCatchBlock())
                {
                    // throw ex; detected!
                    var diagnostic = Diagnostic.Create(Rule, identifier.GetLocation());

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
