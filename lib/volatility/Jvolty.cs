/// <summary>
/// Represents a Jurik Volatility (Jvolty) calculator, a measure of market volatility based on Jurik Moving Average (JMA) concepts.
/// </summary>

namespace QuanTAlib;

public class Jvolty : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _values;
    private readonly CircularBuffer _voltyShort;
    private readonly CircularBuffer _vsumBuff;
    private readonly CircularBuffer _avoltyBuff;

    private double _len1;
    private double _pow1;
    private double _upperBand;
    private double _lowerBand;
    private double _p_upperBand;
    private double _p_lowerBand;

    /// <summary>
    /// Initializes a new instance of the Jvolty class with the specified parameters.
    /// </summary>
    /// <param name="period">The period over which to calculate the Jvolty.</param>
    /// <param name="phase">The phase parameter for the JMA-style calculation.</param>
    /// <param name="vshort">The short-term volatility period.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
    public Jvolty(int period, int vshort = 10)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        int _vlong = 65;

        _values = new CircularBuffer(period);
        _voltyShort = new CircularBuffer(vshort);
        _vsumBuff = new CircularBuffer(_vlong);
        _avoltyBuff = new CircularBuffer(2);

        WarmupPeriod = period * 2;
        Name = $"JVOLTY({period},{vshort})";
    }

    /// <summary>
    /// Initializes a new instance of the Jvolty class with the specified source and parameters.
    /// </summary>
    /// <param name="source">The source object to subscribe to for bar updates.</param>
    /// <param name="period">The period over which to calculate the Jvolty.</param>
    /// <param name="phase">The phase parameter for the JMA-style calculation.</param>
    /// <param name="vshort">The short-term volatility period.</param>
    public Jvolty(object source, int period, int vshort = 10) : this(period, vshort)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    /// <summary>
    /// Initializes the Jvolty instance by setting up the initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _upperBand = _lowerBand = 0.0;
        _p_upperBand = _p_lowerBand = 0.0;
        _len1 = Math.Max((Math.Log(Math.Sqrt(_period - 1)) / Math.Log(2.0)) + 2.0, 0);
        _pow1 = Math.Max(_len1 - 2.0, 0.5);
        _avoltyBuff.Clear();
        _avoltyBuff.Add(0, true);
        _avoltyBuff.Add(0, true);
    }

    /// <summary>
    /// Manages the state of the Jvolty instance based on whether a new bar is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new bar.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_upperBand = _upperBand;
            _p_lowerBand = _lowerBand;
        }
        else
        {
            _upperBand = _p_upperBand;
            _lowerBand = _p_lowerBand;
        }
    }

    /// <summary>
    /// Performs the Jvolty calculation for the current bar.
    /// </summary>
    /// <returns>
    /// The calculated Jvolty value for the current bar.
    /// </returns>
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        _values.Add(BarInput.Close, BarInput.IsNew);

        if (_index == 1)
        {
            return 0;
        }

        double hprice = _values.Max();
        double lprice = _values.Min();

        double del1 = hprice - _upperBand;
        double del2 = lprice - _lowerBand;
        double volty = Math.Max(Math.Abs(del1), Math.Abs(del2));

        _voltyShort.Add(volty, BarInput.IsNew);
        double vsum = _vsumBuff.Newest() + 0.1 * (volty - _voltyShort.Oldest());
        _vsumBuff.Add(vsum, BarInput.IsNew);

        double prevAvolty = _avoltyBuff.Newest();
        double avolty = prevAvolty + 2.0 / (Math.Max(4.0 * _period, 30) + 1.0) * (vsum - prevAvolty);
        _avoltyBuff.Add(avolty, BarInput.IsNew);

        double dVolty = (avolty > 0) ? volty / avolty : 0;
        dVolty = Math.Min(Math.Max(dVolty, 1.0), Math.Pow(_len1, 1.0 / _pow1));

        double pow2 = Math.Pow(dVolty, _pow1);
        double len2 = Math.Sqrt(0.5 * (_period - 1)) * _len1;
        double Kv = Math.Pow(len2 / (len2 + 1), Math.Sqrt(pow2));

        _upperBand = (del1 > 0) ? hprice : hprice - (Kv * del1);
        _lowerBand = (del2 < 0) ? lprice : lprice - (Kv * del2);

        IsHot = _index >= WarmupPeriod;
        return volty;
    }
}
