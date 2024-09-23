namespace QuanTAlib;

using System;
using System.Linq;

// Shannon's Entropy calculation
public class Entropy : AbstractBase
{
    public readonly int Period;
    private CircularBuffer _buffer;

    public Entropy(int period) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for entropy calculation.");
        }
        Period = period;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"Entropy(period={period})";
        Init();
    }

    public Entropy(object source, int period) : this(period)
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

        double entropy = 0;
        if (_index > 1)  // We need at least two data points for entropy calculation
        {
            var values = _buffer.GetSpan().ToArray();
            int n = values.Length;

            // Calculate probabilities
            var groupedValues = values.GroupBy(x => x).Select(g => new { Value = g.Key, Count = g.Count() });

            // Use the actual count of values for probability calculation
            foreach (var group in groupedValues)
            {
                double probability = (double)group.Count / n;
                entropy -= probability * Math.Log2(probability);
            }

            // Normalize the entropy based on the current number of unique values
            int uniqueValueCount = groupedValues.Count();
            double maxEntropy = Math.Log2(uniqueValueCount);

            entropy = entropy == 0 ? 1 : entropy / maxEntropy;

        }
        else { entropy = 1; }

        IsHot = _buffer.Count >= Period;
        return entropy;
    }
}