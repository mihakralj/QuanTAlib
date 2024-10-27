using System;
namespace QuanTAlib;

/// <summary>
/// ATR: Average True Range
/// A technical indicator that measures market volatility by decomposing the entire
/// range of an asset's price for a period. ATR accounts for gaps between periods
/// and provides a comprehensive view of price volatility.
/// </summary>
/// <remarks>
/// The ATR calculation process:
/// 1. Calculates True Range (TR) as maximum of:
///    - Current High - Current Low
///    - |Current High - Previous Close|
///    - |Current Low - Previous Close|
/// 2. Applies RMA smoothing to TR values
/// 3. Updates with each new price bar
/// 4. Adapts to changing volatility
///
/// Key characteristics:
/// - Absolute price measure
/// - Gap-inclusive calculation
/// - Trend independent
/// - Volatility focused
/// - Smoothed output
///
/// Formula:
/// TR = max(high-low, |high-prevClose|, |low-prevClose|)
/// ATR = RMA(TR, period)
///
/// Market Applications:
/// - Position sizing
/// - Stop loss placement
/// - Volatility breakouts
/// - Risk assessment
/// - Entry/exit timing
///
/// Sources:
///     J. Welles Wilder - "New Concepts in Technical Trading Systems"
///     https://www.investopedia.com/terms/a/atr.asp
///
/// Note: Higher ATR indicates higher volatility
/// </remarks>

public class Atr : AbstractBase
{
    public double Tr { get; private set; }
    private readonly Rma _ma;
    private double _prevClose, _p_prevClose;

    /// <param name="period">The number of periods for ATR calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Atr(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 1.");
        }
        _ma = new(period, useSma: true);
        WarmupPeriod = _ma.WarmupPeriod;
        Name = $"ATR({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for ATR calculation.</param>
    public Atr(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _ma.Init();
        _prevClose = double.NaN;
        Tr = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        if (_index == 1)
        {
            // First bar uses simple high-low range
            Tr = BarInput.High - BarInput.Low;
            _prevClose = BarInput.Close;
        }
        else
        {
            // Calculate True Range as maximum of three measures
            Tr = Math.Max(
                BarInput.High - BarInput.Low,
                Math.Max(
                    Math.Abs(BarInput.High - _prevClose),
                    Math.Abs(BarInput.Low - _prevClose)
                )
            );
        }

        // Apply RMA smoothing to True Range
        _ma.Calc(new TValue(Input.Time, Tr, BarInput.IsNew));

        IsHot = _ma.IsHot;
        _prevClose = BarInput.Close;
        return _ma.Value;
    }
}
