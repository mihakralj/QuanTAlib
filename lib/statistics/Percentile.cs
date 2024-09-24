namespace QuanTAlib;

using System;
using System.Linq;

public class Percentile : AbstractBase
{
    public readonly int Period;
    public readonly double Percent;
    private CircularBuffer _buffer;

    public Percentile(int period, double percent) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for percentile calculation.");
        }
        if (percent < 0 || percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 100.");
        }
        Period = period;
        Percent = percent;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"Percentile(period={period}, percent={percent})";
        Init();
    }

    public Percentile(object source, int period, double percent) : this(period, percent)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double result;
        if (_buffer.Count >= Period)
        {
            var values = _buffer.GetSpan().ToArray();
            Array.Sort(values);

            double position = (Percent / 100.0) * (values.Length - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                result = values[lowerIndex];
            }
            else
            {
                // Interpolate between the two nearest values
                double lowerValue = values[lowerIndex];
                double upperValue = values[upperIndex];
                double fraction = position - lowerIndex;
                result = lowerValue + (upperValue - lowerValue) * fraction;
            }
        }
        else
        {
            // Use average for insufficient data, like the Median class
            result = _buffer.Average();
        }

        IsHot = _buffer.Count >= Period;
        return result;
    }
}