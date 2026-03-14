using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// QRMA: Quadratic Regression Moving Average
/// </summary>
/// <remarks>
/// Fits a degree-2 polynomial y = a + b*x + c*x² to the most recent N bars via
/// ordinary least squares, returns the fitted endpoint value at x = N-1 (newest bar).
///
/// Calculation: Accumulate Faulhaber power sums S0..S4 + 3 cross-products in O(N),
/// solve 3×3 normal equations via Cramer's rule in O(1).
/// X-indexing: x = 0 oldest, x = N-1 newest; evaluate at x = N-1.
/// </remarks>
/// <seealso href="Qrma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Qrma : AbstractBase
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
    /// Creates QRMA with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 3 for quadratic regression)</param>
    public Qrma(int period)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3 for quadratic regression", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Qrma({period})";
        WarmupPeriod = period;
        _handler = Handle;
        _state.LastValidValue = double.NaN;
    }

    public Qrma(ITValuePublisher source, int period) : this(period)
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
    /// Solves the 3×3 normal equation system for quadratic polynomial regression
    /// using Cramer's rule. Data is oldest-first: data[0] = oldest, data[N-1] = newest.
    /// Returns the fitted value at x = N-1 (newest bar endpoint).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SolveQuadratic(ReadOnlySpan<double> data, int count)
    {
        // N = count; x goes 0..N-1  (oldest=0, newest=N-1)
        double n = count;

        // Faulhaber closed-form power sums (O(1))
        double s1 = n * (n - 1.0) * 0.5;                                   // Σx
        double s2 = n * (n - 1.0) * ((2.0 * n) - 1.0) / 6.0;                // Σx²
        double s3 = s1 * s1;                                                // Σx³ = [N(N-1)/2]²
        double s4 = n * (n - 1.0) * ((2.0 * n) - 1.0) * Math.FusedMultiplyAdd(3.0 * n, n - 1.0, -1.0) / 30.0; // Σx⁴

        // Cross-products in O(N)
        double r0 = 0, r1 = 0, r2 = 0;
        for (int i = 0; i < count; i++)
        {
            double v = data[i];
            double x = (double)i;
            double x2 = x * x;

            r0 += v;                                    // Σy
            r1 = Math.FusedMultiplyAdd(x, v, r1);       // Σxy
            r2 = Math.FusedMultiplyAdd(x2, v, r2);      // Σx²y
        }

        // 3×3 normal equations:
        //   [ N   S1  S2 ] [a]   [r0]
        //   [ S1  S2  S3 ] [b] = [r1]
        //   [ S2  S3  S4 ] [c]   [r2]

        // Cramer's rule: det of coefficient matrix
        double det = Math.FusedMultiplyAdd(n, (s2 * s4) - (s3 * s3),
                     Math.FusedMultiplyAdd(-s1, (s1 * s4) - (s3 * s2),
                                           s2 * ((s1 * s3) - (s2 * s2))));

        if (Math.Abs(det) < 1e-20)
        {
            return double.NaN; // Singular — caller substitutes raw price
        }

        double invDet = 1.0 / det;

        // det_a: replace column 0 with [r0, r1, r2]
        double detA = Math.FusedMultiplyAdd(r0, (s2 * s4) - (s3 * s3),
                      Math.FusedMultiplyAdd(-s1, (r1 * s4) - (r2 * s3),
                                            s2 * ((r1 * s3) - (r2 * s2))));

        // det_b: replace column 1 with [r0, r1, r2]
        double detB = Math.FusedMultiplyAdd(n, (r1 * s4) - (r2 * s3),
                      Math.FusedMultiplyAdd(-r0, (s1 * s4) - (s3 * s2),
                                            s2 * ((s1 * r2) - (s2 * r1))));

        // det_c: replace column 2 with [r0, r1, r2]
        double detC = Math.FusedMultiplyAdd(n, (s2 * r2) - (s3 * r1),
                      Math.FusedMultiplyAdd(-s1, (s1 * r2) - (s2 * r1),
                                            r0 * ((s1 * s3) - (s2 * s2))));

        double a = detA * invDet;
        double b = detB * invDet;
        double c = detC * invDet;

        // Evaluate at x = N-1 (newest bar endpoint)
        double xEval = n - 1.0;
        return Math.FusedMultiplyAdd(c, xEval * xEval, Math.FusedMultiplyAdd(b, xEval, a));
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
        if (count < 3)
        {
            // Not enough points for quadratic regression — return current value
            result = _buffer.Newest;
        }
        else
        {
            // Get buffer data in chronological order (oldest=index 0, newest=last)
            // SolveQuadratic expects oldest-first: data[0]=oldest, data[N-1]=newest
            const int StackAllocThreshold = 256;
            double[]? rented = count > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(count) : null;
            Span<double> data = rented != null
                ? rented.AsSpan(0, count)
                : stackalloc double[count];

            try
            {
                // Copy buffer in chronological order (oldest first) — direct from RingBuffer
                var span = _buffer.GetSpan();
                span[..count].CopyTo(data);

                double solved = SolveQuadratic(data, count);
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
        var qrma = new Qrma(period);
        return qrma.Update(source);
    }

    /// <summary>
    /// Calculates QRMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double initialLastValid = double.NaN)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3 for quadratic regression", nameof(period));
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

            // For each bar, solve quadratic regression over the window
            for (int i = 0; i < len; i++)
            {
                int n = Math.Min(i + 1, period);
                if (n < 3)
                {
                    output[i] = clean[i];
                }
                else
                {
                    // Build oldest-first data for SolveQuadratic
                    // data[0]=oldest (bar i-n+1), data[n-1]=newest (bar i)
                    Span<double> data = dataBuffer[..n];
                    for (int j = 0; j < n; j++)
                    {
                        data[j] = clean[i - n + 1 + j];
                    }

                    double solved = SolveQuadratic(data, n);
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

    public static (TSeries Results, Qrma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Qrma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Resets the QRMA state.
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
    /// Disposes the Qrma instance, unsubscribing from the source publisher if subscribed.
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
