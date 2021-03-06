﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SecurityCodeScan.Analyzers.Locale;
using SecurityCodeScan.Analyzers.Utils;
using System.Collections.Immutable;
using CSharp = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace SecurityCodeScan.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WeakCertificateValidationAnalyzerCSharp : WeakCertificateValidationAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => VisitSyntaxNode(ctx, CSharpSyntaxNodeHelper.Default),
                                             CSharp.SyntaxKind.AddAssignmentExpression,
                                             CSharp.SyntaxKind.SimpleAssignmentExpression);
        }
    }

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class WeakCertificateValidationAnalyzerVisualBasic : WeakCertificateValidationAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => VisitSyntaxNode(ctx, VBSyntaxNodeHelper.Default),
                                             VB.SyntaxKind.AddAssignmentStatement,
                                             VB.SyntaxKind.SimpleAssignmentStatement);
        }
    }

    public abstract class WeakCertificateValidationAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = LocaleUtil.GetDescriptor("SCS0004");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected static void VisitSyntaxNode(SyntaxNodeAnalysisContext ctx, SyntaxNodeHelper nodeHelper)
        {
            var leftNode = nodeHelper.GetAssignmentLeftNode(ctx.Node);
            if (!nodeHelper.IsSimpleMemberAccessExpressionNode(leftNode))
                return;

            var symbolMemberAccess = ctx.SemanticModel.GetSymbolInfo(leftNode).Symbol;
            if (IsMatch(symbolMemberAccess))
            {
                var diagnostic = Diagnostic.Create(Rule, ctx.Node.GetLocation());
                ctx.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsMatch(ISymbol symbolMemberAccess)
        {
            return AnalyzerUtil.SymbolMatch(symbolMemberAccess,
                                            type: "ServicePointManager",
                                            name: "ServerCertificateValidationCallback") ||
                   AnalyzerUtil.SymbolMatch(symbolMemberAccess,
                                            type: "HttpWebRequest",
                                            name: "ServerCertificateValidationCallback") ||
                   AnalyzerUtil.SymbolMatch(symbolMemberAccess,
                                            type: "ServicePointManager",
                                            name: "CertificatePolicy");
        }
    }
}
