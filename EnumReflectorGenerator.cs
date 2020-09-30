﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EnumReflector
{
    [Generator]
    public class EnumReflectorGenerator : ISourceGenerator
    {
        public const string AttributeNamespace = @"EnumReflector";
        public const string AttributeShortTypeName = @"ReflectEnum";

        private static SymbolDisplayFormat FullTypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
        public static string AttributeTypeName { get; } = @$"{AttributeShortTypeName}Attribute";
        public static string AttributeFullName { get; } = $"{AttributeNamespace}.{AttributeTypeName}";

        private static string AttributeText { get; } = $@"
using System;
namespace {AttributeNamespace}
{{
    [AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
    sealed class {AttributeTypeName} : Attribute
    {{
        public {AttributeTypeName}()
        {{
        }}
    }}
}}
";



        public void Execute(SourceGeneratorContext context)
        {
            context.AddSource(AttributeFullName, SourceText.From(AttributeText, Encoding.UTF8));

            // TODO : Improve
            CSharpParseOptions options = (context.Compilation as CSharpCompilation)?.SyntaxTrees[0]?.Options as CSharpParseOptions ?? throw new InvalidOperationException();
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributeText, Encoding.UTF8), options));

            if (!(context.SyntaxReceiver is EnumSyntaxReceiver receiver))
                return;

            var attributeSymbol = compilation.GetTypeByMetadataName(AttributeFullName) 
                                  ?? throw new NullReferenceException($@"[{AttributeFullName}] is missing from the compilation");

            var eligibleEnums = receiver.EnumCandidates
                .Where(enumItem =>
                {
                    var model = compilation.GetSemanticModel(enumItem.SyntaxTree);
                    var symb = model.GetDeclaredSymbol(enumItem);
                    return symb is not null && symb.GetAttributes().Any(x =>
                        x.AttributeClass?.Equals(attributeSymbol, SymbolEqualityComparer.Default) == true);
                });

            var preProcessedEnums = eligibleEnums
                .Select(x => (EnumDeclaration: x, Processor: new EnumProcessor(x, compilation)))
                .ToList();

            foreach (var (enumItem, _) in preProcessedEnums)
            {
                var proc = new EnumProcessor(enumItem, compilation);
                var src = GenerateEnumExtension(proc);
                context.AddSource(proc.EnumType.Name, src);
            }


            context.AddSource(nameof(GenerateAllEnumValues), GenerateAllEnumValues(preProcessedEnums));
        }

        

        private SourceText GenerateAllEnumValues(IEnumerable<(EnumDeclarationSyntax EnumDeclaration, EnumProcessor Processor)> input)
        {
            var sb = new StringBuilder();
            AddPartialClassHeader(sb);
            sb.AppendLineTabbed($"public static ({typeof(string).FullName} Name, T Value)[] GetEnumValues<T>() where T : {typeof(Enum).FullName}", 2);
            // Method body scope
            using (new ScopeWriter(sb, 2))
            {
                foreach (var (_, proc) in input)
                {
                    var fullEnumType = proc.EnumType.ToDisplayString(FullTypeFormat);
                    var members = proc.Process();
                    sb.AppendLineTabbed($"if (typeof(T) == typeof({fullEnumType}))", 3);
                    // If condition scope
                    // ReSharper disable once ConvertToUsingDeclaration
                    using (new ScopeWriter(sb, 3))
                    {
                        sb.AppendLineTabbed($"return (({typeof(string).FullName} Name, T Value)[])({typeof(object).FullName}) " + $"new ({typeof(string).FullName} Name, {fullEnumType} Value)[]", 4);
                        // Array initialization scope
                        using (new ScopeWriter(sb, 4, true))
                        {
                            foreach (var member in members)
                            {
                                var memberName = member.MemberName.ToDisplayString(FullTypeFormat);
                                sb.AppendLineTabbed($"(\"{memberName}\", {fullEnumType}.{memberName}),", 5);
                            }
                        }
                    }
                }

                sb.AppendLineTabbed($"throw new {typeof(NotSupportedException).FullName}(\"Only enums declared with [{AttributeFullName}] are supported.\");", 3);
            }

            AddPartialClassFooter(sb);

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        private static SourceText GenerateEnumExtension(EnumProcessor proc)
        {
            var fullEnumType = proc.EnumType.ToDisplayString(FullTypeFormat);
            var sb = new StringBuilder();
            AddPartialClassHeader(sb);

            sb.AppendLineTabbed($"public static string GetEnumName(this {fullEnumType} @this)", 2);
            // Method body scope
            using (var _ = new ScopeWriter(sb, 2))
            {
                foreach (var item in proc.Process()) sb.AppendLineTabbed($"if (@this == {fullEnumType}.{item.MemberName.ToDisplayString(FullTypeFormat)}) return\"{item.MemberName.Name}\";", 3);

                sb.AppendLineTabbed($"throw new {typeof(Exception).FullName}(\"Undefined enum value.\");", 3);
            }


            AddPartialClassFooter(sb);

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new EnumSyntaxReceiver(AttributeNamespace, AttributeShortTypeName));
        }

      

        private void EmitExtensionForEnum(EnumProcessor proc)
        {

        }

        private static void AddPartialClassHeader(StringBuilder sb)
        {
            sb.AppendLineTabbed("namespace EnumReflector");
            sb.AppendLineTabbed("{");
            sb.AppendLineTabbed("public static partial class EnumExtensions", 2);
            sb.AppendLineTabbed("{", 1);
        }

        private static void AddPartialClassFooter(StringBuilder sb)
        {
            sb.AppendLineTabbed("}", 1);
            sb.AppendLineTabbed("}");
        }
    }
}