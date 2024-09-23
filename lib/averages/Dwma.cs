namespace QuanTAlib;

/// <summary>
/// DWMA: Double Weighted Moving Average
/// DWMA is a technical indicator that applies a Weighted Moving Average (WMA) twice to the input data.
/// The weights are decreasing over the period with p^2 decay, and the most recent data has the heaviest weight.
/// </summary>
/// <remarks>
/// Smoothness:     ★★★★★ (5/5)
/// Sensitivity:    ★★★☆☆ (3/5)
/// Overshooting:   ★★★★☆ (4/5)
/// Lag:            ★★☆☆☆ (2/5)
///
/// The DWMA is calculated by applying two WMAs in sequence:
/// 1. An inner WMA is applied to the input data.
/// 2. An outer WMA is then applied to the result of the inner WMA.
///
/// Key characteristics:
/// - The weight distribution follows a p^2 decay, where p is the position of the data point.
/// - More recent data points receive higher weights, emphasizing recent price movements.
/// - The double application of WMA results in a smoother indicator compared to a single WMA.
///
/// The formula for DWMA can be expressed as:
/// DWMA = WMA(WMA(price, period), period)
///
/// Where WMA is the Weighted Moving Average function and 'period' is the number of data points used in each WMA calculation.
/// </remarks>

public class Dwma : AbstractBase
{
    private readonly int _period;
    private readonly Wma _innerWma;
    private readonly Wma _outerWma;

    public Dwma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _innerWma = new Wma(period);
        _outerWma = new Wma(period);
        Name = "Wma";
        WarmupPeriod = 2 * _period - 1;
        Init();
    }

    public Dwma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _innerWma.Init();
        _outerWma.Init();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate inner WMA
        TValue innerResult = _innerWma.Calc(Input);

        // Calculate outer WMA using the result of inner WMA
        TValue outerResult = _outerWma.Calc(innerResult);

        double result = outerResult.Value;
        IsHot = _index >= WarmupPeriod;

        return result;
    }
}
