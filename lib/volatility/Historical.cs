namespace QuanTAlib;

public class Historical : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _buffer;
    private readonly CircularBuffer _logReturns;
    private double _previousClose;

    public Historical(int period, bool isAnnualized = true) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // We need one extra data point to calculate the first return
        _buffer = new CircularBuffer(period + 1);
        _logReturns = new CircularBuffer(period);
        Name = $"Historical(period={period}, annualized={isAnnualized})";
        Init();
    }

    public Historical(object source, int period, bool isAnnualized = true) : this(period, isAnnualized)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _logReturns.Clear();
        _previousClose = 0;
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

        _buffer.Add(Input.Value, Input.IsNew);

        double volatility = 0;
        if (_buffer.Count > 1)
        {
            if (_previousClose != 0)
            {
                double logReturn = Math.Log(Input.Value / _previousClose);
                _logReturns.Add(logReturn, Input.IsNew);
            }

            if (_logReturns.Count == Period)
            {
                var returns = _logReturns.GetSpan().ToArray();
                double mean = returns.Average();
                double sumOfSquaredDifferences = returns.Sum(x => Math.Pow(x - mean, 2));

                double variance = sumOfSquaredDifferences / (Period - 1);  // Using sample standard deviation
                volatility = Math.Sqrt(variance);

                if (IsAnnualized)
                {
                    // Assuming 252 trading days in a year. Adjust as needed.
                    volatility *= Math.Sqrt(252);
                }
            }
        }

        _previousClose = Input.Value;
        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
