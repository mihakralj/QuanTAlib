namespace QuanTAlib;

// Excess kurtosis calculated with Sheskin Algorithm
public class Kurtosis : AbstractBase
{
    public readonly int Period;
    private readonly CircularBuffer _buffer;

    public Kurtosis(int period)
    {
        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 4 for kurtosis calculation.");
        }
        Period = period;
        WarmupPeriod = Period - 1;
        _buffer = new CircularBuffer(period);
        Name = $"Kurtosis(period={period})";
        Init();
    }

    public Kurtosis(object source, int period) : this(period)
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

        double kurtosis = 0;
        if (_buffer.Count > 3)
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double s2 = 0;
            double s4 = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                s2 += diff * diff;
                s4 += diff * diff * diff * diff;
            }

            double variance = s2 / (n - 1);

            // Using the Sheskin Algorithm for kurtosis
            kurtosis = (n * (n + 1) * s4) / (variance * variance * (n - 3) * (n - 1) * (n - 2))
                       - (3 * (n - 1) * (n - 1) / ((n - 2) * (n - 3)));
        }

        IsHot = _buffer.Count >= Period;
        return kurtosis;
    }
}
