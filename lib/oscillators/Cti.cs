using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CTI: Ehler's Correlation Trend Indicator
/// Measures the correlation between price and an ideal trend line.
/// </summary>
/// <remarks>
/// The CTI calculation process:
/// 1. Correlates price curve with an ideal trend line (negative count due to backwards data storage)
/// 2. Uses Spearman's correlation algorithm
/// 3. Returns values between -1 and 1
///
/// Key characteristics:
/// - Oscillates between -1 and 1
/// - Positive values indicate price follows uptrend
/// - Negative values indicate price follows downtrend
///
/// Formula:
/// CTI = (n∑xy - ∑x∑y) / sqrt((n∑x² - (∑x)²)(n∑y² - (∑y)²))
/// where:
/// x = price curve
/// y = -count (ideal trend line)
/// n = period length
///
/// Sources:
///     John Ehlers - "Cybernetic Analysis for Stocks and Futures" (2004)
///     John Ehlers, Correlation Trend Indicator, Stocks & Commodities May-2020
/// </remarks>
[SkipLocalsInit]
public sealed class Cti : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _priceBuffer;
    private readonly double[] _trendLine;
    private const int MinimumPoints = 2;  // Minimum points needed for correlation

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The calculation period (default: 20)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cti(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cti(int period = 20)
    {
        _period = period;
        _priceBuffer = new CircularBuffer(period);

        // Pre-calculate trend line values since they're static
        _trendLine = new double[period];
        for (int i = 0; i < period; i++)
        {
            _trendLine[i] = -i; // negative count for backwards data
        }

        WarmupPeriod = period;
        Name = "CTI";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _priceBuffer.Add(Input.Value, Input.IsNew);

        // Use available points for early calculations
        int points = Math.Min(_index + 1, _period);
        if (points < MinimumPoints) return 0;  // Need at least 2 points for correlation

        double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;

        // Calculate correlation components using available points
        for (int i = 0; i < points; i++)
        {
            double x = _priceBuffer[i];        // price curve
            double y = _trendLine[i];          // pre-calculated trend line

            sx += x;
            sy += y;
            sxx += x * x;
            sxy += x * y;
            syy += y * y;
        }

        // Check for numerical stability
        double denomX = points * sxx - sx * sx;
        double denomY = points * syy - sy * sy;

        if (denomX > 0 && denomY > 0)
        {
            return (points * sxy - sx * sy) / Math.Sqrt(denomX * denomY);
        }

        return 0;
    }
}
