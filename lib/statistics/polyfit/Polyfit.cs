using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Polyfit: Polynomial Fit (Regression) Moving Average
/// </summary>
/// <remarks>
/// Fits a degree-m polynomial y = a0 + a1*t + ... + am*t^m to the most recent
/// N bars via least squares normal equations, where t is normalized to [0,1]
/// (t=0 oldest bar, t=1 newest bar). Returns the fitted value at t=1.
///
/// Calculation: Accumulate (2m+1) power sums + (m+1) cross-products in O(N*m),
/// solve (m+1)×(m+1) normal equations via Gaussian elimination with partial
/// pivoting in O(m³). Degree is clamped to period-1. Min period = degree+1.
///
/// With degree=1 the result is identical to LSMA (linear regression endpoint).
/// </remarks>
/// <seealso href="Polyfit.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Polyfit : AbstractBase
{
    private readonly int _period;
    private readonly int _degree;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private int _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastVal, double LastValidValue);
    private State _state;
    private State _p_state;

    private bool _isNew;

    public int Degree => _degree;
    public override bool IsHot => _buffer.IsFull;
    public bool IsNew => _isNew;

    /// <summary>
    /// Creates Polyfit with specified period and polynomial degree.
    /// </summary>
    /// <param name="period">Lookback window size (must be >= 2)</param>
    /// <param name="degree">Polynomial degree 1–6 (clamped to period-1)</param>
    public Polyfit(int period, int degree = 2)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (degree < 1)
        {
            throw new ArgumentException("Degree must be at least 1", nameof(degree));
        }

        _period = period;
        _degree = Math.Min(degree, period - 1);
        _buffer = new RingBuffer(period);
        Name = $"Polyfit({period},{_degree})";
        WarmupPeriod = period;
        _handler = Handle;
        _state.LastValidValue = double.NaN;
    }

    public Polyfit(ITValuePublisher source, int period, int degree = 2) : this(period, degree)
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
    /// Solves the (m+1)×(m+1) normal equation system for polynomial regression of degree m.
    /// t-convention: data[0]=oldest (t=0/(n-1)), data[n-1]=newest (t=1).
    /// Returns the fitted value at t=1.0 (newest bar).
    /// </summary>
    /// <param name="data">Values oldest-first (data[0] = oldest, data[n-1] = newest)</param>
    /// <param name="count">Number of valid values in data</param>
    /// <param name="degree">Polynomial degree</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SolvePoly(ReadOnlySpan<double> data, int count, int degree)
    {
        int m = degree;
        int sz = m + 1;

        // Power sums and cross products accumulate with normalized t ∈ [0, 1].
        // Max degree=6 → sz=7, matrix=7*8=56 doubles + powSums=13 + crossSums=7 — all stackalloc safe.
        Span<double> powSums = stackalloc double[2 * m + 1];
        Span<double> crossSums = stackalloc double[sz];
        Span<double> aug = stackalloc double[sz * (sz + 1)]; // augmented matrix row-major

        powSums.Clear();
        crossSums.Clear();
        aug.Clear();

        double tScale = count > 1 ? 1.0 / (count - 1) : 0.0;
        for (int i = 0; i < count; i++)
        {
            double v = data[i];
            double t = i * tScale; // t=0 for oldest (i=0), t=1 for newest (i=count-1)
            double tk = 1.0;
            for (int k = 0; k <= 2 * m; k++)
            {
                powSums[k] += tk;
                tk *= t;
            }
            tk = 1.0;
            for (int k = 0; k <= m; k++)
            {
                crossSums[k] = Math.FusedMultiplyAdd(tk, v, crossSums[k]);
                tk *= t;
            }
        }

        // Build augmented matrix: G[row,col] = powSums[row+col], rhs[row] = crossSums[row]
        int stride = sz + 1;
        for (int row = 0; row < sz; row++)
        {
            for (int col = 0; col < sz; col++)
            {
                aug[row * stride + col] = powSums[row + col];
            }
            aug[row * stride + sz] = crossSums[row];
        }

        // Gaussian elimination with partial pivoting
        for (int col = 0; col < sz; col++)
        {
            int pivotRow = col;
            double pivotMax = Math.Abs(aug[col * stride + col]);
            for (int row = col + 1; row < sz; row++)
            {
                double absVal = Math.Abs(aug[row * stride + col]);
                if (absVal > pivotMax)
                {
                    pivotMax = absVal;
                    pivotRow = row;
                }
            }

            if (pivotMax < 1e-12)
            {
                return double.NaN; // Singular — caller substitutes raw price
            }

            if (pivotRow != col)
            {
                int colOff = col * stride;
                int pivOff = pivotRow * stride;
                for (int k = col; k <= sz; k++)
                {
                    (aug[colOff + k], aug[pivOff + k]) = (aug[pivOff + k], aug[colOff + k]);
                }
            }

            double diag = aug[col * stride + col];
            for (int row = col + 1; row < sz; row++)
            {
                double factor = aug[row * stride + col] / diag;
                for (int k = col; k <= sz; k++)
                {
                    aug[row * stride + k] = Math.FusedMultiplyAdd(-factor, aug[col * stride + k], aug[row * stride + k]);
                }
            }
        }

        // Back-substitution → coefficients a[0..m]
        Span<double> a = stackalloc double[sz];
        for (int row = sz - 1; row >= 0; row--)
        {
            double val = aug[row * stride + sz];
            for (int k = row + 1; k < sz; k++)
            {
                val = Math.FusedMultiplyAdd(-aug[row * stride + k], a[k], val);
            }
            a[row] = val / aug[row * stride + row];
        }

        // Evaluate polynomial at t=1: P(1) = a0 + a1 + a2 + ... + am
        double result = 0.0;
        for (int k = 0; k < sz; k++)
        {
            result += a[k];
        }
        return result;
    }

    /// <summary>
    /// Public entry point for the validation tests: accepts oldest-first data,
    /// returns the polynomial fit evaluated at t=1 (the newest bar endpoint).
    /// </summary>
    public static double ComputePolyfit(ReadOnlySpan<double> data, int degree)
    {
        if (data.Length < 1)
        {
            return double.NaN;
        }
        int m = Math.Min(degree, data.Length - 1);
        if (m < 1)
        {
            return data[^1];
        }
        return SolvePoly(data, data.Length, m);
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
        int minPoints = _degree + 1;
        if (count < minPoints)
        {
            result = _buffer.Newest;
        }
        else
        {
            // Get buffer in chronological oldest-first order for SolvePoly
            const int StackAllocThreshold = 256;
            double[]? rented = count > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(count) : null;
            Span<double> data = rented != null
                ? rented.AsSpan(0, count)
                : stackalloc double[count];

            try
            {
                // RingBuffer.GetSpan() returns oldest-first — matches SolvePoly t=0..1 convention
                _buffer.GetSpan().CopyTo(data);

                double solved = SolvePoly(data, count, _degree);
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
        Batch(source.Values, vSpan, _period, _degree, initialLastValid);
        source.Times.CopyTo(tSpan);

        // Restore streaming state by replaying last 'period' bars
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

    public static TSeries Batch(TSeries source, int period, int degree = 2)
    {
        var pf = new Polyfit(period, degree);
        return pf.Update(source);
    }

    /// <summary>
    /// Calculates Polyfit in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance. Data oldest-first.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int degree = 2, double initialLastValid = double.NaN)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (degree < 1)
        {
            throw new ArgumentException("Degree must be at least 1", nameof(degree));
        }

        int m = Math.Min(degree, period - 1);
        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;

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
            // Build NaN-corrected array (oldest-first matches source order)
            double lastValid = initialLastValid;
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
                    clean[i] = double.IsFinite(lastValid) ? lastValid : 0.0;
                }
            }

            int minPoints = m + 1;
            for (int i = 0; i < len; i++)
            {
                int n = Math.Min(i + 1, period);
                if (n < minPoints)
                {
                    output[i] = clean[i];
                }
                else
                {
                    // Window is clean[i-n+1..i] already oldest-first
                    Span<double> data = dataBuffer[..n];
                    clean.Slice(i - n + 1, n).CopyTo(data);

                    double solved = SolvePoly(data, n, m);
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

    public static (TSeries Results, Polyfit Indicator) Calculate(TSeries source, int period, int degree = 2)
    {
        var indicator = new Polyfit(period, degree);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _state.LastValidValue = double.NaN;
        _p_state = default;
        Last = default;
    }

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
