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
public sealed class Rma : ITValuePublisher
{
    private readonly Ema _ema;
    private readonly int _period;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name => $"Rma({_period})";

    public event Action<TValue>? Pub;

    /// <summary>
    /// Creates RMA with specified period.
    /// Alpha = 1 / period
    /// </summary>
    /// <param name="period">Period for RMA calculation (must be > 0)</param>
    public Rma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _ema = new Ema(1.0 / period);
        _ema.Pub += (item) => Pub?.Invoke(item);
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
    /// Current RMA value.
    /// </summary>
    public TValue Last => _ema.Last;

    /// <summary>
    /// True if the RMA has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _ema.IsHot;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return _ema.Update(input, isNew);
    }

    public TSeries Update(TSeries source)
    {
        return _ema.Update(source);
    }

    /// <summary>
    /// Calculates RMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">RMA period</param>
    /// <returns>RMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
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
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 1.0 / period;
        Ema.Calculate(source, output, alpha);
    }

    /// <summary>
    /// Resets the RMA state.
    /// </summary>
    public void Reset()
    {
        _ema.Reset();
    }
}
