using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// BOP: Balance of Power
/// A momentum oscillator that measures the strength of buying and selling pressure by comparing
/// closing prices to their corresponding opening prices.
/// </summary>
/// <remarks>
/// The BOP calculation process:
/// 1. Calculate (Close - Open) / (High - Low) for each period
/// 2. A positive BOP indicates buying pressure (bullish)
/// 3. A negative BOP indicates selling pressure (bearish)
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - No upper or lower bounds
/// - Zero line acts as equilibrium between buying and selling pressure
/// - Can be used to identify potential trend reversals and divergences
///
/// Formula:
/// BOP = (Close - Open) / (High - Low)
///
/// Sources:
///     Igor Livshin (1990s)
///     https://www.investopedia.com/terms/b/bop.asp
/// </remarks>

[SkipLocalsInit]
public sealed class Bop : AbstractBase
{
    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bop(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bop()
    {
        WarmupPeriod = 1;
        Name = "BOP";
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

        var range = BarInput.High - BarInput.Low;
        if (range <= double.Epsilon) return 0;

        return (BarInput.Close - BarInput.Open) / range;
    }
}
