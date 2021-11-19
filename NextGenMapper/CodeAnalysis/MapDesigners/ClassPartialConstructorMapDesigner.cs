﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextGenMapper.CodeAnalysis.Maps;
using NextGenMapper.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;


namespace NextGenMapper.CodeAnalysis.MapDesigners
{
    public class ClassPartialConstructorMapDesigner
    {
        private readonly ClassMapDesigner _classMapDesigner;
        private readonly DiagnosticReporter _diagnosticReporter;

        public ClassPartialConstructorMapDesigner(DiagnosticReporter diagnosticReporter)
        {
            _classMapDesigner = new(diagnosticReporter);
            _diagnosticReporter = diagnosticReporter;
        }

        public List<ClassMap> DesignMapsForPlanner(ITypeSymbol from, ITypeSymbol to, IMethodSymbol constructor, MethodDeclarationSyntax methodSyntax)
        {
            var objCreationExpression = methodSyntax.GetObjectCreateionExpression();
            if (objCreationExpression == null)
            {
                _diagnosticReporter.ReportObjectCreationExpressionNotFoundError(methodSyntax.GetLocation(), from, to);
                return new();
            }

            var argumentByParameterName = objCreationExpression.ArgumentList?.Arguments
                .Where(x => !x.IsDefaultLiteralExpression())
                .Select(x => new { Argument = x, ParameterName = GetConstructorParameter(constructor, x).Name })
                .ToDictionary(x => x.ParameterName, x => x.Argument, StringComparer.InvariantCultureIgnoreCase) ?? new();

            var initializerByPropertyName = objCreationExpression.Initializer?.Expressions
                .OfType<InitializerExpressionSyntax>()
                .Select(x => new { Initializer = x, PropertyName = GetInitializerLeft(x) })
                .Where(x => x.PropertyName != null)
                .ToDictionary(x => x.PropertyName, x => x.Initializer) ?? new();

            var maps = new List<ClassMap>();
            var membersMaps = new List<MemberMap>();
            var toMembers = constructor.GetPropertiesInitializedByConstructorAndInitializer();
            foreach (var member in toMembers)
            {
                MemberMap? memberMap = (member) switch
                {
                    IParameterSymbol parameter when argumentByParameterName.TryGetValue(member.Name, out var argument) => MemberMap.Argument(parameter, argument),
                    IPropertySymbol property when initializerByPropertyName.TryGetValue(member.Name, out var initializer) => MemberMap.InitializerExpression(property, initializer),
                    IParameterSymbol parameter => _classMapDesigner.DesignConstructorParameterMap(from, parameter),
                    IPropertySymbol property => _classMapDesigner.DesignInitializerPropertyMap(from, property),
                    _ => null
                };

                if (memberMap == null)
                {
                    continue;
                }
                membersMaps.Add(memberMap);

                if (memberMap.MapType is MemberMapType.UnflattenConstructor or MemberMapType.UnflattenInitializer)
                {
                    maps.AddRange(_classMapDesigner.DesignUnflattingClassMap(from, memberMap.ToName, memberMap.ToType));
                }
                else if (memberMap is { IsSameTypes: false, IsProvidedByUser: false })
                {
                    maps.AddRange(_classMapDesigner.DesignMapsForPlanner(memberMap.FromType, memberMap.ToType));
                }
            }

            var customParameterName = methodSyntax.ParameterList.Parameters.First().Identifier.Text;
            maps.Add(new ClassPartialConstructorMap(from, to, membersMaps, customParameterName));

            return maps;
        }

        private IParameterSymbol GetConstructorParameter(IMethodSymbol constructor, ArgumentSyntax argument)
        {
            //argument -> argumentList -> method
            if (argument.Parent?.Parent is ObjectCreationExpressionSyntax methodDeclaration
                && methodDeclaration?.ArgumentList?.Arguments.IndexOf(argument) is int index)
            {
                return constructor.Parameters[index];
            }
            else
            {
                throw new Exception($"Parameter for argument {argument} was not found");
            }
        }

        private string? GetInitializerLeft(InitializerExpressionSyntax initializer)
            => initializer.As<AssignmentExpressionSyntax>()?.Left.As<IdentifierNameSyntax>()?.Identifier.ValueText;
    }
}
