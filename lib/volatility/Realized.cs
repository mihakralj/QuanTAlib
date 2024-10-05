namespace QuanTAlib;
public class Realized : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _returns;
    private double _previousClose;
    private double _sumSquaredReturns;

    public Realized(int period, bool isAnnualized = true) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // We need one extra data point to calculate the first return
        _returns = new CircularBuffer(period);
        Name = $"Realized(period={period}, annualized={isAnnualized})";
        Init();
    }

    public override void Init()
    {
        base.Init();
        _returns.Clear();
        _previousClose = 0;
        _sumSquaredReturns = 0;
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

        double volatility = 0;
        if (_previousClose != 0)
        {
            double logReturn = Math.Log(Input.Value / _previousClose);

            if (_returns.Count == Period)
            {
                // Remove the oldest squared return from the sum
                _sumSquaredReturns -= Math.Pow(_returns[0], 2);
            }

            _returns.Add(logReturn, Input.IsNew);
            _sumSquaredReturns += Math.Pow(logReturn, 2);

            if (_returns.Count == Period)
            {
                double variance = _sumSquaredReturns / Period;
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