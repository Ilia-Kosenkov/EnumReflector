using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace EnumReflector
{
    internal static class Extensions
    {

        public static IReadOnlyDictionary<SpecialType, Type> SpecialTypeMap { get; }
            = new Dictionary<SpecialType, Type>()
            {
                [SpecialType.System_Byte] = typeof(byte),
                [SpecialType.System_SByte] = typeof(sbyte),
                [SpecialType.System_Int16] = typeof(short),
                [SpecialType.System_UInt16] = typeof(ushort),
                [SpecialType.System_Int32] = typeof(int),
                [SpecialType.System_UInt32] = typeof(uint),
                [SpecialType.System_Int64] = typeof(long),
                [SpecialType.System_UInt64] = typeof(ulong),
            };

        public static object IncrementBy(object value, byte val) =>
            value is ulong
                ? IncrementByAsUInt64(value, val)
                : IncrementByAsInt64(value, val);

        public static object IncrementByAsInt64(object value, long by) => (long) Convert.ChangeType(value, TypeCode.Int64) + 1;
        public static object IncrementByAsUInt64(object value, ulong by) => (ulong)Convert.ChangeType(value, TypeCode.UInt64) + 1;


        public static StringBuilder AppendLineTabbed(this StringBuilder builder, string line, int nTabs = 0)
            => builder.Append(new string('\t', nTabs)).AppendLine(line);

        public static StringBuilder AppendLineTabbed(this StringBuilder builder, int nTabs = 0)
            => builder.Append(new string('\t', nTabs));
    }
}
