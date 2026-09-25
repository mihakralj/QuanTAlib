#if NETFRAMEWORK
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices;

/// <summary>
/// net48 polyfill for <c>System.Runtime.InteropServices.CollectionsMarshal</c>.
/// </summary>
/// <remarks>
/// The BCL type (and the <c>AsSpan</c>/<c>SetCount</c> members) only exist on .NET 5+. The library
/// relies on the pattern "create a list with capacity, set its count, then write through the
/// returned span" in the batch calculation paths. On .NET Framework there is no supported way to
/// resize a <see cref="List{T}"/> without materializing elements, so this polyfill reads/writes the
/// list's private <c>_items</c>/<c>_size</c> fields. Both <see cref="List{T}"/> and
/// <see cref="ListLayout{T}"/> place the backing array first and the size second, so the field
/// offsets line up. This is only compiled for net48.
/// </remarks>
internal static class CollectionsMarshal
{
    public static Span<T> AsSpan<T>(List<T>? list)
    {
        if (list is null)
        {
            return default;
        }

        return new Span<T>(ListLayout<T>.GetItems(list), 0, list.Count);
    }

    public static void SetCount<T>(List<T> list, int count)
    {
        if (list is null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count > list.Capacity)
        {
            list.Capacity = count;
        }

        ListLayout<T>.SetSize(list, count);
    }

    /// <summary>Mirrors the first two fields of <see cref="List{T}"/> (<c>_items</c>, <c>_size</c>).</summary>
#pragma warning disable S4487 // Fields are read/written through Unsafe field-offset reinterpretation, not directly.
    private sealed class ListLayout<T>
    {
        public T[] Items = null!;
        public int Size;

        public static T[] GetItems(List<T> list) => Unsafe.As<ListLayout<T>>(list).Items;

        public static void SetSize(List<T> list, int size) => Unsafe.As<ListLayout<T>>(list).Size = size;
    }
#pragma warning restore S4487
}
#endif
