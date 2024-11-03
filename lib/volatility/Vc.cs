using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VC: Volatility Cone
/// A technical indicator that analyzes volatility across different time periods
/// to identify normal ranges and extreme values.
/// </summary>
/// <remarks>
/// The VC calculation process:
/// 1. Calculate volatility for the specified period
/// 2. Track mean and standard deviation of volatility
/// 3. Calculate upper and lower bounds:
///    Upper = Mean + (deviations * StdDev)
///    Lower = Mean - (deviations * StdDev)
///
/// Key characteristics:
/// - Multi-period volatility analysis
/// - Statistical approach
/// - Default period is 20 days
/// - Returns mean and bounds
/// - Adaptive to market conditions
///
/// Formula:
/// Volatility = StdDev(Returns) * sqrt(252)  // Annualized
/// Upper = Mean(Volatility) + (deviations * StdDev(Volatility))
/// Lower = Mean(Volatility) - (deviations * StdDev(Volatility))
///
/// Market Applications:
/// - Options trading
/// - Risk assessment
/// - Volatility forecasting
/// - Trading strategy development
/// - Market regime analysis
///
/// Sources:
///     https://www.investopedia.com/terms/v/volatility-cone.asp
///
/// Note: Returns three values: mean volatility and its upper/lower bounds
/// </remarks>
[SkipLocalsInit]
public sealed class Vc : AbstractBase
{
    private readonly int _period;
    private readonly double _deviations;
    private readonly CircularBuffer _returns;
    private readonly CircularBuffer _volatilities;
    private double _prevClose;
    private double _upperBound;
    private double _lowerBound;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vc(int period = 20, double deviations = 2.0)
    {
        _period = period;
        _deviations = deviations;
        WarmupPeriod = period * 2;  // Need enough data for stable statistics
        Name = $"VC({_period},{_deviations})";
        _returns = new CircularBuffer(period);
        _volatilities = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vc(object source, int period = 20, double deviations = 2.0) : this(period, deviations)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _upperBound = 0;
        _lowerBound = 0;
        _returns.Clear();
        _volatilities.Clear();
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
    private double CalculateVariance(CircularBuffer buffer)
    {
        if (buffer.Count == 0) return 0;
        double mean = buffer.Average();
        double sumSquaredDiff = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            double diff = buffer[i] - mean;
            sumSquaredDiff += diff * diff;
        }
        return sumSquaredDiff / buffer.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate return
        double ret = Math.Abs(_prevClose) > double.Epsilon ? Math.Log(BarInput.Close / _prevClose) : 0;
        _returns.Add(ret);

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Need enough returns for volatility calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate current volatility (annualized)
        double vol = Math.Sqrt(CalculateVariance(_returns)) * Math.Sqrt(252);
        _volatilities.Add(vol);

        // Need enough volatilities for cone calculation
        if (_index <= WarmupPeriod)
        {
            return vol;
        }

        // Calculate mean and standard deviation of volatilities
        double meanVol = _volatilities.Average();
        double stdVol = Math.Sqrt(CalculateVariance(_volatilities));

        // Calculate bounds
        _upperBound = meanVol + (_deviations * stdVol);
        _lowerBound = Math.Max(0, meanVol - (_deviations * stdVol));

        IsHot = _index >= WarmupPeriod;
        return meanVol;
    }

    /// <summary>
    /// Gets the upper bound of the volatility cone
    /// </summary>
    public double UpperBound => _upperBound;

    /// <summary>
    /// Gets the lower bound of the volatility cone
    /// </summary>
    public double LowerBound => _lowerBound;
}
