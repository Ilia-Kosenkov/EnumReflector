using System;
using System.Collections.Generic;
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
        public const string EmittedClassName = @"EnumExtensions";
        public const string AttributeShortTypeName = @"ReflectEnum";

        private static readonly SymbolDisplayFormat FullTypeFormat = new SymbolDisplayFormat(
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




        // Can be used to switch off nullable support?
        private string NullableSymbol { get; } = "?";
        private string NullableCoerceSymbol { get; } = "!";


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

            //foreach (var (enumItem, _) in preProcessedEnums)
            //{
            //    var proc = new EnumProcessor(enumItem, compilation);
            //    var src = GenerateEnumExtension(proc);
            //    context.AddSource(proc.EnumType.Name, src);
            //}


            context.AddSource(nameof(GenerateAllEnumValues), GenerateAllEnumValues(preProcessedEnums));
            context.AddSource(nameof(GenerateAllEnumNames), GenerateAllEnumNames(preProcessedEnums));
            context.AddSource(nameof(GenerateTryParseEnum), GenerateTryParseEnum(preProcessedEnums));

        }

        private SourceText GenerateTryParseEnum(
            IEnumerable<(EnumDeclarationSyntax EnumDeclaration, EnumProcessor Processor)> input)
        {
            var sb = new StringBuilder();
            AddPartialClassHeader(sb);
            sb.AppendLineTabbed($"public static bool TryParseEnum<T>({typeof(string).FullName} name, out T result) where T : {typeof(Enum).FullName}", 2);
            
            // Method body scope
            using (new ScopeWriter(sb, 2))
            {
                sb.AppendLineTabbed($"if ({typeof(string).FullName}.{nameof(string.IsNullOrWhiteSpace)}(name))", 3);
                sb.AppendLineTabbed($"throw new {typeof(ArgumentNullException).FullName}(nameof(name));", 4);

                sb.AppendLineTabbed($"var values = {AttributeNamespace}.{EmittedClassName}.GetEnumValues<T>();", 3);
                sb.AppendLineTabbed($"result = default(T){NullableCoerceSymbol};", 3);
                sb.AppendLineTabbed("foreach (var item in values)", 3);
                sb.AppendLineTabbed($"if (item.Item1.Equals(name, {typeof(StringComparison).FullName}.{nameof(StringComparison.Ordinal)}))", 4);
                // If condition scope
                using (new ScopeWriter(sb, 4))
                {
                    sb.AppendLineTabbed("result = item.Item2;", 5);
                    sb.AppendLineTabbed("return true;", 5);
                }

                sb.AppendLineTabbed("return false;", 3);
            }
            AddPartialClassFooter(sb);
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }
        private SourceText GenerateAllEnumNames(
            IEnumerable<(EnumDeclarationSyntax EnumDeclaration, EnumProcessor Processor)> input)
        {
            var sb = new StringBuilder();
            AddPartialClassHeader(sb);
            sb.AppendLineTabbed(
                $"public static {typeof(string).FullName}{NullableSymbol} GetEnumName<T>(this T @this) where T : {typeof(Enum).FullName}",
                2);
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
                        sb.AppendLineTabbed($"var value = ({fullEnumType})({typeof(object).FullName})@this;", 4);
                        foreach (var member in members)
                        {
                            var memberName = member.MemberName.ToDisplayString(FullTypeFormat);
                            sb.AppendLineTabbed($"if (value == {fullEnumType}.{memberName}) return \"{memberName}\";", 4);
                        }

                        sb.AppendLineTabbed("return null;", 4);
                    }
                }

                sb.AppendLineTabbed($"throw new {typeof(NotSupportedException).FullName}(\"Only enums declared with [{AttributeFullName}] are supported.\");", 3);

            }

            AddPartialClassFooter(sb);

            return SourceText.From(sb.ToString(), Encoding.UTF8);

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


        private static void AddPartialClassHeader(StringBuilder sb)
        {
            sb.AppendLineTabbed($"namespace {AttributeNamespace}");
            sb.AppendLineTabbed("{");
            sb.AppendLineTabbed($"public static partial class {EmittedClassName}", 1);
            sb.AppendLineTabbed("{", 1);
        }

        private static void AddPartialClassFooter(StringBuilder sb)
        {
            sb.AppendLineTabbed("}", 1);
            sb.AppendLineTabbed("}");
        }
    }
}