﻿using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SecurityCodeScan.Analyzers.Locale;
using SecurityCodeScan.Analyzers.Utils;
using CSharp = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace SecurityCodeScan.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnsafeDeserializationAnalyzerCSharp : UnsafeDeserializationAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => VisitAttributeArgument(ctx, CSharpSyntaxNodeHelper.Default), CSharp.SyntaxKind.AttributeArgument);
            context.RegisterSyntaxNodeAction(ctx => VisitAssignment(ctx, CSharpSyntaxNodeHelper.Default),        CSharp.SyntaxKind.SimpleAssignmentExpression);
            context.RegisterSyntaxNodeAction(ctx => VisitObjectCreation(ctx, CSharpSyntaxNodeHelper.Default),    CSharp.SyntaxKind.ObjectCreationExpression);
        }
    }

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class UnsafeDeserializationAnalyzerVisualBasic : UnsafeDeserializationAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => VisitAttributeArgument(ctx, VBSyntaxNodeHelper.Default),     VB.SyntaxKind.SimpleArgument);
            context.RegisterSyntaxNodeAction(ctx => VisitAssignment(ctx, VBSyntaxNodeHelper.Default),            VB.SyntaxKind.SimpleAssignmentStatement);
            context.RegisterSyntaxNodeAction(ctx => VisitAssignment(ctx, VBSyntaxNodeHelper.Default),            VB.SyntaxKind.NamedFieldInitializer);
            context.RegisterSyntaxNodeAction(ctx => VisitObjectCreation(ctx, VBSyntaxNodeHelper.Default),        VB.SyntaxKind.ObjectCreationExpression);
        }

    }

    public abstract class UnsafeDeserializationAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = LocaleUtil.GetDescriptor("SCS0028", "title_analyzer");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected void VisitAttributeArgument(SyntaxNodeAnalysisContext ctx, SyntaxNodeHelper nodeHelper)
        {
            if (!nodeHelper.IsAttributeArgument(ctx.Node))
                return;

            var name = nodeHelper.GetAttributeArgumentNode(ctx.Node);

            if (name == null)
                return;

            if (!name.ToString().EndsWith("TypeNameHandling"))
                return;

            var symbols = ctx.SemanticModel.GetSymbolInfo(name).Symbol;
            if (symbols == null)
                return;

            if (symbols.ContainingSymbol.ToString() != "Newtonsoft.Json.JsonPropertyAttribute")
                return;

            ReportIfTypeNameHandlingIsNotNone(ctx, nodeHelper.GetAttributeArgumentExpresionNode(ctx.Node));
        }

        protected void VisitAssignment(SyntaxNodeAnalysisContext ctx, SyntaxNodeHelper nodeHelper)
        {
            var leftNode = nodeHelper.GetAssignmentLeftNode(ctx.Node);

            if (!leftNode.ToString().EndsWith("TypeNameHandling"))
                return;

            var symbols = ctx.SemanticModel.GetSymbolInfo(leftNode).Symbol;
            if (symbols == null)
                return;

            if (symbols.ContainingSymbol.ToString() != "Newtonsoft.Json.JsonSerializerSettings")
                return;

            ReportIfTypeNameHandlingIsNotNone(ctx, nodeHelper.GetAssignmentRightNode(ctx.Node));
        }

        private void ReportIfTypeNameHandlingIsNotNone(SyntaxNodeAnalysisContext ctx, SyntaxNode expression)
        {
            var value = ctx.SemanticModel.GetConstantValue(expression);

            if (!value.HasValue)
                return;

            //check if it is really integer, because visual basic allows to assign string values to enums
            if (value.Value is int intValue && intValue != 0 /*TypeNameHandling.None*/ )
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation()));
        }

        protected void VisitObjectCreation(SyntaxNodeAnalysisContext ctx, SyntaxNodeHelper nodeHelper)
        {
            var creationSymbols = ctx.SemanticModel.GetSymbolInfo(ctx.Node).Symbol;
            if (creationSymbols == null || creationSymbols.ContainingSymbol.ToString() != "System.Web.Script.Serialization.JavaScriptSerializer")
                return;

            var arguments = nodeHelper.GetObjectCreationArgumentExpressionNodes(ctx.Node);

            //if there is argument it means JavaScriptSerializer(JavaScriptTypeResolver) constructor is used
            var firstArgument = arguments.FirstOrDefault();
            if (firstArgument == null)
                return;

            if (ctx.SemanticModel.GetSymbolInfo(firstArgument).Symbol != null)
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Node.GetLocation()));
        }
    }
}
