using System;
namespace QuanTAlib;

/// <summary>
/// SMMA: Smoothed Moving Average
/// A modified moving average that gives more weight to recent prices while maintaining
/// a smooth output. It uses the previous SMMA value in its calculation, creating
/// a smoother line than traditional moving averages.
/// </summary>
/// <remarks>
/// The SMMA calculation process:
/// 1. Uses SMA for initial value (first period points)
/// 2. For subsequent points, calculates: (prevSMMA * (period-1) + price) / period
/// 3. This creates a smoothed effect with reduced volatility
///
/// Key characteristics:
/// - Smoother than traditional moving averages
/// - Reduced volatility in output
/// - Takes into account all previous prices
/// - Good for identifying overall trends
/// - Less lag than SMA but more than EMA
///
/// Implementation:
///     Based on smoothed moving average principles with
///     initial SMA seeding for stability
/// </remarks>

public class Smma : AbstractBase
{
    private readonly int _period;
    private CircularBuffer? _buffer;
    private double _lastSmma, _p_lastSmma;

    /// <param name="period">The number of data points used in the SMMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Smma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        WarmupPeriod = period;
        Name = $"Smma({_period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the SMMA calculation.</param>
    public Smma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer = new CircularBuffer(_period);
        _lastSmma = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _p_lastSmma = _lastSmma;
            _index++;
        }
        else
        {
            _lastSmma = _p_lastSmma;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer!.Add(Input.Value, Input.IsNew);

        double smma;

        if (_index <= _period)
        {
            smma = _buffer.Average();

            if (_index == _period)
            {
                _lastSmma = smma; // Initialize _lastSmma for the transition
            }
        }
        else
        {
            smma = ((_lastSmma * (_period - 1)) + Input.Value) / _period;
        }

        _lastSmma = smma;
        IsHot = _index >= WarmupPeriod;

        return smma;
    }
}
