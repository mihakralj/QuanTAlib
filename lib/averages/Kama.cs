using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// KAMA: Kaufman's Adaptive Moving Average
/// An adaptive moving average that adjusts its smoothing based on market efficiency.
/// KAMA responds quickly during trending periods and becomes more stable during
/// sideways or choppy markets.
/// </summary>
/// <remarks>
/// The KAMA calculation process:
/// 1. Calculates the Efficiency Ratio (ER) to measure market noise
/// 2. Uses ER to determine the optimal smoothing between fast and slow constants
/// 3. Applies the adaptive smoothing to create the moving average
///
/// Key characteristics:
/// - Self-adaptive to market conditions
/// - Fast response during trends
/// - Stable during sideways markets
/// - Uses market efficiency for smoothing adjustment
/// - Reduces whipsaws in choppy markets
///
/// Sources:
///     Perry Kaufman - "Smarter Trading"
///     https://www.investopedia.com/terms/k/kaufmansadaptivemovingaverage.asp
/// </remarks>
public class Kama : AbstractBase
{
    private readonly int _period;
    private readonly double _scFast, _scSlow;
    private readonly double _scDiff;  // Precalculated (_scFast - _scSlow)
    private readonly CircularBuffer _buffer;
    private double _lastKama, _p_lastKama;

    /// <param name="period">The number of periods used to calculate the Efficiency Ratio.</param>
    /// <param name="fast">The number of periods for the fastest EMA response (default 2).</param>
    /// <param name="slow">The number of periods for the slowest EMA response (default 30).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Kama(int period, int fast = 2, int slow = 30)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _scFast = 2.0 / (((period < fast) ? period : fast) + 1);
        _scSlow = 2.0 / (slow + 1);
        _scDiff = _scFast - _scSlow;
        _buffer = new CircularBuffer(_period + 1);
        WarmupPeriod = period;
        Name = $"Kama({_period}, {fast}, {slow})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used to calculate the Efficiency Ratio.</param>
    /// <param name="fast">The number of periods for the fastest EMA response (default 2).</param>
    /// <param name="slow">The number of periods for the slowest EMA response (default 30).</param>
    public Kama(object source, int period, int fast = 2, int slow = 30) : this(period, fast, slow)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _lastKama = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastKama = _lastKama;
        }
        else
        {
            _lastKama = _p_lastKama;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateVolatility()
    {
        double volatility = 0;
        for (int i = 1; i < _buffer.Count; i++)
        {
            volatility += System.Math.Abs(_buffer[i] - _buffer[i - 1]);
        }
        return volatility;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEfficiencyRatio(double change, double volatility)
    {
        return volatility != 0 ? change / volatility : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSmoothingConstant(double er)
    {
        double sc = (er * _scDiff) + _scSlow;
        return sc * sc; // Square the smoothing constant
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        if (_index <= _period)
        {
            _lastKama = Input.Value;
            return Input.Value;
        }

        double change = System.Math.Abs(_buffer[^1] - _buffer[0]);
        double volatility = CalculateVolatility();
        double er = CalculateEfficiencyRatio(change, volatility);
        double sc = CalculateSmoothingConstant(er);

        _lastKama += sc * (Input.Value - _lastKama);
        IsHot = _index >= WarmupPeriod;

        return _lastKama;
    }
}
