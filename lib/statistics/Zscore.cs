namespace QuanTAlib;

using System;
using System.Linq;

public class Zscore : AbstractBase
{
    public readonly int Period;
    private CircularBuffer _buffer;

    public Zscore(int period) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for Z-score calculation.");
        }
        Period = period;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"ZScore(period={period})";
        Init();
    }

    public Zscore(object source, int period) : this(period)
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

        double zScore = 0;
        if (_buffer.Count >= 2)  // We need at least 2 data points for Z-score
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double sumSquaredDeviations = values.Sum(x => Math.Pow(x - mean, 2));
            double standardDeviation = Math.Sqrt(sumSquaredDeviations / (n - 1));  // Sample standard deviation

            if (standardDeviation != 0)  // Avoid division by zero
            {
                zScore = (Input.Value - mean) / standardDeviation;
            }
        }

        IsHot = _buffer.Count >= Period;
        return zScore;
    }
}