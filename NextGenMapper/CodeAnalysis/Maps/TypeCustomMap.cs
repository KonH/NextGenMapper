﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NextGenMapper.CodeAnalysis.Maps
{
    public sealed class TypeCustomMap : TypeMap
    {
        public ArrowExpressionClauseSyntax? ExpressionBody { get; }
        public BlockSyntax? Body { get; }
        public MethodBodyType MethodType { get; }
        public string ParameterName { get; }

        public TypeCustomMap(ITypeSymbol from, ITypeSymbol to, MethodDeclarationSyntax method)
            : base(from, to)
        {
            ExpressionBody = method.ExpressionBody;
            Body = method.Body;
            MethodType = method.Body is null ? MethodBodyType.Expression : MethodBodyType.Block;
            ParameterName = method.ParameterList.Parameters.First().Identifier.Text;
        }
    }
}
