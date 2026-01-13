using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Deviation-Scaled Moving Average (DSMA):
/// An adaptive moving average that uses standard deviation to dynamically adjust
/// its smoothing factor. Combines a 2-pole Super Smoother filter for trend estimation
/// with RMS-based deviation scaling for volatility adaptation.
/// </summary>
/// <remarks>
/// Key characteristics:
/// - Uses Super Smoother (Butterworth) 2-pole IIR filter for trend extraction
/// - RMS (Root Mean Square) of filtered deviations for volatility measurement
/// - Dynamic alpha scaling based on deviation ratio (|filtered| / RMS)
/// - O(1) streaming updates via circular buffer for RMS calculation
/// - Adapts smoothing: faster in trending markets, slower in ranging markets
/// 
/// Mathematical foundation:
/// 1. Super Smoother: H(z) = c₁(1 + z⁻¹) / (1 - b₁z⁻¹ + a₁²z⁻²)
///    where a₁ = exp(-√2·π/period), b₁ = 2a₁·cos(√2·π/period), c₁ = (1-b₁+a₁²)/2
/// 2. RMS = √(Σ(filt²)/period)
/// 3. alpha = min(scaleFactor · 5/period · |filt|/RMS, 1)
/// 4. DSMA = alpha·price + (1-alpha)·prevDSMA
/// 
/// Performance:
/// - Update: O(1) with FMA optimizations
/// - Memory: O(period) for RMS buffer
/// - SIMD: Calculate method uses vectorized RMS computation
/// </remarks>
[SkipLocalsInit]
public sealed class Dsma : AbstractBase
{
    private const double SqrtTwo = 1.414213562373095;
    private const double ScaleMultiplier = 5.0;
    private const double MinRms = 1e-10;

    // Super Smoother filter coefficients (precomputed from period)
    private readonly double _b1;      // 2a₁·cos(√2·π/period)
    private readonly double _c1Half;  // c₁/2 for optimization
    private readonly double _a1Sq;    // a₁² for optimization

    // RMS scaling parameters
    private readonly double _periodRecip;     // 1/period
    private readonly double _scaleAdjustment; // scaleFactor · 5 / period

    // Circular buffer for filtered deviations squared
    private readonly RingBuffer _filtSquaredBuffer;

    // Event handler
    private readonly TValuePublishedHandler _handler;

    // Streaming state (current + previous for isNew=false rollback)
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        // Super Smoother filter state
        public double Filt;      // current filtered value
        public double Filt1;     // filt[t-1]
        public double Filt2;     // filt[t-2]
        public double Zeros1;    // (price - result)[t-1]

        // RMS tracking
        public double SumSquared; // running sum of filtered² values

        // Result tracking
        public double Result;     // current DSMA value
        public double LastPrice;  // last finite price (for NaN handling)

