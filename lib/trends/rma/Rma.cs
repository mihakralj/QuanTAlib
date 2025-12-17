using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// RMA: Running Moving Average (also known as Wilder's Moving Average or SMMA)
/// </summary>
/// <remarks>
/// RMA is an Exponential Moving Average (EMA) with a different smoothing factor.
/// While EMA uses alpha = 2 / (period + 1), RMA uses alpha = 1 / period.
///
/// Calculation:
/// alpha = 1 / period
/// RMA_new = RMA_old + alpha * (newest - RMA_old)
///
/// This implementation wraps the EMA implementation to ensure identical behavior and performance,
/// utilizing the same O(1) update complexity and zero-allocation architecture.
/// </remarks>
[SkipLocalsInit]
public sealed class Rma : AbstractBase
{
    private readonly Ema _ema;

    /// <summary>
    /// Creates RMA with specified period.
    /// Alpha = 1 / period
    /// </summary>
    /// <param name="period">Period for RMA calculation (must be > 0)</param>
    public Rma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _ema = new Ema(1.0 / period);
        Name = $"Rma({period})";
        WarmupPeriod = _ema.WarmupPeriod;
    }

    /// <summary>
    /// Creates RMA with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for RMA calculation</param>
    public Rma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Creates RMA with specified source and period.
    /// </summary>
    /// <param name="source">Source series</param>
    /// <param name="period">Period for RMA calculation</param>
    public Rma(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// True if the RMA has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _ema.IsHot;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    public override void Prime(ReadOnlySpan<double> source)
    {
        _ema.Prime(source);
        Last = _ema.Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        TValue result = _ema.Update(input, isNew);
        Last = result;
        PubEvent(Last);
        return result;
    }

    public override TSeries Update(TSeries source)
    {
        TSeries result = _ema.Update(source);
        Last = _ema.Last;
        return result;
    }

    /// <summary>
    /// Calculates RMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">RMA period</param>
    /// <returns>RMA series</returns>
    public static TSeries Batch(TSeries source, int period)
    {
        var rma = new Rma(period);
        return rma.Update(source);
    }

    /// <summary>
    /// Calculates RMA in-place using period, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Alpha = 1 / period
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">RMA period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 1.0 / period;
        Ema.Batch(source, output, alpha);
    }

    /// <summary>
    /// Runs a high-performance batch calculation on history and returns
    /// a "Hot" Rma instance ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <param name="period">RMA Period</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Rma Indicator) Calculate(TSeries source, int period)
    {
        var rma = new Rma(period);
        TSeries results = rma.Update(source);
        return (results, rma);
    }

    /// <summary>
    /// Resets the RMA state.
    /// </summary>
    public override void Reset()
    {
        _ema.Reset();
        Last = default;
    }
}
