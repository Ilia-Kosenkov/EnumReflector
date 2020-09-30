using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EnumReflector
{
    internal sealed class EnumProcessor
    {
        private readonly EnumDeclarationSyntax _syntax;
        private readonly Compilation _compilation;

        private IReadOnlyDictionary<INamedTypeSymbol, SpecialType> NamedTypeMap { get; }


        public EnumProcessor(EnumDeclarationSyntax syntax, Compilation compilation)
        {
            (_syntax, _compilation) = (syntax, compilation);
            NamedTypeMap = new List<SpecialType>
            {
                SpecialType.System_Byte,
                SpecialType.System_SByte,
                SpecialType.System_Int16,
                SpecialType.System_UInt16,
                SpecialType.System_Int32,
                SpecialType.System_UInt32,
                SpecialType.System_Int64,
                SpecialType.System_UInt64
            }.ToDictionary(_compilation.GetSpecialType);
        }

        public INamedTypeSymbol BaseType =>
            EnumType.EnumUnderlyingType ?? throw new InvalidCastException("Failed to resolve type info from the syntax tree");

        public Type ActualBaseType =>
            NamedTypeMap.TryGetValue(BaseType, out var specType)
            && Extensions.SpecialTypeMap.TryGetValue(specType, out var type)
                ? type
                : throw new InvalidOperationException("Failed to match type symbol");

        public INamedTypeSymbol EnumType
        {
            get
            {
                var model = _compilation.GetSemanticModel(_syntax.SyntaxTree);

                return model.GetDeclaredSymbol(_syntax) as INamedTypeSymbol ?? throw new InvalidCastException("Failed to resolve type info from the syntax tree");
            }
        }

        public IReadOnlyList<EnumKeyValue> Process() => VerifyEnumMemberValues(EnumerateEnumMembers());

        private IReadOnlyList<EnumKeyValue> EnumerateEnumMembers() =>
            _syntax.Members.Select(x =>
            {
                var model = _compilation.GetSemanticModel(x.SyntaxTree);
                var symb = model.GetDeclaredSymbol(x);
                var val = x.EqualsValue?.Value switch
                {
                    { } expr => model.GetConstantValue(expr) switch
                    {
                        {HasValue: true, Value: var innerVal} => Convert.ChangeType(innerVal, ActualBaseType),
                        _ => Activator.CreateInstance(ActualBaseType)
                    },
                    _ => default
                };

                return new EnumKeyValue(symb, val);
            })
            .ToList();

        private IReadOnlyList<EnumKeyValue> VerifyEnumMemberValues(IReadOnlyList<EnumKeyValue> input)
        {

            var result = new List<EnumKeyValue>(input.Count);

            var prevVal = input[0] switch
            {
                {Value: null} x => x.WithValue(Activator.CreateInstance(ActualBaseType)),
                {} x => x
            };
            result.Add(prevVal);

            for (var i = 1; i < input.Count; i++)
            {
                var nextVal = input[i] switch
                {
                    {Value : null} x => x.WithValue(Convert.ChangeType(Extensions.IncrementBy(prevVal.Value ?? 0, 1), ActualBaseType)),
                    {} x => x
                };
                result.Add(nextVal);
                prevVal = nextVal;
            }

            return result;
        }
    }
}
