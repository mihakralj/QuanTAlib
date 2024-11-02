using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// UI: Ulcer Index
/// A technical indicator that measures downside risk by incorporating both
/// the depth and duration of price declines over a given period.
/// </summary>
/// <remarks>
/// The UI calculation process:
/// 1. Calculate percentage drawdown from recent high for each period
/// 2. Square the drawdowns to emphasize larger declines
/// 3. Calculate the average of squared drawdowns
/// 4. Take the square root of the average
///
/// Key characteristics:
/// - Measures downside volatility
/// - Emphasizes larger drawdowns
/// - Default period is 14 days
/// - Always positive
/// - No upper bound
///
/// Formula:
/// Drawdown = ((Close - 14-period High) / 14-period High) * 100
/// UI = sqrt(sum(Drawdown^2) / period)
///
/// Market Applications:
/// - Risk assessment
/// - Portfolio analysis
/// - Trading system evaluation
/// - Market timing
/// - Trend strength measurement
///
/// Sources:
///     Peter Martin - Original development (1987)
///     https://www.investopedia.com/terms/u/ulcerindex.asp
///
/// Note: Higher values indicate higher risk due to deeper or more frequent drawdowns
/// </remarks>

[SkipLocalsInit]
public sealed class Ui : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _prices;
    private readonly CircularBuffer _drawdowns;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ui(int period = 14)
    {
        _period = period;
        WarmupPeriod = period;
        Name = $"UI({_period})";
        _prices = new CircularBuffer(period);
        _drawdowns = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ui(object source, int period = 14) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prices.Clear();
        _drawdowns.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Add current price to buffer
        _prices.Add(BarInput.Close);

        // Need enough prices for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate maximum price in period
        double maxPrice = _prices.Max();

        // Calculate percentage drawdown
        double drawdown = Math.Abs(maxPrice) > double.Epsilon ? ((BarInput.Close - maxPrice) / maxPrice) * 100 : 0;

        // Add squared drawdown to buffer
        _drawdowns.Add(drawdown * drawdown);

        // Calculate Ulcer Index
        double ui = Math.Sqrt(_drawdowns.Average());

        IsHot = _index >= WarmupPeriod;
        return ui;
    }
}
