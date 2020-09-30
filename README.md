# EnumReflector
Working with enums can be a real pain, especially when values are serialized/deserialized or simply represented as meaningful strings.

Existing methods, like [`string[] Enum.GetNames(Type)`](https://docs.microsoft.com/en-us/dotnet/api/system.enum.getnames?view=netcore-3.1) rely heavily on the reflection over enum's fields.
So we have to either cache values for later reuse, pay reflection price every time, or manually write maps between enum values and their string representation.

Source generators appear to be a perfect solution for the third case, and this project demonstrates how it can be applied to user-defined enums.

When attached as anylyzer, this library provides an attribute `[EnumReflector.RflectEnumAttribute]` with no parameters, which can be applied to an enum.
The source generator then produces static class `EnumReflector.EnumExtensions` with a few static methods.
For instance, `string? GetEnumName<T>(this T) where T : Enum` can be used as extension method on any enum. It throws when applied to an enum with no `[ReflectEnum]` attribute.
The method essentially is a giant switch over `typeof(T)` and over all possible values of enum `T`. 
Such approach allows JIT to potentially eliminate unreachable branches when method is used with a given `T`.
Currently, this method does not account for possible duplicate values in the enum, so expect to receive the name of the first occurance of passed value.
If value is not declared, `null` is returned.

Similarly works `(String Name, T Value)[] GetEnumValues<T>() where T : Enum`, which returns an array of tuples, containing name and enum value (which is strongly typed as `T`).
Again, this is achieved by directly initializing array with tuples with constant values, no reflection applied.
I expect JIT to be able to do some magic here in runtime, though not sure.

# Plans
Tests & benchmarks coming later -- it is very well possible that BCL's solution works better.

The important limitation is that this method works only with the user-defined enums, but it is very easy to fall-back to BCL's implementation for all other cases.

Another thing is that string-enum value pairs can be also cached as static dictionaries, which will allow much faster enum parsing, relying on dictionary's implementation of search by key.
This can be done, again, reflection free.
