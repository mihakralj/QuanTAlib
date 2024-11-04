using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// UO: Ultimate Oscillator
/// A momentum oscillator that uses three different time periods to reduce volatility
/// and false signals. It incorporates a weighted average of three oscillator calculations
/// using different periods.
/// </summary>
/// <remarks>
/// The UO calculation process:
/// 1. Calculate buying pressure (BP): Close - Min(Low, Prior Close)
/// 2. Calculate true range (TR): Max(High, Prior Close) - Min(Low, Prior Close)
/// 3. Calculate average of BP/TR for each period
/// 4. Apply weights to each period's average
/// 5. Scale result to oscillator range
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Uses multiple timeframes to reduce false signals
/// - Weighted sum of three periods
/// - Traditional overbought level at 70
/// - Traditional oversold level at 30
///
/// Formula:
/// UO = 100 * ((4 * Average7) + (2 * Average14) + Average28) / (4 + 2 + 1)
/// where:
/// Average7 = 7-period average of BP/TR
/// Average14 = 14-period average of BP/TR
/// Average28 = 28-period average of BP/TR
///
/// Sources:
///     Larry Williams - "New Trading Dimensions" (1998)
///     https://www.investopedia.com/terms/u/ultimateoscillator.asp
///
/// Note: Default periods (7,14,28) and weights (4,2,1) were recommended by Williams
/// </remarks>
[SkipLocalsInit]
public sealed class Uo : AbstractBase
{
    private readonly CircularBuffer _bp1;
    private readonly CircularBuffer _tr1;
    private readonly CircularBuffer _bp2;
    private readonly CircularBuffer _tr2;
    private readonly CircularBuffer _bp3;
    private readonly CircularBuffer _tr3;
    private readonly double _weight1;
    private readonly double _weight2;
    private readonly double _weight3;
    private double _prevClose;
    private const int DefaultPeriod1 = 7;
    private const int DefaultPeriod2 = 14;
    private const int DefaultPeriod3 = 28;
    private const double DefaultWeight1 = 4.0;
    private const double DefaultWeight2 = 2.0;
    private const double DefaultWeight3 = 1.0;
    private const double ScalingFactor = 100.0;

    /// <param name="period1">The first period (default 7).</param>
    /// <param name="period2">The second period (default 14).</param>
    /// <param name="period3">The third period (default 28).</param>
    /// <param name="weight1">Weight for first period (default 4).</param>
    /// <param name="weight2">Weight for second period (default 2).</param>
    /// <param name="weight3">Weight for third period (default 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1 or any weight is less than or equal to 0.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uo(int period1 = DefaultPeriod1, int period2 = DefaultPeriod2, int period3 = DefaultPeriod3,
             double weight1 = DefaultWeight1, double weight2 = DefaultWeight2, double weight3 = DefaultWeight3)
    {
        if (period1 < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period1), "Period1 must be greater than 0");
        }
        if (period2 < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period2), "Period2 must be greater than 0");
        }
        if (period3 < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period3), "Period3 must be greater than 0");
        }
        if (weight1 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight1), "Weight1 must be greater than 0");
        }
        if (weight2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight2), "Weight2 must be greater than 0");
        }
        if (weight3 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight3), "Weight3 must be greater than 0");
        }

        _weight1 = weight1;
        _weight2 = weight2;
        _weight3 = weight3;

        _bp1 = new(period1);
        _tr1 = new(period1);
        _bp2 = new(period2);
        _tr2 = new(period2);
        _bp3 = new(period3);
        _tr3 = new(period3);

        WarmupPeriod = period3;
        Name = $"UO({period1},{period2},{period3})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period1">The first period.</param>
    /// <param name="period2">The second period.</param>
    /// <param name="period3">The third period.</param>
    /// <param name="weight1">Weight for first period.</param>
    /// <param name="weight2">Weight for second period.</param>
    /// <param name="weight3">Weight for third period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uo(object source, int period1 = DefaultPeriod1, int period2 = DefaultPeriod2, int period3 = DefaultPeriod3,
              double weight1 = DefaultWeight1, double weight2 = DefaultWeight2, double weight3 = DefaultWeight3)
        : this(period1, period2, period3, weight1, weight2, weight3)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            if (_index == 0)
                _prevClose = BarInput.Close;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateAverage(CircularBuffer bp, CircularBuffer tr)
    {
        double trSum = tr.Sum();
        return trSum >= double.Epsilon ? bp.Sum() / trSum : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate buying pressure and true range
        double minLowPrevClose = Math.Min(BarInput.Low, _prevClose);
        double maxHighPrevClose = Math.Max(BarInput.High, _prevClose);
        double bp = BarInput.Close - minLowPrevClose;
        double tr = maxHighPrevClose - minLowPrevClose;

        if (BarInput.IsNew)
        {
            // Add values to buffers
            _bp1.Add(bp);
            _tr1.Add(tr);
            _bp2.Add(bp);
            _tr2.Add(tr);
            _bp3.Add(bp);
            _tr3.Add(tr);
            _prevClose = BarInput.Close;
        }

        // Not enough data
        if (_index <= 1) return 0;

        // Calculate averages for each period
        double avg1 = CalculateAverage(_bp1, _tr1);
        double avg2 = CalculateAverage(_bp2, _tr2);
        double avg3 = CalculateAverage(_bp3, _tr3);

        // Calculate weighted sum
        double weightSum = _weight1 + _weight2 + _weight3;
        return ScalingFactor * ((_weight1 * avg1 + _weight2 * avg2 + _weight3 * avg3) / weightSum);
    }
}
