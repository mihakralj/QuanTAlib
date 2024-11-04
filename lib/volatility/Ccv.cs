using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CCV: Close-to-Close Volatility
/// A measure of price volatility that uses only closing prices,
/// calculated as the standard deviation of logarithmic returns.
/// </summary>
/// <remarks>
/// The CCV calculation process:
/// 1. Calculate logarithmic returns: ln(Close[t]/Close[t-1])
/// 2. Calculate standard deviation of returns over the period
/// 3. Annualize by multiplying by sqrt(trading days per year)
///
/// Key characteristics:
/// - Uses only closing prices
/// - Based on logarithmic returns
/// - Default period is 20 days
/// - Annualized by default (multiply by sqrt(252))
/// - Expressed as a percentage
///
/// Formula:
/// Returns = ln(Close[t]/Close[t-1])
/// CCV = StdDev(Returns, period) * sqrt(252) * 100
///
/// Market Applications:
/// - Volatility measurement
/// - Risk assessment
/// - Option pricing
/// - Trading strategy development
/// - Portfolio management
///
/// Sources:
///     Close-to-Close Volatility concept
///     https://www.investopedia.com/terms/v/volatility.asp
///
/// Note: Returns annualized volatility as a percentage
/// </remarks>
[SkipLocalsInit]
public sealed class Ccv : AbstractBase
{
    private readonly int _period;
    private readonly bool _annualize;
    private readonly CircularBuffer _returns;
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ccv(int period = 20, bool annualize = true)
    {
        _period = period;
        _annualize = annualize;
        WarmupPeriod = period + 1;  // Need one extra period for returns calculation
        Name = $"CCV({_period})";
        _returns = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ccv(object source, int period = 20, bool annualize = true) : this(period, annualize)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _returns.Clear();
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

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate logarithmic return
        double logReturn = Math.Log(BarInput.Close / _prevClose);
        _returns.Add(logReturn);
        _prevClose = BarInput.Close;

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate standard deviation
        double mean = _returns.Average();
        double sumSquaredDeviations = 0;
        for (int i = 0; i < _period; i++)
        {
            double deviation = _returns[i] - mean;
            sumSquaredDeviations += deviation * deviation;
        }
        double stdDev = Math.Sqrt(sumSquaredDeviations / _period);

        // Annualize if requested (sqrt(252) for trading days in a year)
        if (_annualize)
        {
            stdDev *= Math.Sqrt(252);
        }

        // Convert to percentage
        double volatility = stdDev * 100;

        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
