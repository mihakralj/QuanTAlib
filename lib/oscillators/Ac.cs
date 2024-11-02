using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// AC: Acceleration/Deceleration Oscillator
/// A momentum indicator that measures the acceleration and deceleration of the current driving force.
/// It is derived from the Awesome Oscillator (AO) and helps identify potential trend reversals.
/// </summary>
/// <remarks>
/// The AC calculation process:
/// 1. Calculate the Awesome Oscillator (AO)
/// 2. Calculate a 5-period simple moving average of the AO
/// 3. Subtract the 5-period SMA from the current AO value
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Measures the acceleration/deceleration of market driving force
/// - Positive values indicate increasing momentum
/// - Negative values indicate decreasing momentum
/// - Can be used to identify potential trend reversals
///
/// Formula:
/// AC = AO - SMA(AO, 5)
///
/// Sources:
///     Bill Williams - "Trading Chaos" (1995)
///     https://www.investopedia.com/terms/a/ac.asp
/// </remarks>

[SkipLocalsInit]
public sealed class Ac : AbstractBase
{
    private readonly Ao _ao;
    private readonly Sma _sma5;

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ac(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ac()
    {
        _ao = new Ao();
        _sma5 = new Sma(5);
        WarmupPeriod = 39; // AO requires 34 periods + 5 for AC's SMA
        Name = "AC";
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
        var ao = _ao.Calc(BarInput, BarInput.IsNew);
        _sma5.Calc(ao, BarInput.IsNew);

        return ao - _sma5.Value;
    }
}
