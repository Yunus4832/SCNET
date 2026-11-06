using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

namespace Game.Protocol.Generators;

public sealed partial class MessageOwnershipGenerator
{
    private sealed partial class Emitter
    {
        private readonly Dictionary<INamedTypeSymbol, string> _charges = new(SymbolEqualityComparer.Default);

        private static long ScalarCharge(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
            {
                return ScalarCharge(enumeration.EnumUnderlyingType!);
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte: return 1;
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16: return 2;
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Single: return 4;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Double:
                case SpecialType.System_DateTime: return 8;
                case SpecialType.System_Decimal: return 16;
            }

            if (type is INamedTypeSymbol named && type.IsUnmanagedType)
            {
                return Math.Max(
                    1,
                    named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic)
                        .Sum(field => ScalarCharge(field.Type))
                );
            }

            throw new InvalidOperationException("No scalar queue charge for " + Name(type));
        }

        private string Charge(ITypeSymbol type, string value)
        {
            if (type.NullableAnnotation == NullableAnnotation.Annotated ||
                type is INamedTypeSymbol nullable &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var local = "optional" + _local++;
                var inner = type is INamedTypeSymbol n &&
                            n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    ? n.TypeArguments[0]
                    : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                return "(" + value + " is { } " + local + " ? " + Charge(inner, local) + " : 0L)";
            }

            if (type.SpecialType == SpecialType.System_String) { return "TextCharge(" + value + ")"; }

            if (type.IsUnmanagedType || type.TypeKind == TypeKind.Enum) { return ScalarCharge(type) + "L"; }

            if (Json(type)) { return "JsonCharge(" + value + ")"; }

            switch (type.ToDisplayString())
            {
                case "Game.Protocol.ModId":
                case "Game.Protocol.Identity.PlayerAddress": return "TextCharge(" + value + ".Value)";
                case "Game.Protocol.ResourceId":
                    return "checked(TextCharge(" + value + ".Namespace.Value) + TextCharge(" + value + ".Path))";
                case "Game.Protocol.Localization.LocalizedText":
                    return "global::Game.Protocol.Localization.LocalizedTextLimits.GetRetainedBytes(" + value + ")";
                case "Game.Protocol.Messaging.MessageContent":
                    return "SequenceCharge(" +
                           value +
                           ".Segments, segment => checked(32L + global::Game.Protocol.Localization.LocalizedTextLimits.GetRetainedBytes(segment.Text)))";
            }

            if (type is IArrayTypeSymbol array)
            {
                if (array.ElementType.IsUnmanagedType)
                {
                    return "checked(32L + " + ScalarCharge(array.ElementType) + "L * " + value + ".LongLength)";
                }

                var local = "item" + _local++;
                return "SequenceCharge(" + value + ", " + local + " => " + Charge(array.ElementType, local) + ")";
            }

            var named = (INamedTypeSymbol)type;
            if (Sequence(named))
            {
                var local = "item" + _local++;
                return "SequenceCharge(" + value + ", " + local + " => " + Charge(named.TypeArguments[0], local) + ")";
            }

            if (Map(named))
            {
                var local = "entry" + _local++;
                return "SequenceCharge(" +
                       value +
                       ", " +
                       local +
                       " => checked(32L + " +
                       Charge(named.TypeArguments[0], local + ".Key") +
                       " + " +
                       Charge(named.TypeArguments[1], local + ".Value") +
                       "))";
            }

            if (!_charges.TryGetValue(named, out var method))
            {
                method = "Charge" + _charges.Count;
                _charges.Add(named, method);
            }

            return method + "(" + value + ")";
        }

        private void EmitCharges(IEnumerable<INamedTypeSymbol> types, StringBuilder text)
        {
            text.AppendLine(
                "private static long EstimateCore(global::Game.Protocol.IProtocolMessage message) => message switch {"
            );
            foreach (var type in types)
            {
                text.Append(Name(type)).Append(" value => ").Append(Charge(type, "value")).AppendLine(",");
            }

            text.AppendLine(
                "_ => throw new System.IO.InvalidDataException(\"Unknown message queue charge contract.\") };"
            );
            var emitted = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            while (emitted.Count < _charges.Count)
            {
                var next = _charges.First(pair => !emitted.Contains(pair.Key));
                emitted.Add(next.Key);
                text.Append("private static long ").Append(next.Value).Append('(').Append(Name(next.Key))
                    .Append(" value) => ");
                var union = next.Key.GetAttributes().Where(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "MessagePack.UnionAttribute"
                ).ToArray();
                if (union.Length > 0)
                {
                    text.AppendLine("value switch {");
                    foreach (var attribute in union)
                    {
                        var subtype = (INamedTypeSymbol)attribute.ConstructorArguments[1].Value!;
                        text.Append(Name(subtype)).Append(" item => ").Append(Charge(subtype, "item")).AppendLine(",");
                    }

                    text.AppendLine(
                        "_ => throw new System.IO.InvalidDataException(\"Unknown queue charge union member.\") };"
                    );
                }
                else
                {
                    text.Append("checked(32L");
                    foreach (var property in Properties(next.Key))
                    {
                        text.Append(" + ").Append(Charge(property.Type, "value." + property.Name));
                    }

                    text.AppendLine(");");
                }
            }
        }
    }
}
