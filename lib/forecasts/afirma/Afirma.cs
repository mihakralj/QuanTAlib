using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AFIRMA: Autoregressive FIR Moving Average
/// A Windowed Weighted Moving Average that uses standard window functions (Hanning, Hamming,
/// Blackman, Blackman-Harris) as filter coefficients.
/// Optionally applies Least Squares Cubic Polynomial fitting for autoregressive prediction.
/// </summary>
/// <remarks>
/// AFIRMA calculates a weighted average where weights are determined by a window function.
/// Unlike standard WMAs that assume linear or triangle weights, AFIRMA uses signal processing
/// windows to achieve specific frequency response characteristics.
///
/// The filter equation:
///    y[n] = (Σ w_k · x[n-k]) / (Σ w_k)
///
/// Window Functions:
/// - Hanning: 0.5 - 0.5cos(x)
/// - Hamming: 0.54 - 0.46cos(x)
/// - Blackman: 0.42 - 0.5cos(x) + 0.08cos(2x)
/// - Blackman-Harris: 0.35875 - 0.48829cos(x) + 0.14128cos(2x) - 0.01168cos(3x)
///
/// Parameters:
/// - Period: The length of the window.
/// - Window: The window function to use for weights.
/// - LeastSquares: Enable cubic polynomial fitting (default: false).
/// </remarks>
[SkipLocalsInit]
public sealed class Afirma : AbstractBase
{
    /// <summary>
    /// Available window functions for the FIR filter.
    /// </summary>
    public enum WindowType
    {
        /// <summary>No windowing - simple rectangular window (SMA)</summary>
        Rectangular,
        /// <summary>Hanning window</summary>
        Hanning,
        /// <summary>Hamming window</summary>
        Hamming,
        /// <summary>Blackman window (3-term)</summary>
        Blackman,
        /// <summary>Blackman-Harris window (4-term, minimum sidelobe)</summary>
        BlackmanHarris,
    }

