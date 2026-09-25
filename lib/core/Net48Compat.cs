using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

#if !NET5_0_OR_GREATER
/// <summary>
/// net48 shims for .NET Core-only BCL APIs used throughout the library.
/// </summary>
/// <remarks>
/// These are declared as C# static extension members, so call sites stay unchanged:
/// <c>double.IsFinite(x)</c>, <c>Math.FusedMultiplyAdd(...)</c>, <c>GC.AllocateArray&lt;T&gt;(n)</c>,
/// <c>Array.Clear(a)</c>, <c>DateTime.UnixEpoch</c>, <c>Vector.LoadUnsafe(...)</c> and
/// <c>Vector&lt;long&gt;.AllBitsSet</c> all resolve to the shims below on .NET Framework, while
/// native implementations are used on net10.0. Only compiled for net48.
/// </remarks>
internal static class Net48Compat
{
    // 2^27 + 1, the Dekker split constant for IEEE-754 binary64.
    private const double Splitter = 134217729.0;

    extension(double)
    {
        /// <summary>net48 shim for <c>double.IsFinite</c>.</summary>
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    extension(Math)
    {
        /// <summary>
        /// net48 software FMA (Dekker two-product plus error-free add). .NET Framework has neither a
        /// hardware FMA instruction nor <c>Math.FusedMultiplyAdd</c>; this performs a single rounding
        /// of <c>x * y + z</c>, matching the net10.0 result's rounding semantics.
        /// </summary>
        public static double FusedMultiplyAdd(double x, double y, double z)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            {
                return (x * y) + z;
            }

            // Dekker splitting overflows for very large operands; fall back rather than corrupt the result.
            if (Math.Abs(x) > 1e150 || Math.Abs(y) > 1e150)
            {
                return (x * y) + z;
            }

            double p = x * y;
            double e = TwoProductError(x, y, p);

            double s = p + z;
            double t = Math.Abs(p) >= Math.Abs(z) ? (p - s) + z : (z - s) + p;
            return s + (e + t);
        }

        /// <summary>net48 shim for <c>Math.Asinh</c>.</summary>
        public static double Asinh(double value)
        {
            double abs = Math.Abs(value);
            double result = Math.Log(abs + Math.Sqrt((abs * abs) + 1.0));
            return value < 0 ? -result : result;
        }

        /// <summary>net48 shim for <c>Math.CopySign</c>.</summary>
        public static double CopySign(double x, double y) =>
            BitConverter.DoubleToInt64Bits(y) < 0 ? -Math.Abs(x) : Math.Abs(x);
    }

    extension(GC)
    {
        /// <summary>net48 shim for <c>GC.AllocateArray&lt;T&gt;</c> (pinning is ignored).</summary>
        public static T[] AllocateArray<T>(int length, bool pinned = false) => new T[length];
    }

    extension(Array)
    {
        /// <summary>net48 shim for the single-argument <c>Array.Clear(Array)</c> overload.</summary>
        public static void Clear(Array array) => Array.Clear(array, 0, array.Length);
    }

    extension(DateTime)
    {
        /// <summary>net48 shim for <c>DateTime.UnixEpoch</c>.</summary>
        public static DateTime UnixEpoch => new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    extension(Vector)
    {
        /// <summary>net48 shim for <c>Vector.LoadUnsafe&lt;T&gt;(ref T, nuint)</c>.</summary>
        public static Vector<T> LoadUnsafe<T>(ref T source, nuint elementOffset) where T : struct =>
            Unsafe.ReadUnaligned<Vector<T>>(
                ref Unsafe.AddByteOffset(ref Unsafe.As<T, byte>(ref source), (nint)elementOffset * Unsafe.SizeOf<T>()));
    }

    extension(Vector<long>)
    {
        /// <summary>net48 shim for <c>Vector&lt;long&gt;.AllBitsSet</c>.</summary>
        public static Vector<long> AllBitsSet => new(-1L);
    }

    extension<T>(Vector<T> vector) where T : struct
    {
        /// <summary>net48 shim for <c>Vector&lt;T&gt;.CopyTo(Span&lt;T&gt;)</c>.</summary>
        public void CopyTo(Span<T> destination)
        {
            if (destination.Length < Vector<T>.Count)
            {
                throw new ArgumentException("Destination span is too short.", nameof(destination));
            }

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(destination)), vector);
        }

        /// <summary>net48 shim for <c>Vector&lt;T&gt;.StoreUnsafe(ref T, nuint)</c>.</summary>
        public void StoreUnsafe(ref T destination, nuint elementOffset) =>
            Unsafe.WriteUnaligned(
                ref Unsafe.AddByteOffset(ref Unsafe.As<T, byte>(ref destination), (nint)elementOffset * Unsafe.SizeOf<T>()),
                vector);
    }

    /// <summary>Exact error of the rounded product <c>p = a * b</c> (Dekker).</summary>
    private static double TwoProductError(double a, double b, double p)
    {
        double ah = Split(a);
        double al = a - ah;
        double bh = Split(b);
        double bl = b - bh;
        return ((ah * bh - p) + (ah * bl) + (al * bh)) + (al * bl);
    }

    private static double Split(double a)
    {
        double t = Splitter * a;
        return t - (t - a);
    }
}

/// <summary>net48 fallback for <c>MemoryExtensions.Sort(Span&lt;T&gt;)</c>, which System.Memory omits.</summary>
internal static class SpanSortCompat
{
    public static void Sort<T>(this Span<T> span) where T : IComparable<T>
    {
        if (span.Length <= 1)
        {
            return;
        }

        T[] rented = ArrayPool<T>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(rented);
            Array.Sort(rented, 0, span.Length);
            new ReadOnlySpan<T>(rented, 0, span.Length).CopyTo(span);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(rented, clearArray: true);
        }
    }
}
#endif

/// <summary>
/// Loads a <see cref="Vector{T}"/> from the start of a span. net10.0 uses the span constructor;
/// net48 (whose System.Numerics.Vectors surface predates it) reinterprets the span via
/// <see cref="MemoryMarshal.Cast{TFrom, TTo}(ReadOnlySpan{TFrom})"/>, which System.Memory provides.
/// The span must contain at least <c>Vector&lt;T&gt;.Count</c> elements (the same contract as the
/// net10.0 span constructor).
/// </summary>
internal static class VectorCompat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<T> Load<T>(ReadOnlySpan<T> span) where T : struct =>
#if NET5_0_OR_GREATER
        new Vector<T>(span);
#else
        MemoryMarshal.Cast<T, Vector<T>>(span)[0];
#endif
}
