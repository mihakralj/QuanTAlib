using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CRMA: Cubic Regression Moving Average
/// </summary>
/// <remarks>
/// Fits a degree-3 polynomial y = a0 + a1*x + a2*x² + a3*x³ to the most recent
/// N bars via least squares, returns the fitted endpoint value a0.
///
/// Calculation: Accumulate 7 power sums + 4 cross-products in O(N), solve 4×4
/// normal equations via Gaussian elimination with partial pivoting in O(1).
/// </remarks>
/// <seealso href="Crma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Crma : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private int _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastVal, double LastValidValue);
    private State _state;
    private State _p_state;

    private bool _isNew;

    public override bool IsHot => _buffer.IsFull;
    public bool IsNew => _isNew;

    /// <summary>
    /// Creates CRMA with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 4 for cubic regression)</param>
    public Crma(int period)
    {
        if (period < 4)
        {
            throw new ArgumentException("Period must be at least 4 for cubic regression", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Crma({period})";
        WarmupPeriod = period;
        _handler = Handle;
        _state.LastValidValue = double.NaN;
    }

    public Crma(ITValuePublisher source, int period) : this(period)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    /// <summary>
    /// Solves the 4×4 normal equation system for cubic polynomial regression.
    /// Returns the intercept a0 (fitted value at x=0, the newest bar).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SolveCubic(ReadOnlySpan<double> data, int count)
    {
        // Accumulate power sums S0..S6 and cross-products r0..r3
        double s0 = 0, s1 = 0, s2 = 0, s3 = 0, s4 = 0, s5 = 0, s6 = 0;
        double r0 = 0, r1 = 0, r2 = 0, r3 = 0;

        for (int i = 0; i < count; i++)
        {
            double v = data[i];
            double x = (double)i;
            double x2 = x * x;
            double x3 = x2 * x;

            s0 += 1.0;
            s1 += x;
            s2 += x2;
            s3 += x3;
            s4 += x2 * x2;
            s5 += x2 * x3;
            s6 += x3 * x3;

            r0 += v;
            r1 = Math.FusedMultiplyAdd(x, v, r1);
            r2 = Math.FusedMultiplyAdd(x2, v, r2);
            r3 = Math.FusedMultiplyAdd(x3, v, r3);
        }

        // Build 4×5 augmented matrix (row-major, inline on stack)
        //   [s0 s1 s2 s3 | r0]
        //   [s1 s2 s3 s4 | r1]
        //   [s2 s3 s4 s5 | r2]
        //   [s3 s4 s5 s6 | r3]
        Span<double> m = stackalloc double[20];
        m[0] = s0; m[1] = s1; m[2] = s2; m[3] = s3; m[4] = r0;
        m[5] = s1; m[6] = s2; m[7] = s3; m[8] = s4; m[9] = r1;
        m[10] = s2; m[11] = s3; m[12] = s4; m[13] = s5; m[14] = r2;
        m[15] = s3; m[16] = s4; m[17] = s5; m[18] = s6; m[19] = r3;

        // Gaussian elimination with partial pivoting
        for (int col = 0; col < 4; col++)
        {
            // Find pivot row
            int pivotRow = col;
            double pivotMax = Math.Abs(m[(col * 5) + col]);
            for (int row = col + 1; row < 4; row++)
            {
                double absVal = Math.Abs(m[(row * 5) + col]);
                if (absVal > pivotMax)
                {
                    pivotMax = absVal;
                    pivotRow = row;
                }
            }

            if (pivotMax < 1e-12)
            {
                return double.NaN; // Singular — caller will substitute raw price
            }

            // Swap rows if needed
            if (pivotRow != col)
            {
                int colOff = col * 5;
                int pivOff = pivotRow * 5;
                for (int k = col; k < 5; k++)
                {
                    (m[colOff + k], m[pivOff + k]) = (m[pivOff + k], m[colOff + k]);
                }
            }

            // Eliminate below
            double diag = m[(col * 5) + col];
            for (int row = col + 1; row < 4; row++)
            {
                double factor = m[(row * 5) + col] / diag;
                for (int k = col; k < 5; k++)
                {
                    m[(row * 5) + k] = Math.FusedMultiplyAdd(-factor, m[(col * 5) + k], m[(row * 5) + k]);
                }
            }
        }

        // Back-substitution
        Span<double> a = stackalloc double[4];
        for (int row = 3; row >= 0; row--)
        {
            double val = m[(row * 5) + 4];
            for (int k = row + 1; k < 4; k++)
            {
                val = Math.FusedMultiplyAdd(-m[(row * 5) + k], a[k], val);
            }
            a[row] = val / m[(row * 5) + row];
        }

        return a[0]; // Fitted value at x=0 (newest bar)
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _p_state = _state;
            double val = GetValidValue(input.Value);
            _buffer.Add(val);
            _state.LastVal = val;
        }
        else
        {
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);
            _buffer.UpdateNewest(val);
            _state.LastVal = val;
        }

        double result;
        int count = _buffer.Count;
        if (count < 4)
        {
            // Not enough points for cubic regression — return current value
            result = _buffer.Newest;
        }
        else
        {
            // Get buffer data in chronological order (oldest=index 0, newest=last)
            // We need newest at x=0, so we reverse the iteration in SolveCubic
            // Actually, we pass data newest-first: data[0]=newest, data[count-1]=oldest
            // This matches the PineScript convention: x=0 for newest
            const int StackAllocThreshold = 256;
            double[]? rented = count > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(count) : null;
            Span<double> data = rented != null
                ? rented.AsSpan(0, count)
                : stackalloc double[count];

            try
            {
                // Copy buffer in reverse chronological order (newest first)
                var span = _buffer.GetSpan();
                for (int i = 0; i < count; i++)
                {
                    data[i] = span[count - 1 - i];
                }

                double solved = SolveCubic(data, count);
                result = double.IsFinite(solved) ? solved : _buffer.Newest;
            }
            finally
            {
                if (rented != null)
                {
                    ArrayPool<double>.Shared.Return(rented);
                }
            }
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        double initialLastValid = _state.LastValidValue;
        Batch(source.Values, vSpan, _period, initialLastValid);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying last 'period' bars
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        Reset();

        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source.Values[i]))
                {
                    _state.LastValidValue = source.Values[i];
                    break;
                }
            }
        }
        else
        {
            _state.LastValidValue = initialLastValid;
        }

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            _buffer.Add(val);
            _state.LastVal = val;
        }
        _p_state = _state;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var crma = new Crma(period);
        return crma.Update(source);
    }

    /// <summary>
    /// Calculates CRMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double initialLastValid = double.NaN)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 4)
        {
            throw new ArgumentException("Period must be at least 4 for cubic regression", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;

        // Pre-process: build a NaN-corrected copy of source so we can index it directly
        double[]? rentedClean = len > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = rentedClean != null
            ? rentedClean.AsSpan(0, len)
            : stackalloc double[len];

        double[]? rentedData = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> dataBuffer = rentedData != null
            ? rentedData.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
            double lastValid = initialLastValid;

            // Build NaN-corrected array
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                    clean[i] = val;
                }
                else
                {
                    clean[i] = lastValid;
                }
            }

            // For each bar, solve cubic regression over the window
            for (int i = 0; i < len; i++)
            {
                int n = Math.Min(i + 1, period);
                if (n < 4)
                {
                    output[i] = clean[i];
                }
                else
                {
                    // Build newest-first data for SolveCubic
                    Span<double> data = dataBuffer[..n];
                    for (int j = 0; j < n; j++)
                    {
                        data[j] = clean[i - j]; // newest first (data[0]=bar i, data[1]=bar i-1, ...)
                    }

                    double solved = SolveCubic(data, n);
                    output[i] = double.IsFinite(solved) ? solved : clean[i];
                }
            }
        }
        finally
        {
            if (rentedClean != null)
            {
                ArrayPool<double>.Shared.Return(rentedClean);
            }
            if (rentedData != null)
            {
                ArrayPool<double>.Shared.Return(rentedData);
            }
        }
    }

    public static (TSeries Results, Crma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Crma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Resets the CRMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _state.LastValidValue = double.NaN;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Disposes the Crma instance, unsubscribing from the source publisher if subscribed.
    /// This method is idempotent and thread-safe.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0 && _source != null)
        {
            _source.Pub -= _handler;
            _source = null;
        }
        base.Dispose(disposing);
    }
}
