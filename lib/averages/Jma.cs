/// <summary>
/// Represents a Jurik Moving Average, based on known and reverse-engineered insights
/// </summary>

namespace QuanTAlib;

public class Jma : AbstractBase
{
    private readonly double _period;
    private readonly double _phase;
    private readonly CircularBuffer _vsumBuff;
    private readonly CircularBuffer _avoltyBuff;

    private double _len1;
    private double _pow1;
    private readonly double _beta;
    private double _upperBand, _lowerBand, _p_upperBand, _p_lowerBand;
    private double _prevMa1, _prevDet0, _prevDet1, _prevJma, _p_prevMa1, _p_prevDet0, _p_prevDet1, _p_prevJma;
    private double _vSum, _p_vSum;


    public double UpperBand { get; set; }
    public double LowerBand { get; set; }
    public double Volty { get; set; }
    public double Factor { get; set; }

    /// <summary>
    /// Initializes a new instance of the Jma class with the specified parameters.
    /// </summary>
    /// <param name="period">The period over which to calculate the Jvolty.</param>
    /// <param name="phase">The phase parameter for the JMA-style calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
    public Jma(int period, int phase = 0, double factor = 0.45)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Factor = factor;
        _period = period;
        _phase = Math.Clamp((phase * 0.01) + 1.5, 0.5, 2.5);

        _vsumBuff = new CircularBuffer(10);
        _avoltyBuff = new CircularBuffer(65);
        _beta = factor * (_period - 1) / (factor * (_period - 1) + 2);

        WarmupPeriod = period * 2;
        Name = $"JMA({period})";
    }

    /// <summary>
    /// Initializes a new instance of the Jvolty class with the specified source and parameters.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Jvolty.</param>
    /// <param name="phase">The phase parameter for the JMA-style calculation.</param>
    public Jma(object source, int period, int phase = 0) : this(period, phase)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Jma instance by setting up the initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _upperBand = _lowerBand = 0.0;
        _p_upperBand = _p_lowerBand = 0.0;
        _len1 = Math.Max((Math.Log(Math.Sqrt(_period - 1)) / Math.Log(2.0)) + 2.0, 0);
        _pow1 = Math.Max(_len1 - 2.0, 0.5);
        _avoltyBuff.Clear();
        _vsumBuff.Clear();
    }

    /// <summary>
    /// Manages the state of the Jma instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_upperBand = _upperBand;
            _p_lowerBand = _lowerBand;
            _p_vSum = _vSum;
            _p_prevMa1 = _prevMa1;
            _p_prevDet0 = _prevDet0;
            _p_prevDet1 = _prevDet1;
            _p_prevJma = _prevJma;
        }
        else
        {
            _upperBand = _p_upperBand;
            _lowerBand = _p_lowerBand;
            _vSum = _p_vSum;
            _prevMa1 = _p_prevMa1;
            _prevDet0 = _p_prevDet0;
            _prevDet1 = _p_prevDet1;
            _prevJma = _p_prevJma;
        }
    }

    /// <summary>
    /// Performs the Jma calculation for the current value.
    /// </summary>
    /// <returns>
    /// The calculated Jma value for the current input.
    /// </returns>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double price = Input.Value;
        if (_index <= 1)
        {
            _upperBand = _lowerBand = price;
            _prevMa1 = _prevJma = price;
        }

        double del1 = price - _upperBand;
        double del2 = price - _lowerBand;
        double volty = Math.Max(Math.Abs(del1), Math.Abs(del2));

        _vsumBuff.Add(volty, Input.IsNew);
        _vSum += (_vsumBuff[^1] - _vsumBuff[0]) / _vsumBuff.Count;
        _avoltyBuff.Add(_vSum, Input.IsNew);
        double avgvolty = _avoltyBuff.Average();

        double rvolty = (avgvolty > 0) ? volty / avgvolty : 1;
        rvolty = Math.Min(Math.Max(rvolty, 1.0), Math.Pow(_len1, 1.0 / _pow1));

        double pow2 = Math.Pow(rvolty, _pow1);
        double Kv = Math.Pow(_beta, Math.Sqrt(pow2));

        _upperBand = (del1 >= 0) ? price : price - (Kv * del1);
        _lowerBand = (del2 <= 0) ? price : price - (Kv * del2);

        double _alpha = Math.Pow(_beta, pow2);
        double ma1 = Input.Value + _alpha * (_prevMa1 - Input.Value);  //original: (1 - _alpha) * Input.Value + _alpha * _prevMa1;
        _prevMa1 = ma1;

        double det0 = price + _beta * (_prevDet0 - price + ma1) - ma1; //original: (price - ma1) * (1 - _beta) + _beta * _prevDet0;
        _prevDet0 = det0;
        double ma2 = ma1 + _phase * det0;

        double det1 = ((ma2 - _prevJma) * (1 - _alpha) * (1 - _alpha) ) + (_alpha * _alpha * _prevDet1);
        _prevDet1 = det1;
        double jma = _prevJma + det1;
        _prevJma = jma;

        UpperBand = _upperBand;
        LowerBand = _lowerBand;
        Volty = volty;

        IsHot = _index >= WarmupPeriod;
        return jma;
    }
}
