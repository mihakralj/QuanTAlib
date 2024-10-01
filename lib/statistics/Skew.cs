namespace QuanTAlib;

using System;
using System.Linq;

public class Skew : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    public Skew(int period) : base()
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 3 for skewness calculation.");
        }
        Period = period;
        WarmupPeriod = 3;
        _buffer = new CircularBuffer(period);
        Name = $"Skew(period={period})";
        Init();
    }

    public Skew(object source, int period) : this(period)
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

        double skew = 0;
        if (_buffer.Count >= 3)  // We need at least 3 data points for skewness
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double sumCubedDeviations = 0;
            double sumSquaredDeviations = 0;

            foreach (var value in values)
            {
                double deviation = value - mean;
                sumCubedDeviations += Math.Pow(deviation, 3);
                sumSquaredDeviations += Math.Pow(deviation, 2);
            }

            // Calculate sample skewness using the adjusted Fisher-Pearson standardized moment coefficient
            double m3 = sumCubedDeviations / n;
            double m2 = sumSquaredDeviations / n;
            double s3 = Math.Pow(m2, 1.5);

            if (s3 != 0)  // Avoid division by zero
            {
                skew = (Math.Sqrt(n * (n - 1)) / (n - 2)) * (m3 / s3);
            }
        }

        IsHot = _buffer.Count >= Period;
        return skew;
    }
}