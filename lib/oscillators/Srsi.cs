using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SRSI: Stochastic RSI
/// A momentum oscillator that applies the stochastic formula to RSI values
/// instead of price data. It provides a more sensitive indicator than standard
/// RSI or Stochastic oscillators.
/// </summary>
/// <remarks>
/// The SRSI calculation process:
/// 1. Calculate RSI
/// 2. Apply Stochastic formula to RSI values:
///    - Find highest high and lowest low of RSI over period
///    - Calculate where current RSI is within this range
/// 3. Smooth the result with SMA (signal line)
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - More sensitive than standard RSI
/// - Combines benefits of both RSI and Stochastic
/// - Traditional overbought level at 80
/// - Traditional oversold level at 20
///
/// Formula:
/// SRSI = ((RSI - Lowest RSI) / (Highest RSI - Lowest RSI)) * 100
/// Signal = SMA(SRSI, signalPeriod)
///
/// Sources:
///     Tushar Chande and Stanley Kroll - "The New Technical Trader" (1994)
///     https://www.investopedia.com/terms/s/stochrsi.asp
///
/// Note: Default periods (14,14,3,3) are commonly used values
/// </remarks>
[SkipLocalsInit]
public sealed class Srsi : AbstractBase
{
    private readonly Rsi _rsi;
    private readonly CircularBuffer _rsiValues;
    private readonly CircularBuffer _srsiValues;
    private readonly Sma _signal;
    private readonly int _rsiPeriod;
    private const int DefaultRsiPeriod = 14;
    private const int DefaultStochPeriod = 14;
    private const int DefaultSmoothK = 3;
    private const int DefaultSmoothD = 3;
    private const double ScalingFactor = 100.0;

    /// <param name="rsiPeriod">The RSI period (default 14).</param>
    /// <param name="stochPeriod">The Stochastic period (default 14).</param>
    /// <param name="smoothK">K line smoothing period (default 3).</param>
    /// <param name="smoothD">D line smoothing period (default 3).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Srsi(int rsiPeriod = DefaultRsiPeriod, int stochPeriod = DefaultStochPeriod,
                int smoothK = DefaultSmoothK, int smoothD = DefaultSmoothD)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rsiPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stochPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(smoothK, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(smoothD, 1);

        _rsiPeriod = rsiPeriod;
        _rsi = new(rsiPeriod);
        _rsiValues = new(stochPeriod);
        _srsiValues = new(smoothK);
        _signal = new(smoothD);
        WarmupPeriod = rsiPeriod + stochPeriod + Math.Max(smoothK, smoothD);
        Name = $"SRSI({rsiPeriod},{stochPeriod},{smoothK},{smoothD})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="rsiPeriod">The RSI period.</param>
    /// <param name="stochPeriod">The Stochastic period.</param>
    /// <param name="smoothK">K line smoothing period.</param>
    /// <param name="smoothD">D line smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Srsi(object source, int rsiPeriod = DefaultRsiPeriod, int stochPeriod = DefaultStochPeriod,
                int smoothK = DefaultSmoothK, int smoothD = DefaultSmoothD)
        : this(rsiPeriod, stochPeriod, smoothK, smoothD)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew) _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate RSI
        double rsiValue = _rsi.Calc(Input);

        if (Input.IsNew)
            _rsiValues.Add(rsiValue);

        // Not enough data
        if (_index <= _rsiPeriod)
            return 0;

        // Calculate Stochastic RSI
        double highest = _rsiValues.Max();
        double lowest = _rsiValues.Min();
        double range = highest - lowest;
        double srsi = range >= double.Epsilon ? ((rsiValue - lowest) / range) * ScalingFactor : 0;

        if (Input.IsNew)
            _srsiValues.Add(srsi);

        // Calculate signal line
        return _signal.Calc(new TValue(Input.Time, srsi, Input.IsNew));
    }

    /// <summary>
    /// Gets the K line value (raw Stochastic RSI)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double K() => _srsiValues[0];

    /// <summary>
    /// Gets the D line value (signal line)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double D() => Value;
}
