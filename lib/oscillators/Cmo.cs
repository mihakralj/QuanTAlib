using System;
namespace QuanTAlib;

/// <summary>
/// CMO: Chande Momentum Oscillator
/// A technical momentum indicator that measures the difference between upward and
/// downward momentum. CMO helps identify overbought and oversold conditions, as
/// well as trend strength and potential reversals.
/// </summary>
/// <remarks>
/// The CMO calculation process:
/// 1. Calculates price differences from previous period
/// 2. Separates positive (upward) and negative (downward) movements
/// 3. Sums upward and downward movements over period
/// 4. Calculates: 100 * ((sumUp - sumDown) / (sumUp + sumDown))
///
/// Key characteristics:
/// - Oscillates between -100 and +100
/// - Values above +50 indicate overbought
/// - Values below -50 indicate oversold
/// - Zero line crossovers signal trend changes
/// - High absolute values suggest strong trends
///
/// Formula:
/// CMO = 100 * ((ΣUp - ΣDown) / (ΣUp + ΣDown))
/// where:
/// Up = positive price changes
/// Down = absolute negative price changes
///
/// Sources:
///     Tushar Chande - "The New Technical Trader" (1994)
///     https://www.investopedia.com/terms/c/chandemomentumoscillator.asp
///
/// Note: Similar to RSI but with different scaling and calculation method
/// </remarks>

public class Cmo : AbstractBase
{
    private readonly CircularBuffer _sumH;
    private readonly CircularBuffer _sumL;
    private double _prevValue, _p_prevValue;

    /// <param name="period">The number of periods used in the CMO calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Cmo(int period)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _sumH = new(period);
        _sumL = new(period);

        WarmupPeriod = period + 1;
        Name = $"CMO({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the CMO calculation.</param>
    public Cmo(object source, int period) : this(period)
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

        if (_index == 0)
        {
            _prevValue = Input.Value;
        }

        // Calculate price difference
        double diff = Input.Value - _prevValue;
        _prevValue = Input.Value;

        // Separate upward and downward movements
        if (diff > 0)
        {
            _sumH.Add(diff, Input.IsNew);
            _sumL.Add(0, Input.IsNew);
        }
        else
        {
            _sumH.Add(0, Input.IsNew);
            _sumL.Add(-diff, Input.IsNew);
        }

        // Calculate sums for the specified period
        double sumH = _sumH.Sum();
        double sumL = _sumL.Sum();
        double divisor = sumH + sumL;

        // Calculate CMO value
        return (Math.Abs(divisor) > double.Epsilon) ?
            100.0 * ((sumH - sumL) / divisor) :
            0.0;
    }
}
