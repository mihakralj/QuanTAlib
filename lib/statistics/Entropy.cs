using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// Entropy: Information Content Measure
/// A statistical measure that quantifies the unpredictability or randomness in
/// a time series using Shannon's Entropy. Higher entropy indicates more randomness
/// and uncertainty in the data.
/// </summary>
/// <remarks>
/// The Entropy calculation process:
/// 1. Groups values to calculate probabilities
/// 2. Applies Shannon's entropy formula
/// 3. Normalizes result to 0-1 range
/// 4. Adjusts for number of unique values
///
/// Key characteristics:
/// - Range from 0 (predictable) to 1 (random)
/// - Measures information content
/// - Detects regime changes
/// - Identifies market uncertainty
/// - Scale-independent measure
///
/// Formula:
/// H = -Σ(p(x) * log₂(p(x))) / log₂(n)
/// where:
/// p(x) = probability of value x
/// n = number of unique values
///
/// Applications:
/// - Detect market regime changes
/// - Assess price movement predictability
/// - Identify periods of high uncertainty
/// - Measure information flow in markets
///
/// Sources:
///     Claude Shannon - "A Mathematical Theory of Communication" (1948)
///     https://en.wikipedia.org/wiki/Entropy_(information_theory)
///
/// Note: Normalized to [0,1] for easier interpretation
/// </remarks>

public class Entropy : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for entropy calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Entropy(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for entropy calculation.");
        }
        Period = period;
        WarmupPeriod = 2; // Minimum number of points needed for entropy calculation
        _buffer = new CircularBuffer(period);
        Name = $"Entropy(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for entropy calculation.</param>
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
        if (_index > 1)  // Need at least two data points for entropy calculation
        {
            var values = _buffer.GetSpan().ToArray();
            int n = values.Length;

            // Calculate probabilities for each unique value
            var groupedValues = values.GroupBy(x => x).Select(g => new { Value = g.Key, Count = g.Count() });

            // Calculate Shannon's entropy
            foreach (var group in groupedValues)
            {
                double probability = (double)group.Count / n;
                entropy -= probability * Math.Log2(probability);
            }

            // Normalize by maximum possible entropy for current unique values
            int uniqueValueCount = groupedValues.Count();
            double maxEntropy = Math.Log2(uniqueValueCount);

            entropy = entropy == 0 ? 1 : entropy / maxEntropy;
        }
        else
        {
            entropy = 1; // Maximum entropy when insufficient data
        }

        IsHot = _buffer.Count >= Period;
        return entropy;
    }
}
