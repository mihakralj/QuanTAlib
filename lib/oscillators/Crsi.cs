using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CRSI: Connor RSI
/// A momentum oscillator that combines three different RSI time periods to provide
/// a more comprehensive view of price momentum. It helps identify overbought and
/// oversold conditions with higher accuracy than traditional RSI.
/// </summary>
/// <remarks>
/// The CRSI calculation process:
/// 1. Calculate three RSIs with different periods (3,2,1)
/// 2. Sum the three RSI values
/// 3. Divide by 3 to get the average
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - More responsive than traditional RSI
/// - Combines multiple timeframes
/// - Traditional overbought level at 90
/// - Traditional oversold level at 10
///
/// Formula:
/// CRSI = (RSI(3) + RSI(2) + RSI(1)) / 3
/// where each RSI is calculated using standard RSI formula:
/// RSI = 100 - (100 / (1 + RS))
/// RS = Average Gain / Average Loss
///
/// Sources:
///     Larry Connors - "Short-term Trading Strategies That Work"
///     https://www.tradingview.com/script/cYk1LVpw-Connors-RSI-LazyBear/
///
/// Note: Default periods are 3,2,1 as recommended by Connors
/// </remarks>
[SkipLocalsInit]
public sealed class Crsi : AbstractBase
{
    private readonly Rsi _rsi3;
    private readonly Rsi _rsi2;
    private readonly Rsi _rsi1;
    private const int DefaultPeriod1 = 3;
    private const int DefaultPeriod2 = 2;
    private const int DefaultPeriod3 = 1;

    /// <param name="period1">The first RSI period (default 3).</param>
    /// <param name="period2">The second RSI period (default 2).</param>
    /// <param name="period3">The third RSI period (default 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Crsi(int period1 = DefaultPeriod1, int period2 = DefaultPeriod2, int period3 = DefaultPeriod3)
    {
        if (period1 < 1)
            throw new ArgumentOutOfRangeException(nameof(period1), "Period1 must be greater than 0");
        if (period2 < 1)
            throw new ArgumentOutOfRangeException(nameof(period2), "Period2 must be greater than 0");
        if (period3 < 1)
            throw new ArgumentOutOfRangeException(nameof(period3), "Period3 must be greater than 0");

        _rsi3 = new(period1);
        _rsi2 = new(period2);
        _rsi1 = new(period3);
        WarmupPeriod = Math.Max(Math.Max(period1, period2), period3) + 1;
        Name = $"CRSI({period1},{period2},{period3})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period1">The first RSI period.</param>
    /// <param name="period2">The second RSI period.</param>
    /// <param name="period3">The third RSI period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Crsi(object source, int period1 = DefaultPeriod1, int period2 = DefaultPeriod2, int period3 = DefaultPeriod3)
        : this(period1, period2, period3)
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

        // Calculate individual RSIs
        double rsi3 = _rsi3.Calc(Input);
        double rsi2 = _rsi2.Calc(Input);
        double rsi1 = _rsi1.Calc(Input);

        // Average the three RSIs
        return (rsi3 + rsi2 + rsi1) / 3.0;
    }
}
