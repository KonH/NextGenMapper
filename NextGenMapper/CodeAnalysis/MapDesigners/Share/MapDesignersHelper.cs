﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextGenMapper.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NextGenMapper.CodeAnalysis.MapDesigners
{
    public static class MapDesignersHelper
    {
        public static ObjectCreationExpressionSyntax? GetObjectCreateionExpression(this BaseMethodDeclarationSyntax method)
        {
            var objCreationExpression = method.ExpressionBody != null
                ? method.ExpressionBody?.Expression as ObjectCreationExpressionSyntax
                : method.Body?.Statements.OfType<ReturnStatementSyntax>().Last().Expression as ObjectCreationExpressionSyntax;

            return objCreationExpression;
        }

        public static List<ISymbol> GetPropertiesInitializedByConstructorAndInitializer(this IMethodSymbol constructor)
        {
            var constructorParametersNames = constructor.GetParametersNames().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            var initializerProperties = constructor.ContainingType
                .GetProperties()
                .Where(x => !x.IsReadOnly && !constructorParametersNames.Contains(x.Name));
            var members = constructor.GetParameters().Cast<ISymbol>().Concat(initializerProperties).ToList();

            return members;
        }
    }
}
