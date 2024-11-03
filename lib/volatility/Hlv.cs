using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// HLV: High-Low Volatility
/// A volatility measure based on the high-low range relative
/// to the previous close, capturing intraday price movements.
/// </summary>
/// <remarks>
/// The HLV calculation process:
/// 1. Calculate normalized high-low range
/// 2. Take rolling average over period
/// 3. Convert to annualized volatility
///
/// Key characteristics:
/// - Captures intraday price movements
/// - Uses high, low, and previous close
/// - Default period is 20 days
/// - Annualized by default
/// - Expressed as a percentage
///
/// Formula:
/// Range = (High - Low) / PrevClose
/// HLV = sqrt(sum(Range² / period) * 252) * 100
///
/// Market Applications:
/// - Volatility measurement
/// - Risk assessment
/// - Trading range analysis
/// - Market regime identification
/// - Position sizing
///
/// Sources:
///     Parkinson (1980) modified
///     The Extreme Value Method for Estimating the Variance of the Rate of Return
///     Journal of Business 53(1): 61-65
///
/// Note: Returns annualized volatility as a percentage
/// </remarks>
[SkipLocalsInit]
public sealed class Hlv : AbstractBase
{
    private readonly int _period;
    private readonly bool _annualize;
    private readonly CircularBuffer _ranges;
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hlv(int period = 20, bool annualize = true)
    {
        _period = period;
        _annualize = annualize;
        WarmupPeriod = period + 1;  // Need one extra period for previous close
        Name = $"HLV({_period})";
        _ranges = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hlv(object source, int period = 20, bool annualize = true) : this(period, annualize)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _ranges.Clear();
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

        // Calculate normalized range
        double range = (BarInput.High - BarInput.Low) / _prevClose;
        double squaredRange = range * range;
        _ranges.Add(squaredRange);

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate average squared range
        double avgSquaredRange = _ranges.Average();

        // Calculate volatility
        double volatility = Math.Sqrt(avgSquaredRange);

        // Annualize if requested
        if (_annualize)
        {
            volatility *= Math.Sqrt(252);
        }

        // Convert to percentage
        volatility *= 100;

        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
