﻿using Microsoft.CodeAnalysis;
using NextGenMapper.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NextGenMapper.CodeAnalysis.MapDesigners
{
    public class ConstructorFinder
    {
        private readonly ConstructorComparer _constructorComparer = new();

        public IMethodSymbol? GetOptimalConstructor(ITypeSymbol from, ITypeSymbol to, HashSet<string> byUser)
        {
            var constructors = to.GetPublicConstructors();
            constructors.Sort(_constructorComparer);
            if (constructors.Length == 0)
            {
                return null;
            }

            var fromPropertiesNames = from.GetPropertiesNames().ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);
            var flattenPropertiesNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var property in from.GetProperties())
            {
                foreach (var flattenProperty in property.Type.GetProperties())
                {
                    flattenPropertiesNames.Add($"{property.Name}{flattenProperty.Name}");
                }
            }

            bool ValidateCommonCostructor(IMethodSymbol constructor)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (!byUser.Contains(parameter.Name)
                        && !fromPropertiesNames.Contains(parameter.Name)
                        && !flattenPropertiesNames.Contains(parameter.Name))
                    {
                        return false;
                    }
                }

                return true;
            }

            IMethodSymbol? commonConstructor = null;
            foreach (var constructor in constructors)
            {
                if (ValidateCommonCostructor(constructor))
                {
                    commonConstructor = constructor;
                    break;
                }
            }

            bool ValidateUnflattenCostructor(IMethodSymbol constructor)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (GetOptimalUnflatteningConstructor(from, parameter.Type, parameter.Name) == null
                        && !byUser.Contains(parameter.Name)
                        && !fromPropertiesNames.Contains(parameter.Name))
                    {
                        return false;
                    }
                }

                return true;
            }

            IMethodSymbol? unflattenConstructor = null;
            foreach (var constructor in constructors)
            {
                if (ValidateUnflattenCostructor(constructor))
                {
                    unflattenConstructor = constructor;
                    break;
                }
            }

            return GetParametersCount(commonConstructor) > GetParametersCount(unflattenConstructor)
                ? commonConstructor
                : unflattenConstructor;
        }

        public IMethodSymbol? GetOptimalUnflatteningConstructor(ITypeSymbol from, ITypeSymbol to, string unflattingPropertyName)
        {
            var constructors = to.GetPublicConstructors();
            constructors.Sort(_constructorComparer);
            if (constructors.Length == 0)
            {
                return null;
            }

            var fromPropertiesNames = from.GetPropertiesNames().ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

            bool ValidateCommonCostructor(IMethodSymbol constructor)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (!fromPropertiesNames.Contains($"{unflattingPropertyName}{parameter.Name}"))
                    {
                        return false;
                    }
                }

                return true;
            }

            IMethodSymbol? commonConstructor = null;
            foreach (var constructor in constructors)
            {
                if (ValidateCommonCostructor(constructor))
                {
                    commonConstructor = constructor;
                    break;
                }
            }

            var flattenProperties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach(var property in to.GetProperties())
            {
                flattenProperties.Add($"{unflattingPropertyName}{property.Name}");
            }

            foreach(var property in from.GetProperties())
            {
                if (flattenProperties.Contains(property.Name))
                {
                    return commonConstructor;
                }
            }

            return null;
        }

        private int GetParametersCount(IMethodSymbol? method) => method?.Parameters.Length ?? -1;

        class ConstructorComparer : IComparer<IMethodSymbol>
        {
            public int Compare(IMethodSymbol? x, IMethodSymbol? y) => x!.Parameters.Length.CompareTo(y!.Parameters.Length) * -1;
        }
    }
}
