namespace QuanTAlib;

public class Ama : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private readonly double _alpha; // Adaptive factor
    private double _lastAfirma, _p_lastAfirma;
    private double _lastError, _p_lastError;

    public Ama(int period, double alpha = 0.1)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        if (alpha <= 0 || alpha >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 and 1 (exclusive).");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        _alpha = alpha;
        Name = "Afirma";
        WarmupPeriod = period;
        Init();
    }

    public Ama(object source, int period, double alpha = 0.1) : this(period: period, alpha: alpha)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _lastAfirma = 0;
        _lastError = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastAfirma = _lastAfirma;
            _p_lastError = _lastError;
        }
        else
        {
            _lastAfirma = _p_lastAfirma;
            _lastError = _p_lastError;
        }
    }

    /// <summary>
    /// Core AFIRMA calculation
    /// </summary>
    protected override double Calculation()
    {
        double result;
        ManageState(IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        if (_index < Period)
        {
            // Use simple average during warmup period
            result = _buffer.Average();
        }
        else
        {
            // AFIRMA calculation
            double sma = _buffer.Average();
            double error = Input.Value - _lastAfirma;
            double denominator = Math.Abs(error) + Math.Abs(_lastError);
            double adaptiveFactor = denominator != 0 ? _alpha * Math.Abs(error) / denominator : _alpha;
            result = sma + adaptiveFactor * (Input.Value - sma);

            _lastError = error;
        }

        _lastAfirma = result;
        IsHot = _index >= WarmupPeriod;
        return result;
    }
}