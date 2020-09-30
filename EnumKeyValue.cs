using System;
using Microsoft.CodeAnalysis;

namespace EnumReflector
{
    internal readonly struct EnumKeyValue
    {
        public ISymbol MemberName { get; }
        public object? Value { get; }

        public EnumKeyValue(ISymbol name, object? value) => 
            (MemberName, Value) = (name ?? throw new ArgumentNullException(nameof(name)), value);


        public EnumKeyValue WithValue(object? newValue) => new EnumKeyValue(MemberName, newValue);

        public override string ToString() =>
            MemberName.Name + 
            (Value is not null
                ? $" = [{Value.GetType().Name}]{Value}"
                : "");
    }
}