    private readonly int _period;
    private readonly WindowType _window;
    private readonly bool _leastSquares;
    private readonly RingBuffer _buffer;
    private readonly double[] _weights;
    private readonly double _invWeightSum;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _publisher;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidValue)
    {
        public static State New() => new() { LastValidValue = double.NaN };
    }

    private State _state = State.New();
    private State _p_state = State.New();

    /// <summary>
    /// Creates AFIRMA with specified parameters.
    /// </summary>
    /// <param name="period">The window size (filter length), must be >= 1.</param>
    /// <param name="window">Window function to apply.</param>
    /// <param name="leastSquares">Enable least squares fitting.</param>
    public Afirma(int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
    {
        if (period < 1)
            throw new ArgumentException("Period must be at least 1", nameof(period));

        _period = period;
        _window = window;
        _leastSquares = leastSquares;
        _buffer = new RingBuffer(period);
        _weights = new double[period];
        _invWeightSum = 1.0 / CalculateWeights();

        Name = $"Afirma({period},{window},{leastSquares})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates AFIRMA with a data source subscription.
    /// </summary>
    public Afirma(ITValuePublisher source, int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
        : this(period, window, leastSquares)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates AFIRMA with TSeries source for priming.
    /// </summary>
    public Afirma(TSeries source, int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
        : this(period, window, leastSquares)
    {
        _publisher = source;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Gets a value indicating whether the most recent update was a new data point.
    /// </summary>
    public bool IsNew => _isNew;

    /// <summary>
    /// True if the AFIRMA has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _buffer.Clear();
        _state = State.New();
        _p_state = State.New();

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Find first valid value for NaN handling
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        // Feed the RingBuffer
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
        }

        // Calculate initial value
        double result = CalculateAfirma();
        Last = new TValue(DateTime.MinValue, result);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input, bool updateState = true)
    {
        if (double.IsFinite(input))
        {
            if (updateState)
                _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value, updateState: false);
        if (double.IsFinite(input.Value))
        {
            _state.LastValidValue = input.Value;
        }

        _buffer.Add(val, isNew);

        double result = CalculateAfirma();
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _window, _leastSquares);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAfirma()
    {
        int count = _buffer.Count;
        if (count == 0) return double.NaN;

        double result;

        // Warmup path or steady state for Base AFIRMA
        if (count < _period)
        {
            result = 0.0;
            double effectiveWeightSum = 0.0;
            for (int k = 0; k < count; k++)
            {
                double w = _weights[k];
                result = Math.FusedMultiplyAdd(_buffer[k], w, result);
                effectiveWeightSum += w;
            }
            result = effectiveWeightSum > 0 ? result / effectiveWeightSum : _buffer.Newest;
        }
        else
        {
            // Steady state
            double sum = 0.0;
            for (int k = 0; k < _period; k++)
            {
                sum = Math.FusedMultiplyAdd(_buffer[k], _weights[k], sum);
            }
            result = sum * _invWeightSum;
        }

        // Least Squares path - overwrites result if enabled and sufficient data
        if (_leastSquares && count > 2)
        {
            int n = Math.Min((count - 1) / 2, 50); // Pine: math.min(math.floor((p - 1) / 2), 50)
            if (n >= 2)
            {
                // Linear Regression on most recent n points (0 to n-1 in Pine lag terms)
                // Pine lag 0 = Newest. Pine lag n-1 = Newest - (n-1).
                // x coordinates: 0, 1, ..., n-1 (lags)
                // y coordinates: buffer values corresponding to lags.
                // we want fitted line: y = intercept + slope * x

                double sx = 0.0, sx2 = 0.0, sy = 0.0, sxy = 0.0;
                // Precalculate sx, sx2 (depends only on n)
                // sx = sum(i) for i=0..n-1 = (n-1)*n/2
                // sx2 = sum(i^2) for i=0..n-1 = (n-1)*n*(2n-2+1)/6 = (n-1)*n*(2n-1)/6
                // Calculation in loop for clarity or formula:
                double dn = (double)n;
                sx = (dn - 1.0) * dn * 0.5;
                sx2 = (dn - 1.0) * dn * (2.0 * dn - 1.0) / 6.0;

                for (int i = 0; i < n; i++)
                {
                    // Pine uses src[i] where i is lag. i=0 is newest.
                    // RingBuffer: Newest is at index count-1.
                    // Value at lag i: _buffer[count - 1 - i]
                    double val = _buffer[count - 1 - i];
                    sy += val;
                    sxy += i * val;
                }

                double denom = dn * sx2 - sx * sx;
                if (Math.Abs(denom) > 1e-10)
                {
                    double slope = (dn * sxy - sx * sy) / denom;
                    double intercept = (sy - slope * sx) / dn;

                    double lsSum = 0.0;
                    double lsCount = 0.0;

                    // Pine loop: for i = 0 to p - 1
                    // if i < n ? fitted : src[i]
                    // We loop over the full period (or count).
                    // We average the "hybrid" window.

                    for (int i = 0; i < count; i++)
                    {
                        double val;
                        if (i < n)
                        {
                            // Use fitted value: intercept + slope * i
                            val = intercept + slope * i;
                        }
                        else
                        {
                            // Use original value from buffer
                            // At lag i
                            val = _buffer[count - 1 - i];
                        }
                        lsSum += val;
                        lsCount++;
                    }
                    if (lsCount > 0)
                    {
                        result = lsSum / lsCount;
                    }
                }
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeights()
    {
        double wsum = 0.0;

        // Coefficients based on Pine Script implementation
        double a0 = 0.35875, a1 = -0.48829, a2 = 0.14128, a3 = -0.01168;

        if (_window == WindowType.Hanning)
        {
            a0 = 0.50; a1 = -0.50; a2 = 0.0; a3 = 0.0;
        }
        else if (_window == WindowType.Hamming)
        {
            a0 = 0.54; a1 = -0.46; a2 = 0.0; a3 = 0.0;
        }
        else if (_window == WindowType.Blackman)
        {
            a0 = 0.42; a1 = -0.50; a2 = 0.08; a3 = 0.0;
        }
        else if (_window == WindowType.Rectangular)
        {
            a0 = 1.0; a1 = 0.0; a2 = 0.0; a3 = 0.0;
        }

        double twoPiDivP = 2.0 * Math.PI / _period;

        for (int k = 0; k < _period; k++)
        {
            double kTwoPiDivP = k * twoPiDivP;
            double coef = a0 + a1 * Math.Cos(kTwoPiDivP);
            if (Math.Abs(a2) > 1e-9)
                coef += a2 * Math.Cos(2.0 * kTwoPiDivP);
            if (Math.Abs(a3) > 1e-9)
                coef += a3 * Math.Cos(3.0 * kTwoPiDivP);

            _weights[k] = coef;
            wsum += coef;
        }
        return wsum;
    }

    /// <summary>
    /// Calculates AFIRMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
    {
        var afirma = new Afirma(period, window, leastSquares);
        return afirma.Update(source);
    }

    /// <summary>
    /// Calculates AFIRMA in-place, writing results to pre-allocated output span.
    /// Optimized with stackalloc and FMA.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be at least 1", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        // If leastSquares is enabled, use standard Update loop via object or specialized loop.
        // Implementing LS efficiently in Batch/Span is complex because of regression in inner loop.
        // For parity and code reuse without duplication, simpler to instantiate object for LS path or duplicate logic.
        // BUT Batch(Span) should remain allocation free if possible.
        // LS logic fits a line for every bar. That's O(Period) per bar. Simpler WMA is also O(Period) or O(1) if optimized sliding but here it's convolution.
        // Given complexity of LS, strict 0-alloc might require large stack buffers for sx/sy/etc or careful math.
        // Let's implement the core logic inside the loop.

        const int StackAllocThreshold = 256;

        // Allocate weights - use ArrayPool for large buffers to avoid heap allocation
        double[]? rentedWeights = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = rentedWeights != null
            ? rentedWeights.AsSpan(0, period)
            : stackalloc double[period];

        // Allocate circular buffer - use ArrayPool for large buffers
        double[]? rentedBuffer = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> buffer = rentedBuffer != null
            ? rentedBuffer.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
        // Pre-calculate weights (Static version of CalculateWeights)
        // ... (Copy of weights calc logic)
        double a0 = 0.35875, a1 = -0.48829, a2 = 0.14128, a3 = -0.01168;
        if (window == WindowType.Hanning) { a0 = 0.50; a1 = -0.50; a2 = 0.0; a3 = 0.0; }
        else if (window == WindowType.Hamming) { a0 = 0.54; a1 = -0.46; a2 = 0.0; a3 = 0.0; }
        else if (window == WindowType.Blackman) { a0 = 0.42; a1 = -0.50; a2 = 0.08; a3 = 0.0; }
        else if (window == WindowType.Rectangular) { a0 = 1.0; a1 = 0.0; a2 = 0.0; a3 = 0.0; }

        double twoPiDivP = 2.0 * Math.PI / period;
        for (int k = 0; k < period; k++)
        {
            double kTwoPiDivP = k * twoPiDivP;
            double coef = a0 + a1 * Math.Cos(kTwoPiDivP);
            if (Math.Abs(a2) > 1e-9) coef += a2 * Math.Cos(2.0 * kTwoPiDivP);
            if (Math.Abs(a3) > 1e-9) coef += a3 * Math.Cos(3.0 * kTwoPiDivP);
            weights[k] = coef;
        }

        double lastValid = double.NaN;
        for (int k = 0; k < len; k++)
            if (double.IsFinite(source[k])) { lastValid = source[k]; break; }

        int bufferIndex = 0;
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val)) lastValid = val; else val = lastValid;

            buffer[bufferIndex] = val;
            bufferIndex = (bufferIndex + 1) % period;
            if (bufferCount < period) bufferCount++;

            // Base AFIRMA (WMA)
            double result = 0.0;
            double effectiveWeightSum = 0.0;
            int readIndex = (bufferIndex - bufferCount + period) % period;

            for (int k = 0; k < bufferCount; k++)
            {
                // Match Streaming: weights[k] corresponds to Oldest + k
                int idx = (readIndex + k) % period;
                result = Math.FusedMultiplyAdd(buffer[idx], weights[k], result);
                effectiveWeightSum += weights[k];
            }
            output[i] = effectiveWeightSum > 0 ? result / effectiveWeightSum : val;

            // Least Squares Path
            if (leastSquares && bufferCount > 2)
            {
                int n = Math.Min((bufferCount - 1) / 2, 50);
                if (n >= 2)
                {
                    double sx = 0.0, sx2 = 0.0, sy = 0.0, sxy = 0.0;
                    double dn = (double)n;
                    sx = (dn - 1.0) * dn * 0.5;
                    sx2 = (dn - 1.0) * dn * (2.0 * dn - 1.0) / 6.0;

                    for (int j = 0; j < n; j++)
                    {
                        // lag j
                        int idx = (readIndex + bufferCount - 1 - j + period) % period;
                        double v = buffer[idx];
                        sy += v;
                        sxy += j * v;
                    }

                    double denom = dn * sx2 - sx * sx;
                    if (Math.Abs(denom) > 1e-10)
                    {
                        double slope = (dn * sxy - sx * sy) / denom;
                        double intercept = (sy - slope * sx) / dn;

                        double lsSum = 0.0;
                        double lsCount = 0.0;

                        for (int j = 0; j < bufferCount; j++)
                        {
                             // lag j
                            double v_ls;
                            if (j < n)
                            {
                                v_ls = intercept + slope * j;
                            }
                            else
                            {
                                int idx = (readIndex + bufferCount - 1 - j + period) % period;
                                v_ls = buffer[idx];
                            }
                            lsSum += v_ls;
                            lsCount++;
                        }
                        if (lsCount > 0)
                        {
                            output[i] = lsSum / lsCount;
                        }
                    }
                }
            }
        }
        }
        finally
        {
            // Return rented arrays to the pool
            if (rentedWeights != null)
                ArrayPool<double>.Shared.Return(rentedWeights);
            if (rentedBuffer != null)
                ArrayPool<double>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Runs a batch calculation and returns a hot indicator instance.
    /// </summary>
    public static (TSeries Results, Afirma Indicator) Calculate(TSeries source, int period, WindowType window = WindowType.BlackmanHarris, bool leastSquares = false)
    {
        var afirma = new Afirma(period, window, leastSquares);
        TSeries results = afirma.Update(source);
        return (results, afirma);
    }

    /// <summary>
    /// Resets the AFIRMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = State.New();
        _p_state = State.New();
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
        }
        base.Dispose(disposing);
    }
}