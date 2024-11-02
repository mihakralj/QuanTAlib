using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// AP: Andrew's Pitchfork
/// A trend channel tool that uses three points to create a channel with a median
/// line and two parallel lines. It helps identify potential support and resistance
/// levels based on market pivots.
/// </summary>
/// <remarks>
/// The AP calculation process:
/// 1. Use three pivot points (P0, P1, P2)
/// 2. Calculate median line from P0 to midpoint of P1-P2
/// 3. Draw parallel lines at P1 and P2
/// 4. Project all lines forward
///
/// Key characteristics:
/// - Trend channel tool
/// - Support/resistance levels
/// - Price projection
/// - Market geometry
/// - Pivot-based analysis
///
/// Formula:
/// Median Line = Line from P0 to (P1 + P2)/2
/// Upper Line = Parallel to median at P1
/// Lower Line = Parallel to median at P2
///
/// Market Applications:
/// - Trend analysis
/// - Support/resistance
/// - Price targets
/// - Channel trading
/// - Market structure
///
/// Sources:
///     Dr. Alan Andrews
///     https://www.investopedia.com/terms/a/andrewspitchfork.asp
///
/// Note: Returns median line value for current price level
/// </remarks>

[SkipLocalsInit]
public sealed class Ap : AbstractBase
{
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private readonly CircularBuffer _closes;
    private const int DefaultPeriod = 20;

    /// <param name="period">The lookback period for pivot points (default 20).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 3.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ap(int period = DefaultPeriod)
    {
        if (period < 3)
            throw new ArgumentOutOfRangeException(nameof(period));

        _highs = new(period);
        _lows = new(period);
        _closes = new(period);
        WarmupPeriod = period;
        Name = $"AP({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period for pivot points.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ap(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double x, double y) FindPivot(CircularBuffer highs, CircularBuffer lows, CircularBuffer closes, int offset)
    {
        double high = highs[offset];
        double low = lows[offset];
        double close = closes[offset];
        return (offset, (high + low + close) / 3.0); // Simple pivot point calculation
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Store price data
        _highs.Add(BarInput.High, BarInput.IsNew);
        _lows.Add(BarInput.Low, BarInput.IsNew);
        _closes.Add(BarInput.Close, BarInput.IsNew);

        if (_index < WarmupPeriod)
            return BarInput.Close;

        // Find three pivot points
        var p0 = FindPivot(_highs, _lows, _closes, 2);
        var p1 = FindPivot(_highs, _lows, _closes, 1);
        var p2 = FindPivot(_highs, _lows, _closes, 0);

        // Calculate midpoint of P1-P2
        double midX = (p1.x + p2.x) / 2.0;
        double midY = (p1.y + p2.y) / 2.0;

        // Calculate slope of median line
        double slope = (midY - p0.y) / (midX - p0.x);

        // Project median line to current bar
        double currentX = _index - p0.x;
        return p0.y + slope * currentX;
    }
}
