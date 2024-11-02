using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// AROON: Aroon Oscillator
/// A trend-following indicator that measures the strength of a trend and the likelihood
/// that the trend will continue. It consists of two lines (Aroon Up and Aroon Down) and
/// their difference forms the Aroon Oscillator.
/// </summary>
/// <remarks>
/// The Aroon calculation process:
/// 1. Tracks the number of periods since the last highest high (Aroon Up)
/// 2. Tracks the number of periods since the last lowest low (Aroon Down)
/// 3. Normalizes both values to a 0-100 scale
/// 4. Calculates the difference (Aroon Oscillator)
///
/// Key characteristics:
/// - Oscillates between -100 and +100
/// - Positive values indicate uptrend
/// - Negative values indicate downtrend
/// - Zero line crossovers signal trend changes
/// - Extreme readings suggest strong trends
///
/// Formula:
/// Aroon Up = ((period - days since highest high) / period) × 100
/// Aroon Down = ((period - days since lowest low) / period) × 100
/// Aroon Oscillator = Aroon Up - Aroon Down
///
/// Sources:
///     Tushar Chande - "The New Technical Trader" (1994)
///     https://www.investopedia.com/terms/a/aroonoscillator.asp
///
/// Note: Default period of 25 was recommended by Chande
/// </remarks>

[SkipLocalsInit]
public sealed class Aroon : AbstractBarBase
{
    private readonly CircularBuffer _highPrices;
    private readonly CircularBuffer _lowPrices;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 25;

    /// <param name="period">The number of periods used in the Aroon calculation (default 25).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aroon(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _highPrices = new(period);
        _lowPrices = new(period);
        _index = 0;
        WarmupPeriod = period;
        Name = $"AROON({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the Aroon calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aroon(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _highPrices.Add(Input.High);
            _lowPrices.Add(Input.Low);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateAroonLine(int period, int daysSince)
    {
        return ((period - daysSince) * ScalingFactor) / period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index < WarmupPeriod)
            return double.NaN;

        // Find highest high and lowest low positions
        int highestIndex = 0;
        int lowestIndex = 0;
        double highestHigh = _highPrices[0];
        double lowestLow = _lowPrices[0];

        for (int i = 1; i < _highPrices.Count; i++)
        {
            if (_highPrices[i] > highestHigh)
            {
                highestHigh = _highPrices[i];
                highestIndex = i;
            }
            if (_lowPrices[i] < lowestLow)
            {
                lowestLow = _lowPrices[i];
                lowestIndex = i;
            }
        }

        // Calculate Aroon Up and Down
        double aroonUp = CalculateAroonLine(_highPrices.Count, highestIndex);
        double aroonDown = CalculateAroonLine(_lowPrices.Count, lowestIndex);

        // Return Aroon Oscillator
        return aroonUp - aroonDown;
    }
}
