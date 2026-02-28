using System.Runtime.CompilerServices;

namespace QuanTAlib.Python;

internal static class ArrayBridge
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool IsNull(double* ptr) => ptr == null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ValidateLength(int n) =>
        n > 0 ? StatusCodes.QTL_OK : StatusCodes.QTL_ERR_INVALID_LENGTH;
}