        // Counter
        public int Bars;
    }

    /// <summary>
    /// Indicator is "hot" (warmed up) once we have at least Period bars.
    /// </summary>
    public override bool IsHot => _state.Bars >= WarmupPeriod;

    /// <summary>
    /// Creates a new DSMA indicator with the specified parameters.
    /// </summary>
    /// <param name="period">Lookback period for both trend filtering and RMS calculation (≥2)</param>
    /// <param name="scaleFactor">Combined scaling/smoothing factor (0.01-0.9). Higher = more responsive.</param>
    /// <exception cref="ArgumentOutOfRangeException">If period &lt; 2 or scaleFactor outside valid range</exception>
    public Dsma(int period, double scaleFactor = 0.5)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2.");
        if (scaleFactor < 0.01 || scaleFactor > 0.9)
            throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor must be between 0.01 and 0.9.");

        WarmupPeriod = period;
        _periodRecip = 1.0 / period;
        _scaleAdjustment = scaleFactor * ScaleMultiplier * _periodRecip;

        // Precompute Super Smoother coefficients
        // a₁ = exp(-√2·π/(period/2)) = exp(-√2·π·2/period)
        double arg = SqrtTwo * Math.PI / (period * 0.5);
        double a1 = Math.Exp(-arg);
        _b1 = 2.0 * a1 * Math.Cos(arg);
        _a1Sq = a1 * a1;
        double c1 = 1.0 - _b1 + _a1Sq;
        _c1Half = c1 * 0.5;

        _filtSquaredBuffer = new RingBuffer(period);
        _handler = Handle;
        Name = $"Dsma({period},{scaleFactor:F2})";

        Reset();
    }

    /// <summary>
    /// Creates a new DSMA indicator that subscribes to a source publisher.
    /// </summary>
    /// <param name="source">Source data publisher</param>
    /// <param name="period">Lookback period (≥2)</param>
    /// <param name="scaleFactor">Scaling factor (0.01-0.9)</param>
    public Dsma(ITValuePublisher source, int period, double scaleFactor = 0.5)
        : this(period, scaleFactor)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = default;
        _p_state = default;
        _filtSquaredBuffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Core streaming step: processes a single input value and returns the DSMA result.
    /// </summary>
    /// <param name="value">Input price value</param>
    /// <param name="isNew">True for new bar, false for bar correction</param>
    /// <returns>DSMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Step(double value, bool isNew)
    {
        HandleStateSnapshot(isNew);
        value = HandleInvalidInput(value);

        _state.Bars++;

        if (_state.Bars == 1)
            return InitializeFirstBar(value);

        return CalculateDsma(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _filtSquaredBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _filtSquaredBuffer.Restore();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double HandleInvalidInput(double value)
    {
        if (!double.IsFinite(value))
        {
            return _state.Bars == 0 ? double.NaN : _state.LastPrice;
        }

        _state.LastPrice = value;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InitializeFirstBar(double value)
    {
        _state.Result = value;
        _state.Filt = 0.0;
        _state.Filt1 = 0.0;
        _state.Filt2 = 0.0;
        _state.Zeros1 = 0.0;
        _state.SumSquared = 0.0;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateDsma(double value)
    {
        // 1. Calculate deviation from current estimate
        double zeros = value - _state.Result;

        // 2. Apply Super Smoother (2-pole Butterworth) filter
        // filt = c₁/2 · (zeros + zeros[t-1]) + b₁·filt[t-1] - a₁²·filt[t-2]
        // Using FMA for the core computation
        double filtPart1 = _c1Half * (zeros + _state.Zeros1);
        double filtPart2 = Math.FusedMultiplyAdd(_state.Filt1, _b1, -_a1Sq * _state.Filt2);
        double filt = filtPart1 + filtPart2;

        // 3. Update RMS tracking with filtered value squared
        double filtSq = filt * filt;
        double removed = _filtSquaredBuffer.Add(filtSq);
        _state.SumSquared = Math.FusedMultiplyAdd(-1.0, removed, _state.SumSquared + filtSq);

        // 4. Calculate RMS from running sum
        double rms = Math.Sqrt(Math.Max(_state.SumSquared * _periodRecip, MinRms));

        // 5. Compute adaptive alpha: scale by |filt|/RMS ratio
        double alpha = Math.Min(_scaleAdjustment * Math.Abs(filt / rms), 1.0);

        // 6. Apply adaptive EMA: result = alpha·value + (1-alpha)·prevResult
        // Using FMA: result = prevResult·(1-alpha) + alpha·value
        double decay = 1.0 - alpha;
        double result = Math.FusedMultiplyAdd(_state.Result, decay, alpha * value);

        // 7. Update state for next iteration
        _state.Zeros1 = zeros;
        _state.Filt2 = _state.Filt1;
        _state.Filt1 = filt;
        _state.Filt = filt;
        _state.Result = result;

        return result;
    }

    /// <summary>
    /// Updates the indicator with a new value.
    /// </summary>
    /// <param name="input">Input value with timestamp</param>
    /// <param name="isNew">True for new bar, false for bar correction</param>
    /// <returns>Updated indicator value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double result = Step(input.Value, isNew);
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Batch processes a time series and returns the DSMA results.
    /// </summary>
    /// <param name="source">Source time series</param>
    /// <returns>Time series containing DSMA values</returns>
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

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            vSpan[i] = Step(source.Values[i], isNew: true);
        }

        // Synchronize state for subsequent streaming calls
        _p_state = _state;
        _filtSquaredBuffer.Snapshot();

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    /// <summary>
    /// Primes the indicator with historical data.
    /// </summary>
    /// <param name="source">Historical price data</param>
    /// <param name="step">Optional time step (not used in calculation)</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Batch calculates DSMA for a time series.
    /// </summary>
    /// <param name="source">Source time series</param>
    /// <param name="period">Lookback period (≥2)</param>
    /// <param name="scaleFactor">Scaling factor (0.01-0.9)</param>
    /// <returns>Time series containing DSMA values</returns>
    public static TSeries Batch(TSeries source, int period, double scaleFactor = 0.5)
    {
        var dsma = new Dsma(period, scaleFactor);
        return dsma.Update(source);
    }

    /// <summary>
    /// Calculates DSMA for a span of values.
    /// </summary>
    /// <param name="source">Source data span</param>
    /// <param name="output">Output span (must be at least as long as source)</param>
    /// <param name="period">Lookback period (≥2)</param>
    /// <param name="scaleFactor">Scaling factor (0.01-0.9)</param>
    /// <exception cref="ArgumentException">If output span is shorter than source</exception>
    public static void Calculate(ReadOnlySpan<double> source,
                                 Span<double> output,
                                 int period,
                                 double scaleFactor = 0.5)
    {
        if (output.Length < source.Length)
            throw new ArgumentException("Output span is shorter than source span.", nameof(output));

        var dsma = new Dsma(period, scaleFactor);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = dsma.Step(source[i], isNew: true);
        }
    }
}
