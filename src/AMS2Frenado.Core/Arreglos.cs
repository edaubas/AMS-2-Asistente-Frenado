using System.Runtime.CompilerServices;

namespace AMS2Frenado.Core;

// Arreglos de tamano fijo embebidos en el struct (equivalen a T x[N] de C).
// Se usan genericos porque el header mezcla float, uint, byte y structs.
[InlineArray(2)] public struct Arr2<T> { private T _e; }
[InlineArray(3)] public struct Arr3<T> { private T _e; }
[InlineArray(4)] public struct Arr4<T> { private T _e; }
[InlineArray(40)] public struct Arr40<T> { private T _e; }
[InlineArray(64)] public struct Arr64<T> { private T _e; }
