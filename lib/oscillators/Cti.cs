using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CTI: Ehler's Correlation Trend Indicator
/// A momentum oscillator that measures the correlation between the price and a lagged version of the price.
/// </summary>
/// <remarks>
/// The CTI calculation process:
/// 1. Calculate the correlation between the price and a lagged version of the price over a specified period.
/// 2. Normalize the correlation values to oscillate between -1 and 1.
/// 3. Use the normalized correlation values to calculate the CTI.
///
/// Key characteristics:
/// - Oscillates between -1 and 1
/// - Positive values indicate bullish momentum
/// - Negative values indicate bearish momentum
///
/// Formula:
/// CTI = 2 * (Correlation - 0.5)
///
/// Sources:
///     John Ehlers - "Cybernetic Analysis for Stocks and Futures" (2004)
///     https://www.investopedia.com/terms/c/correlation-trend-indicator.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Cti : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _priceBuffer;
    private readonly Corr _correlation;

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The calculation period (default: 20)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cti(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cti(int period = 20)
    {
        _period = period;
        _priceBuffer = new CircularBuffer(period);
        _correlation = new Corr(period);
        WarmupPeriod = period;
        Name = "CTI";
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
        ManageState(Input.IsNew);

        _priceBuffer.Add(Input.Value, Input.IsNew);
        var laggedPrice = _index >= _period ? _priceBuffer[_index - _period] : double.NaN;

        _correlation.Calc(new TValue(Input.Time, Input.Value, Input.IsNew), new TValue(Input.Time, laggedPrice, Input.IsNew));

        if (_index < _period - 1) return double.NaN;

        var correlation = _correlation.Value;
        return 2 * (correlation - 0.5);
    }
}
