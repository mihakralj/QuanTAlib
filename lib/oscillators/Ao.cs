using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// AO: Awesome Oscillator
/// A momentum indicator that reflects the precise changes in the market driving force.
/// It is used to affirm trends or to anticipate possible reversals.
/// </summary>
/// <remarks>
/// The AO calculation process:
/// 1. Calculates the 5-period simple moving average of the HL2 (High+Low)/2 values.
/// 2. Calculates the 34-period simple moving average of the HL2 (High+Low)/2 values.
/// 3. Subtracts the 34-period SMA from the 5-period SMA.
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Positive values indicate bullish momentum
/// - Negative values indicate bearish momentum
/// - Crosses above zero suggest buying opportunities
/// - Crosses below zero suggest selling opportunities
///
/// Formula:
/// AO = SMA(HL2, 5) - SMA(HL2, 34)
///
/// Sources:
///     Bill Williams - "Trading Chaos" (1995)
///     https://www.investopedia.com/terms/a/awesomeoscillator.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Ao : AbstractBase
{
    private readonly Sma _sma5;
    private readonly Sma _sma34;

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ao(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ao()
    {
        _sma5 = new Sma(5);
        _sma34 = new Sma(34);
        WarmupPeriod = 34;
        Name = "AO";
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
        _sma5.Calc(BarInput.HL2, BarInput.IsNew);
        _sma34.Calc(BarInput.HL2, BarInput.IsNew);

        return _sma5.Value - _sma34.Value;
    }
}
