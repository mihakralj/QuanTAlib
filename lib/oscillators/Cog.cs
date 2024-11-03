using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// COG: Ehler's Center of Gravity Oscillator
/// A momentum oscillator that uses the concept of center of gravity from physics
/// to measure price momentum. It calculates a weighted sum where more recent
/// prices have higher weights.
/// </summary>
/// <remarks>
/// The COG calculation process:
/// 1. Calculate weighted sum of prices (numerator)
/// 2. Calculate sum of weights (denominator)
/// 3. Divide to get center of gravity
/// 4. Invert and normalize result
///
/// Key characteristics:
/// - Oscillates around zero
/// - Leading indicator (less lag than traditional momentum)
/// - Positive values indicate upward momentum
/// - Negative values indicate downward momentum
/// - Zero line crossovers signal trend changes
///
/// Formula:
/// COG = -((Σ(Price(i) * i)) / (Σ(Price(i))) - (period + 1)/2)
/// where:
/// i = position in period (1 to period)
/// Price(i) = price at position i
///
/// Sources:
///     John F. Ehlers - "Cybernetic Analysis for Stocks and Futures"
///     https://www.mesasoftware.com/papers/CenterOfGravity.pdf
///
/// Note: Default period is 10
/// </remarks>
[SkipLocalsInit]
public sealed class Cog : AbstractBase
{
    private readonly CircularBuffer _prices;
    private readonly int _period;
    private const int DefaultPeriod = 10;

    /// <param name="period">The number of periods used in the COG calculation (default 10).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cog(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _period = period;
        _prices = new(period);
        WarmupPeriod = period;
        Name = $"COG({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the COG calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cog(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Add new price to buffer
        _prices.Add(Input.Value, Input.IsNew);

        double numerator = 0.0;
        double denominator = 0.0;

        // Calculate weighted sums
        for (int i = 0; i < _prices.Count; i++)
        {
            double price = _prices[i];
            double weight = i + 1;
            numerator += price * weight;
            denominator += price;
        }

        // Avoid division by zero
        if (Math.Abs(denominator) < double.Epsilon)
            return 0.0;

        // Calculate center of gravity and normalize
        return -((numerator / denominator) - ((_period + 1.0) / 2.0));
    }
}
