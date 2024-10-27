namespace QuanTAlib;

/// <summary>
/// Represents a Chande Momentum Oscillator (CMO) calculator.
/// </summary>
public class Cmo : AbstractBase
{
    private readonly CircularBuffer _sumH;
    private readonly CircularBuffer _sumL;
    private double _prevValue, _p_prevValue;

    public Cmo(int period)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _sumH = new(period);
        _sumL = new(period);

        WarmupPeriod = period+1;
        Name = $"CMO({period})";
    }

    /// <summary>
    /// Initializes a new instance of the CMO class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
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

        double diff = Input.Value - _prevValue;
        _prevValue = Input.Value;

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

        // Calculate sums for the specified period only
        double sumH = _sumH.Sum();
        double sumL = _sumL.Sum();
        double divisor = sumH + sumL;

        return (Math.Abs(divisor) > double.Epsilon) ?
            100.0 * ((sumH - sumL) / divisor) :
            0.0;
    }
}
