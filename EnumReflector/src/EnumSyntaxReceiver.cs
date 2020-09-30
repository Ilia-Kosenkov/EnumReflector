using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EnumReflector
{
    internal class EnumSyntaxReceiver : ISyntaxReceiver
    {
        private readonly List<EnumDeclarationSyntax> _enumCandidates = new List<EnumDeclarationSyntax>();

        public string Namespace { get; } 
        public string TypeName { get; }

        public IReadOnlyList<EnumDeclarationSyntax> EnumCandidates => _enumCandidates;

        public EnumSyntaxReceiver(string @namespace, string typeName) => (Namespace, TypeName) = 
            string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(typeName)
                ? throw new ArgumentException()
                : (@namespace, typeName);

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (!(syntaxNode is EnumDeclarationSyntax enumSyntax)) return;

            var attrs = enumSyntax.AttributeLists
                .SelectMany(x => x.Attributes, (_, attributeSyntax) => attributeSyntax).ToList();

            var attr = attrs.FirstOrDefault(x =>
            {
                var fullStr = x.Name.ToFullString();
                return fullStr.Equals(@$"{Namespace}.{TypeName}", StringComparison.Ordinal)
                     || fullStr.Equals(@$"{Namespace}.{TypeName}Attribute", StringComparison.Ordinal)
                     || fullStr.Equals(TypeName, StringComparison.Ordinal)
                     || fullStr.Equals(@$"{TypeName}Attribute", StringComparison.Ordinal);
            });

            if(attr is not null)
                _enumCandidates.Add(enumSyntax);
        }
    }
}
