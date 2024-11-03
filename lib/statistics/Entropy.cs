using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
[SkipLocalsInit]
public sealed class Entropy : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private readonly Dictionary<double, int> _valueCounts;
    private const double Epsilon = 1e-10;
    private const double DefaultEntropy = 1.0;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for entropy calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entropy(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for entropy calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints; // Minimum number of points needed for entropy calculation
        _buffer = new CircularBuffer(period);
        _valueCounts = new Dictionary<double, int>();
        Name = $"Entropy(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for entropy calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entropy(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _valueCounts.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void CountValues(ReadOnlySpan<double> values, Dictionary<double, int> counts)
    {
        counts.Clear();
        for (int i = 0; i < values.Length; i++)
        {
            counts[values[i]] = counts.TryGetValue(values[i], out int count) ? count + 1 : 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateShannonsEntropy(Dictionary<double, int> counts, int totalCount)
    {
        double entropy = 0;
        foreach (var count in counts.Values)
        {
            double probability = (double)count / totalCount;
            entropy -= probability * Math.Log2(probability);
        }
        return entropy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        if (_index <= 1)  // Need at least two data points for entropy calculation
        {
            return DefaultEntropy;
        }

        ReadOnlySpan<double> values = _buffer.GetSpan();
        CountValues(values, _valueCounts);

        // Calculate Shannon's entropy
        double entropy = CalculateShannonsEntropy(_valueCounts, values.Length);

        // Normalize by maximum possible entropy for current unique values
        double maxEntropy = Math.Log2(_valueCounts.Count);
        entropy = maxEntropy < Epsilon ? DefaultEntropy : entropy / maxEntropy;

        IsHot = _buffer.Count >= Period;
        return entropy;
    }
}
