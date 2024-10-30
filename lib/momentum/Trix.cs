using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TRIX: Triple Exponential Average Rate of Change
/// A momentum oscillator that shows the percentage rate of change of a triple exponentially
/// smoothed moving average. TRIX filters out insignificant price movements and helps identify
/// overbought/oversold conditions and divergences.
/// </summary>
/// <remarks>
/// The TRIX calculation process:
/// 1. Calculate Triple Exponential Moving Average (TEMA)
/// 2. Calculate 1-day Rate of Change (ROC) of the TEMA
///
/// Key characteristics:
/// - Combines trend-following and momentum in one indicator
/// - Filters out price movements deemed insignificant
/// - Oscillates around zero line
/// - Useful for identifying divergences
/// - Helps spot overbought/oversold conditions
///
/// Formula:
/// TEMA = 3*EMA1 - 3*EMA2 + EMA3
/// TRIX = ROC(TEMA, 1) = ((TEMA - TEMA_prev) / TEMA_prev) * 100
///
/// Sources:
///     Jack Hutson - "Technical Analysis of Stocks and Commodities" magazine, 1983
///     John J. Murphy - "Technical Analysis of the Financial Markets"
/// </remarks>

[SkipLocalsInit]
public sealed class Trix : AbstractBase
{
    private readonly Tema _tema;
    private readonly CircularBuffer _temaBuffer;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 18;

    /// <param name="period">The lookback period for TEMA calculation (default 18).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Trix(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _tema = new(period);
        _temaBuffer = new(2); // We only need current and previous TEMA values
        WarmupPeriod = period + 1; // TEMA period + 1 for ROC
        Name = $"TRIX({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period for TEMA calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Trix(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            double temaValue = _tema.Calc(Input);
            _temaBuffer.Add(temaValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_temaBuffer.Count < _temaBuffer.Capacity)
            return 0.0;

        double oldTema = _temaBuffer[0];
        if (oldTema <= double.Epsilon)
            return 0.0;

        double currentTema = _temaBuffer[^1];
        return ((currentTema - oldTema) / oldTema) * ScalingFactor;
    }
}
