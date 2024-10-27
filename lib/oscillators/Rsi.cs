using System;
namespace QuanTAlib;

/// <summary>
/// RSI: Relative Strength Index
/// A momentum oscillator that measures the speed and magnitude of recent price
/// changes to evaluate overbought or oversold conditions. RSI compares the
/// magnitude of recent gains to recent losses.
/// </summary>
/// <remarks>
/// The RSI calculation process:
/// 1. Calculates price changes from previous period
/// 2. Separates gains and losses
/// 3. Calculates average gain and loss using Wilder's smoothing
/// 4. Computes relative strength (avg gain / avg loss)
/// 5. Normalizes to 0-100 scale: 100 - (100 / (1 + RS))
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Traditional overbought level at 70
/// - Traditional oversold level at 30
/// - Centerline (50) crossovers signal trend changes
/// - Divergences suggest potential reversals
///
/// Formula:
/// RSI = 100 - (100 / (1 + RS))
/// where:
/// RS = Average Gain / Average Loss
/// Average Gain/Loss = Wilder's smoothed average over period
///
/// Sources:
///     J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     https://www.investopedia.com/terms/r/rsi.asp
///
/// Note: Default period of 14 was recommended by Wilder
/// </remarks>

public class Rsi : AbstractBase
{
    private readonly Rma _avgGain;
    private readonly Rma _avgLoss;
    private double _prevValue, _p_prevValue;

    /// <param name="period">The number of periods used in the RSI calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rsi(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _avgGain = new(period, useSma: true);
        _avgLoss = new(period, useSma: true);
        _index = 0;
        WarmupPeriod = period + 1;
        Name = $"RSI({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the RSI calculation.</param>
    public Rsi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevValue = _prevValue;
        }
        else
        {
            _prevValue = _p_prevValue;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _prevValue = Input.Value;
        }

        // Calculate price change and separate gains/losses
        double change = Input.Value - _prevValue;
        double gain = Math.Max(change, 0);
        double loss = Math.Max(-change, 0);
        _prevValue = Input.Value;

        // Calculate smoothed averages using Wilder's method
        _avgGain.Calc(gain, IsNew: Input.IsNew);
        _avgLoss.Calc(loss, IsNew: Input.IsNew);

        // Calculate RSI
        double rsi = (_avgLoss.Value > 0) ? 100 - (100 / (1 + (_avgGain.Value / _avgLoss.Value))) : 100;

        return rsi;
    }
}
