﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace NextGenMapper
{
    public class TypeMapping
    {
        public ITypeSymbol From { get; private set; }
        public ITypeSymbol To { get; private set; }
        public List<PropertyMapping> Properties { get; private set; }
        public MethodDeclarationSyntax Method { get; private set; }
        public MappingType Type { get; private set; }
        public bool IsConstructorMapping { get; private set; }

        public string FromType => From.ToDisplayString();
        public string ToType => To.ToDisplayString();
        public ArrowExpressionClauseSyntax ExpressionBody => Method.ExpressionBody;
        public BlockSyntax Body => Method.Body;
        public MethodType MethodType => ExpressionBody != null ? MethodType.Expression : MethodType.Block;
        public string ParameterName => Method.ParameterList.Parameters.First().Identifier.Text;

        private TypeMapping() 
        { }

        public static TypeMapping CreateCommon(ITypeSymbol from, ITypeSymbol to, List<PropertyMapping> properties, bool isConstructorMapping)
            => new()
            {
                From = from.ThrowIfNull(),
                To = to.ThrowIfNull(),
                Properties = properties.ThrowIfNull(),
                Type = MappingType.Common,
                IsConstructorMapping = isConstructorMapping
            };

        public static TypeMapping CreateCustom(ITypeSymbol from, ITypeSymbol to, MethodDeclarationSyntax method)
            => new()
            {
                From = from.ThrowIfNull(),
                To = to.ThrowIfNull(),
                Method = method.ThrowIfNotValid(),
                Type = MappingType.Custom
            };

        public static TypeMapping CreatePartial(ITypeSymbol from, ITypeSymbol to, List<PropertyMapping> properties, MethodDeclarationSyntax method, bool isConstructorMapping)
            => new()
            {
                From = from.ThrowIfNull(),
                To = to.ThrowIfNull(),
                Properties = properties.ThrowIfNull(),
                Method = method.ThrowIfNotValid(),
                Type = MappingType.Partial,
                IsConstructorMapping = isConstructorMapping
            };

        public override bool Equals(object obj)
        {
            return obj is TypeMapping mapping &&
                From.Equals(mapping.From, SymbolEqualityComparer.IncludeNullability) &&
                To.Equals(mapping.To, SymbolEqualityComparer.IncludeNullability);
        }

        public override int GetHashCode()
        {
            int hashCode = -1308899859;
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol>.Default.GetHashCode(From);
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol>.Default.GetHashCode(To);
            return hashCode;
        }

        public bool Equal(ITypeSymbol from, ITypeSymbol to)
            => From.Equals(from, SymbolEqualityComparer.IncludeNullability)
            && To.Equals(to, SymbolEqualityComparer.IncludeNullability);
    }
}
