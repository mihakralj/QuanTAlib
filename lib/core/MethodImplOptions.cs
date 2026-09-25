using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// net48 compatibility shim for <see cref="System.Runtime.CompilerServices.MethodImplOptions"/>.
/// </summary>
/// <remarks>
/// .NET Framework's <c>MethodImplOptions</c> enum does not define <c>AggressiveOptimization</c>
/// (value 512, introduced in .NET 5). Indicator code throughout the library refers to the simple
/// name <c>MethodImplOptions.AggressiveInlining</c> / <c>MethodImplOptions.AggressiveOptimization</c>
/// from inside the <c>QuanTAlib</c> namespace. This internal type shadows the framework enum for
/// in-assembly use and forwards each member to the real enum where it exists, falling back to the
/// literal value otherwise. It is not visible to consumers of the assembly.
/// </remarks>
internal static class MethodImplOptions
{
    internal const System.Runtime.CompilerServices.MethodImplOptions AggressiveInlining =
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining;

    internal const System.Runtime.CompilerServices.MethodImplOptions AggressiveOptimization =
#if NET5_0_OR_GREATER
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization;
#else
        (System.Runtime.CompilerServices.MethodImplOptions)512;
#endif
}
