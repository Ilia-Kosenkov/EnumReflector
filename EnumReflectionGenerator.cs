using Microsoft.CodeAnalysis;
using System;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace EnumReflector
{
    //https://github.com/dotnet/roslyn-sdk/blob/ecc9e9cd950fec9417a361c43232470d8246a2e0/samples/CSharp/SourceGenerators/SourceGeneratorSamples/AutoNotifyGenerator.cs#L10
    [Generator]
    public class EnumReflectionGenerator : ISourceGenerator
    {
        private const string AttributeText = @"
using System;
namespace EnumReflector
{
    [AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
    sealed class ReflectEnumAttribute : Attribute
    {
        public ReflectEnumAttribute()
        {
        }
    }
}
";

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new EnumReflectionSyntaxReceiver());
        }

        public void Execute(SourceGeneratorContext context)
        {
            context.AddSource("AutoNotifyAttribute", SourceText.From(AttributeText, Encoding.UTF8));
            CSharpParseOptions options = (context.Compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions ??  throw new InvalidOperationException();
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributeText, Encoding.UTF8), options));

            var receiver = context.SyntaxReceiver as EnumReflectionSyntaxReceiver
                ?? throw new NullReferenceException($"{nameof(context.SyntaxReceiver)} is {null}");
        }
    }
}
