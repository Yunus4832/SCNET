using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Game.Protocol.Generators;

[Generator]
public sealed partial class MessageOwnershipGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor _unsupported = new(
        "SCNETOWN001",
        "Unsupported message ownership contract",
        "{0}",
        "Ownership",
        DiagnosticSeverity.Error,
        true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (output, compilation) =>
            {
                try
                {
                    var source = new Emitter(compilation).Build();
                    output.AddSource("ProtocolMessageOwnership.g.cs", SourceText.From(source, Encoding.UTF8));
                }
                catch (InvalidOperationException exception)
                {
                    output.ReportDiagnostic(Diagnostic.Create(_unsupported, Location.None, exception.Message));
                }
            }
        );
    }

    private sealed partial class Emitter(Compilation compilation)
    {
        private readonly Dictionary<ITypeSymbol, bool> _mutable = new(SymbolEqualityComparer.Default);

        private readonly Dictionary<INamedTypeSymbol, string> _copies = new(SymbolEqualityComparer.Default);

        private int _local;

        private static string Name(ITypeSymbol type) => type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithMiscellaneousOptions(
                    SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                )
        );

        private static bool Attribute(ISymbol symbol, string name) =>
            symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == name);

        private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.GetMembers().OfType<IFieldSymbol>()
                    .Any(field => Attribute(field, "MessagePack.KeyAttribute")))
                {
                    throw new InvalidOperationException(
                        "Ownership contracts must expose keyed properties: " + Name(current)
                    );
                }

                foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                {
                    if (Attribute(property, "MessagePack.KeyAttribute")) { yield return property; }
                }
            }
        }

        private static string Definition(INamedTypeSymbol type) => type.OriginalDefinition.ToDisplayString();

        private static bool Sequence(INamedTypeSymbol type) =>
            Definition(type) is "System.Collections.Generic.IReadOnlyList<T>" or
                "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.IEnumerable<T>" or
                "System.Collections.Generic.IList<T>" or "System.Collections.Generic.List<T>";

        private static bool Map(INamedTypeSymbol type) =>
            Definition(type) is "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>"
                or "System.Collections.Generic.Dictionary<TKey, TValue>";

        private static bool Json(ITypeSymbol type)
        {
            for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == "System.Text.Json.Nodes.JsonNode") { return true; }
            }

            return false;
        }

        private bool Mutable(ITypeSymbol type)
        {
            if (type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return Mutable(type.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
            }

            if (type.SpecialType == SpecialType.System_String || type.IsUnmanagedType || type.TypeKind == TypeKind.Enum)
            {
                return false;
            }

            if (type is IArrayTypeSymbol ||
                Json(type) ||
                type.ToDisplayString() == "Game.Protocol.Messaging.MessageContent")
            {
                return true;
            }

            if (type is not INamedTypeSymbol named)
            {
                throw new InvalidOperationException("Unsupported contract " + Name(type));
            }

            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return Mutable(named.TypeArguments[0]);
            }

            if (Sequence(named) || Map(named)) { return true; }

            if (_mutable.TryGetValue(type, out var known)) { return known; }

            _mutable[type] = false;
            if (named.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "MessagePack.UnionAttribute"
                ))
            {
                _mutable[type] = true;
                return true;
            }

            if (Attribute(named, "MessagePack.MessagePackObjectAttribute"))
            {
                var result = Properties(named).Any(property => Mutable(property.Type) ||
                                                               !type.IsValueType &&
                                                               property.SetMethod is { IsInitOnly: false }
                );
                _mutable[type] = result;
                return result;
            }

            if (type.IsValueType || named.ToDisplayString() == "Game.Protocol.Localization.LocalizedText")
            {
                var result = named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic)
                    .Any(field => Mutable(field.Type));
                _mutable[type] = result;
                return result;
            }

            throw new InvalidOperationException("No ownership rule for " + Name(type));
        }

        private string Copy(ITypeSymbol type, string value)
        {
            if (!Mutable(type)) { return value; }

            if (type.NullableAnnotation == NullableAnnotation.Annotated ||
                type is INamedTypeSymbol nullable &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var local = "optional" + _local++;
                var inner = type is INamedTypeSymbol n &&
                            n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    ? n.TypeArguments[0]
                    : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                return value + " is { } " + local + " ? " + Copy(inner, local) + " : null";
            }

            if (Json(type)) { return "(" + Name(type) + ")" + value + ".DeepClone()"; }

            if (type.ToDisplayString() == "Game.Protocol.Messaging.MessageContent")
            {
                return "new global::Game.Protocol.Messaging.MessageContent(" + value + ".Segments)";
            }

            if (type is IArrayTypeSymbol array)
            {
                if (array.Rank != 1)
                {
                    throw new InvalidOperationException("Only single-dimensional contract arrays are supported.");
                }

                return CopySequence(array.ElementType, value);
            }

            var named = (INamedTypeSymbol)type;
            if (Sequence(named))
            {
                return CopySequence(
                    named.TypeArguments[0],
                    value,
                    Definition(named) == "System.Collections.Generic.List<T>"
                );
            }

            if (Map(named))
            {
                var local = "entry" + _local++;
                return value +
                       ".ToDictionary(" +
                       local +
                       " => " +
                       Copy(named.TypeArguments[0], local + ".Key") +
                       ", " +
                       local +
                       " => (" +
                       Name(named.TypeArguments[1]) +
                       ")(" +
                       Copy(named.TypeArguments[1], local + ".Value") +
                       "))";
            }

            if (!named.IsRecord && !named.IsValueType)
            {
                throw new InvalidOperationException("Mutable contract must support typed copy: " + Name(type));
            }

            if (!_copies.TryGetValue(named, out var method))
            {
                method = "Copy" + _copies.Count;
                _copies.Add(named, method);
            }

            return method + "(" + value + ")";
        }

        private string CopySequence(ITypeSymbol element, string value, bool asList = false)
        {
            var materialize = asList ? ".ToList()" : ".ToArray()";
            if (!Mutable(element)) { return value + materialize; }

            var local = "item" + _local++;
            return value + ".Select(" + local + " => " + Copy(element, local) + ")" + materialize;
        }

        private void ValidateGraph(ITypeSymbol type, HashSet<ITypeSymbol> path, HashSet<ITypeSymbol> complete)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            if (complete.Contains(type) ||
                type.IsUnmanagedType ||
                type.SpecialType == SpecialType.System_String ||
                Json(type) ||
                type.ToDisplayString() == "Game.Protocol.Messaging.MessageContent") { return; }

            if (!path.Add(type)) { throw new InvalidOperationException("Cyclic ownership contract: " + Name(type)); }

            if (type is IArrayTypeSymbol array) { ValidateGraph(array.ElementType, path, complete); }
            else if (type is INamedTypeSymbol named)
            {
                if (Sequence(named) ||
                    Map(named) ||
                    named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    foreach (var argument in named.TypeArguments) { ValidateGraph(argument, path, complete); }
                }
                else
                {
                    foreach (var union in named.GetAttributes().Where(attribute =>
                                 attribute.AttributeClass?.ToDisplayString() == "MessagePack.UnionAttribute"
                             ))
                    {
                        ValidateGraph((ITypeSymbol)union.ConstructorArguments[1].Value!, path, complete);
                    }

                    foreach (var property in Properties(named)) { ValidateGraph(property.Type, path, complete); }
                }
            }

            path.Remove(type);
            complete.Add(type);
        }

        internal string Build()
        {
            var root = compilation.GetTypeByMetadataName("Game.Protocol.IProtocolMessage") ??
                       throw new InvalidOperationException("Cannot find protocol message root.");
            var types = root.GetAttributes().Where(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "MessagePack.UnionAttribute"
                )
                .Select(attribute => (INamedTypeSymbol)attribute.ConstructorArguments[1].Value!).ToArray();
            var complete = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in types)
            {
                ValidateGraph(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), complete);
            }

            var text = new StringBuilder(
                "// <auto-generated/>\n#nullable enable\nusing System.Linq;\nnamespace Game.Protocol.Serialization;\npublic static partial class ProtocolMessageOwnership\n{\n"
            );
            text.AppendLine(
                "private static global::Game.Protocol.IProtocolMessage CaptureCore(global::Game.Protocol.IProtocolMessage message) => message switch {"
            );
            foreach (var type in types)
            {
                text.Append(Name(type)).Append(" value => ").Append(Copy(type, "value")).AppendLine(",");
            }

            text.AppendLine(
                "_ => throw new System.IO.InvalidDataException(\"Unknown protocol message ownership contract.\") }; "
            );
            var emitted = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            while (emitted.Count < _copies.Count)
            {
                var next = _copies.First(pair => !emitted.Contains(pair.Key));
                emitted.Add(next.Key);
                var union = next.Key.GetAttributes().Where(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "MessagePack.UnionAttribute"
                ).ToArray();
                if (union.Length > 0)
                {
                    text.Append("private static ").Append(Name(next.Key)).Append(' ').Append(next.Value).Append('(')
                        .Append(Name(next.Key)).AppendLine(" value) => value switch {");
                    foreach (var attribute in union)
                    {
                        var subtype = (INamedTypeSymbol)attribute.ConstructorArguments[1].Value!;
                        text.Append(Name(subtype)).Append(" item => ").Append(Copy(subtype, "item")).AppendLine(",");
                    }

                    text.AppendLine(
                        "_ => throw new System.IO.InvalidDataException(\"Unknown ownership union member.\") };"
                    );
                    continue;
                }

                text.Append("private static ").Append(Name(next.Key)).Append(' ').Append(next.Value).Append('(')
                    .Append(Name(next.Key)).AppendLine(" value) => value with {");
                foreach (var property in Properties(next.Key).Where(property => Mutable(property.Type)))
                {
                    if (property.SetMethod == null)
                    {
                        throw new InvalidOperationException("Mutable contract member has no typed setter: " + property);
                    }

                    text.Append(property.Name).Append(" = ").Append(Copy(property.Type, "value." + property.Name))
                        .AppendLine(",");
                }

                text.AppendLine("};");
            }

            EmitCharges(types, text);
            return text.AppendLine("}").ToString();
        }
    }
}
