using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RVI: Relative Volatility Index
/// A technical indicator developed by Donald Dorsey that measures the direction
/// of volatility by comparing upward and downward price movements. RVI helps
/// identify whether volatility is increasing more in up or down moves.
/// </summary>
/// <remarks>
/// The RVI calculation process:
/// 1. Separates price changes into up/down moves
/// 2. Calculates standard deviation for each
/// 3. Applies moving average smoothing
/// 4. Computes relative strength ratio
/// 5. Scales to percentage (0-100)
///
/// Key characteristics:
/// - Oscillator (0-100 range)
/// - Directional volatility measure
/// - Combines volatility and momentum
/// - Uses standard deviation
/// - Smoothed output
///
/// Formula:
/// RVI = 100 * SMA(StdDev(upMoves)) / (SMA(StdDev(upMoves)) + SMA(StdDev(downMoves)))
/// where:
/// upMove = max(close - prevClose, 0)
/// downMove = max(prevClose - close, 0)
///
/// Market Applications:
/// - Trend confirmation
/// - Divergence analysis
/// - Volatility breakouts
/// - Market reversals
/// - Overbought/oversold levels
///
/// Sources:
///     Donald Dorsey - "Technical Analysis of Stocks & Commodities" (1993)
///     https://www.investopedia.com/terms/r/relative_volatility_index.asp
///
/// Note: Similar concept to RSI but using volatility
/// </remarks>

[SkipLocalsInit]
public sealed class Rvi : AbstractBase
{
    private readonly Stddev _upStdDev, _downStdDev;
    private readonly Sma _upSma, _downSma;
    private double _previousClose;
    private const double ScalingFactor = 100.0;
    private const double Epsilon = 1e-10;

    /// <param name="period">The number of periods for RVI calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rvi(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        WarmupPeriod = period;
        Name = $"RVI(period={period})";
        _upStdDev = new Stddev(period);
        _downStdDev = new Stddev(period);
        _upSma = new(period);
        _downSma = new(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for RVI calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rvi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _previousClose = 0;
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
    private static (double upMove, double downMove) CalculateMoves(double change)
    {
        return (Math.Max(change, 0), Math.Max(-change, 0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateRvi(double upSma, double downSma)
    {
        double totalSma = upSma + downSma;
        return totalSma > Epsilon ? ScalingFactor * upSma / totalSma : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double close = Input.Value;
        double change = close - _previousClose;

        // Separate into up and down moves
        var (upMove, downMove) = CalculateMoves(change);

        // Calculate standard deviations and apply smoothing
        _upSma.Calc(_upStdDev.Calc(new TValue(Input.Time, upMove, Input.IsNew)));
        _downSma.Calc(_downStdDev.Calc(new TValue(Input.Time, downMove, Input.IsNew)));

        // Calculate RVI ratio
        double rvi = CalculateRvi(_upSma.Value, _downSma.Value);

        _previousClose = close;
        IsHot = _index >= WarmupPeriod;
        return rvi;
    }
}
