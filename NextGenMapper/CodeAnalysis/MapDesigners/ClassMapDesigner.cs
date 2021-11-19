﻿using Microsoft.CodeAnalysis;
using NextGenMapper.CodeAnalysis.Maps;
using NextGenMapper.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NextGenMapper.CodeAnalysis.MapDesigners
{
    public class ClassMapDesigner
    {
        private readonly List<(ITypeSymbol from, ITypeSymbol to)> _referencesHistory;
        private readonly DiagnosticReporter _diagnosticReporter;
        private readonly ConstructorFinder _constructorFinder;

        public ClassMapDesigner(DiagnosticReporter diagnosticReporter)
        {
            _referencesHistory = new();
            _diagnosticReporter = diagnosticReporter;
            _constructorFinder = new();
        }

        public List<ClassMap> DesignMapsForPlanner(ITypeSymbol from, ITypeSymbol to)
        {
            if (from.IsPrimitive() || to.IsPrimitive())
            {
                return new();
            }
            if (_referencesHistory.Contains((from, to), new ReferencesEqualityComparer()))
            {
                _diagnosticReporter.ReportCircularReferenceError(to.Locations, _referencesHistory.Select(x => x.from).Append(from));
                return new();
            }
            _referencesHistory.Add((from, to));

            var constructor = _constructorFinder.GetOptimalConstructor(from, to, new List<string>());
            if (constructor == null)
            {
                _diagnosticReporter.ReportConstructorNotFoundError(to.Locations, from, to);
                return new();
            }

            var maps = new List<ClassMap>();
            var membersMaps = new List<MemberMap>();
            var toMembers = constructor.GetPropertiesInitializedByConstructorAndInitializer();
            foreach (var member in toMembers)
            {
                MemberMap? memberMap = member switch
                {
                    IParameterSymbol parameter => DesignConstructorParameterMap(from, parameter),
                    IPropertySymbol property => DesignInitializerPropertyMap(from, property),
                    _ => null
                };

                if (memberMap == null)
                {
                    continue;
                }
                membersMaps.Add(memberMap);

                if (memberMap.MapType is MemberMapType.UnflattenConstructor or MemberMapType.UnflattenInitializer)
                {
                    maps.AddRange(DesignUnflattingClassMap(from, memberMap.ToName, memberMap.ToType));
                }
                else if (memberMap is { IsSameTypes: false })
                {
                    maps.AddRange(DesignMapsForPlanner(memberMap.FromType, memberMap.ToType));
                }
            }

            maps.Add(new ClassMap(from, to, membersMaps));

            return maps;
        }

        public MemberMap? DesignConstructorParameterMap(ITypeSymbol from, IParameterSymbol constructorParameter)
        {
            var fromProperty = from.FindProperty(constructorParameter.Name);
            if (fromProperty != null)
            {
                return MemberMap.Counstructor(fromProperty, constructorParameter);
            }

            var (flattenProperty, mappedProperty) = FindFlattenMappedProperty(from, constructorParameter.Name);
            if (flattenProperty != null && mappedProperty != null)
            {
                return MemberMap.Counstructor(mappedProperty, constructorParameter, flattenPropertyName: flattenProperty.Name);
            }

            var isUnflattening = IsUnflattening(from, constructorParameter.Name, constructorParameter.Type);
            if (isUnflattening)
            {
                return MemberMap.CounstructorUnflatten(from, constructorParameter);
            }

            return null;
        }

        public MemberMap? DesignInitializerPropertyMap(ITypeSymbol from, IPropertySymbol initializerProperty)
        {
            var fromProperty = from.FindProperty(initializerProperty.Name);
            if (fromProperty != null)
            {
                return MemberMap.Initializer(fromProperty, initializerProperty);
            }

            var (flattenProperty, mappedProperty) = FindFlattenMappedProperty(from, initializerProperty.Name);
            if (flattenProperty != null && mappedProperty != null)
            {
                return MemberMap.Initializer(mappedProperty, initializerProperty, flattenPropertyName: flattenProperty.Name);
            }

            var isUnflattening = IsUnflattening(from, initializerProperty.Name, initializerProperty.Type);
            if (isUnflattening)
            {
                return MemberMap.InitializerUnflatten(from, initializerProperty);
            }

            return null;
        }

        private bool IsUnflattening(ITypeSymbol from, string unflattingPropertyName, ITypeSymbol unflattingPropertyType)
        {
            var constructor = _constructorFinder.GetOptimalUnflatteningConstructor(from, unflattingPropertyType, unflattingPropertyName);
            if (constructor == null)
            {
                return false;
            }

            var flattenProperties = unflattingPropertyType.GetProperties().Select(x => $"{unflattingPropertyName}{x.Name}");
            var isUnflattening = from.GetPropertiesNames().Any(x => flattenProperties.Contains(x, StringComparer.InvariantCultureIgnoreCase));

            return isUnflattening;
        }

        public List<ClassMap> DesignUnflattingClassMap(ITypeSymbol from, string unflattingPropertyName, ITypeSymbol unflattingPropertyType)
        {
            var constructor = _constructorFinder.GetOptimalUnflatteningConstructor(from, unflattingPropertyType, unflattingPropertyName);
            if (constructor == null)
            {
                return new();
            }
            var toMembers = constructor.GetPropertiesInitializedByConstructorAndInitializer();

            var maps = new List<ClassMap>();
            var membersMaps = new List<MemberMap>();
            foreach (var member in toMembers)
            {
                var fromProperty = from.FindProperty($"{unflattingPropertyName}{member.Name}");
                MemberMap? map = (fromProperty, member) switch
                {
                    ({ }, IParameterSymbol parameter) => MemberMap.Counstructor(fromProperty, parameter),
                    ({ }, IPropertySymbol property) => MemberMap.Initializer(fromProperty, property),
                    _ => null
                };
                membersMaps.AddIfNotNull(map);

                if (map is { IsSameTypes: false })
                {
                    maps.AddRange(DesignMapsForPlanner(map.FromType, map.ToType));
                }
            }
            if (membersMaps.IsEmpty())
            {
                return new();
            }
            maps.Add(new ClassMap(from, unflattingPropertyType, membersMaps, isUnflattening: true));

            return maps;
        }

        private (IPropertySymbol flattenProperty, IPropertySymbol mappedProperty) FindFlattenMappedProperty(
            ITypeSymbol type, string name, StringComparison comparision = StringComparison.InvariantCultureIgnoreCase)
        {
            return type
                .GetProperties()
                    .SelectMany(flatten => flatten.Type
                        .GetProperties()
                        .Where(mapped => $"{flatten.Name}{mapped.Name}".Equals(name, comparision))
                        .Select(mapped => (flatten, mapped)))
                    .FirstOrDefault();
        }
    }
}
