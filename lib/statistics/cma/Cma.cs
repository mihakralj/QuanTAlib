using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CMA: Cumulative Moving Average (Running Average / Cumulative Mean)
/// </summary>
/// <remarks>
/// CMA calculates the arithmetic mean of ALL data points seen so far, not just a fixed window.
/// Uses Welford's algorithm with FMA (Fused Multiply-Add) for maximum numerical precision.
///
/// Calculation:
/// M_n = M_(n-1) + α * (x_n - M_(n-1))  where α = 1/n
///
/// Implemented using FMA for single-rounding precision:
/// mean = FusedMultiplyAdd(alpha, delta, mean)
///
/// This is equivalent to:
/// M_n = ((n-1) * M_(n-1) + x_n) / n
///
/// Key Features:
/// - Zero window: includes ALL historical data with equal weight
/// - O(1) time complexity per update
/// - Maximum precision: FMA avoids intermediate rounding of alpha*delta
/// - Numerically stable: avoids overflow from summing large sequences
/// - No buffer required: only stores count and mean
///
/// IsHot:
/// Always true after the first value (no warmup period needed).
/// </remarks>
[SkipLocalsInit]
public sealed class Cma : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Mean, long Count, double LastValidValue);
    private State _state;
    private State _p_state;

    private readonly TValuePublishedHandler _handler;

    /// <summary>
    /// Creates a new CMA indicator instance.
    /// No period parameter required since CMA averages all values.
    /// </summary>
    public Cma()
    {
        Name = "Cma";
        WarmupPeriod = 1;
        _handler = Handle;
    }

    /// <summary>
    /// Creates CMA with a source to subscribe to.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    public Cma(ITValuePublisher source) : this()
    {
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates CMA with a TSeries source to prime from and subscribe to.
    /// </summary>
    /// <param name="source">TSeries source</param>
    public Cma(TSeries source) : this()
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode B: Streaming (Stateful)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// True if the CMA has enough data to produce valid results.
    /// CMA is "hot" after the first value since no warmup is needed.
    /// </summary>
    public override bool IsHot => _state.Count > 0;

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode C: Priming (The Bridge)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    /// <param name="step">Time interval between values (not used for CMA)</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _state = default;
        _p_state = default;

        // Find first valid value to seed lastValid
        for (int i = 0; i < source.Length; i++)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        // Process all values using Welford's algorithm with FMA
        for (int i = 0; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _state.Count++;
            double alpha = 1.0 / _state.Count;
            double delta = val - _state.Mean;
            _state.Mean = Math.FusedMultiplyAdd(alpha, delta, _state.Mean);
        }

        Last = new TValue(DateTime.MinValue, _state.Mean);
        _p_state = _state;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);
        _state.Count++;
        double alpha = 1.0 / _state.Count;
        double delta = val - _state.Mean;
        _state.Mean = Math.FusedMultiplyAdd(alpha, delta, _state.Mean);

        Last = new TValue(input.Time, _state.Mean);
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

        Batch(source.Values, vSpan);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode A: Batch (Stateless)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Calculates CMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>CMA series</returns>
    public static TSeries Batch(TSeries source)
    {
        var cma = new Cma();
        return cma.Update(source);
    }

    /// <summary>
    /// Calculates CMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Uses Welford's algorithm for numerical stability.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        int len = source.Length;
        if (len == 0) return;

        double mean = 0;
        double lastValid = double.NaN;

        // Find first valid value to seed lastValid
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                break;
            }
        }

        // Welford's algorithm for running mean with FMA
        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // M_n = M_(n-1) + alpha * delta using FMA for single-rounding precision
            double alpha = 1.0 / (i + 1);
            double delta = val - mean;
            mean = Math.FusedMultiplyAdd(alpha, delta, mean);
            output[i] = mean;
        }
    }

    /// <summary>
    /// Runs a batch calculation on history and returns
    /// a "Hot" Cma instance ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Cma Indicator) Calculate(TSeries source)
    {
        var cma = new Cma();
        TSeries results = cma.Update(source);
        return (results, cma);
    }

    /// <summary>
    /// Resets the CMA state.
    /// </summary>
    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }
}