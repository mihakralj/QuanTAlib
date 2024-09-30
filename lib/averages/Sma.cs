namespace QuanTAlib;

public class Sma : AbstractBase
{
    // inherited _index
    // inherited _value
    private readonly CircularBuffer _buffer;

    public Sma(int period) : base()
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        Name = "Sma";
        WarmupPeriod = period;
        Init();
    }

    public Sma(object source, int period) : this(period: period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }
    //inhereted public void Sub(object source, in ValueEventArgs args)

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Core SMA calculation - using _buffer.Average
    /// </summary>

    protected override double Calculation()
    {
        double result;
        ManageState(IsNew);
        _buffer.Add(Input.Value, Input.IsNew);
        result = _buffer.Average();

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}