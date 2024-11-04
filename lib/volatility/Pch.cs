using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PCH: Price Channel
/// A volatility indicator that identifies the highest high and lowest low
/// over a specified period, creating a channel that contains price movement.
/// </summary>
/// <remarks>
/// The PCH calculation process:
/// 1. Track highest high over period
/// 2. Track lowest low over period
/// 3. Calculate midline as average of high and low
/// 4. Updates with each new price bar
///
/// Key characteristics:
/// - Trend following indicator
/// - Support/resistance identification
/// - Breakout detection
/// - Volatility measurement
/// - Range-based analysis
///
/// Formula:
/// Upper = Highest High over period
/// Lower = Lowest Low over period
/// Middle = (Upper + Lower) / 2
///
/// Market Applications:
/// - Trend identification
/// - Support/resistance levels
/// - Breakout trading
/// - Volatility analysis
/// - Range-bound trading
///
/// Note: Also known as Donchian Channels
/// </remarks>
[SkipLocalsInit]
public sealed class Pch : AbstractBase
{
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private const int DefaultPeriod = 20;

    /// <param name="period">The number of periods for PCH calculation (default 20).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pch(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _highs = new(period);
        _lows = new(period);
        WarmupPeriod = period;
        Name = $"PCH({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for PCH calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pch(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _highs.Add(BarInput.High);
            _lows.Add(BarInput.Low);
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate channel boundaries
        double upper = _highs.Max();
        double lower = _lows.Min();

        // Return midline
        return (upper + lower) / 2.0;
    }

    /// <summary>
    /// Gets the upper channel value (highest high)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Upper() => _highs.Max();

    /// <summary>
    /// Gets the lower channel value (lowest low)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Lower() => _lows.Min();
}
