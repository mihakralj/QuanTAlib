using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DOSC: Derivative Oscillator
/// A momentum indicator that combines the Relative Strength Index (RSI) and the Moving Average Convergence Divergence (MACD) to identify potential trend reversals.
/// </summary>
/// <remarks>
/// The DOSC calculation process:
/// 1. Calculate the RSI
/// 2. Calculate the MACD of the RSI
/// 3. Calculate the signal line (SMA) of the MACD
/// 4. Subtract the signal line from the MACD to get the DOSC
///
/// Key characteristics:
/// - Combines RSI and MACD
/// - Oscillates above and below zero
/// - Positive values indicate bullish momentum
/// - Negative values indicate bearish momentum
/// - Crosses above zero suggest buying opportunities
/// - Crosses below zero suggest selling opportunities
///
/// Formula:
/// DOSC = MACD(RSI) - Signal(MACD(RSI))
///
/// Sources:
///     Original development
///     https://www.investopedia.com/terms/d/derivativeoscillator.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Dosc : AbstractBase
{
    private readonly Rsi _rsi;
    private readonly Macd _macd;
    private readonly Sma _signal;

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dosc(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dosc()
    {
        _rsi = new Rsi();
        _macd = new Macd();
        _signal = new Sma(9);
        WarmupPeriod = 34; // RSI requires 14 periods + MACD requires 26 periods + 9 for signal line
        Name = "DOSC";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);
        var rsi = _rsi.Calc(BarInput.Close, BarInput.IsNew);
        var macd = _macd.Calc(rsi, BarInput.IsNew);
        _signal.Calc(macd, BarInput.IsNew);

        return macd - _signal.Value;
    }
}
