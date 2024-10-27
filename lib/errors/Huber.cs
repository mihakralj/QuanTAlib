namespace QuanTAlib;

public class Huber : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;
    private readonly double _delta;

    public Huber(int period, double delta = 1.0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        if (delta <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be greater than 0.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        _delta = delta;
        Name = $"Huberloss(period={period}, delta={delta})";
        Init();
    }

    public Huber(object source, int period, double delta = 1.0) : this(period, delta)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
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

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double huberloss = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumLoss = 0;
            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double error = actualValues[i] - predictedValues[i];
                double absError = Math.Abs(error);

                if (absError <= _delta)
                {
                    sumLoss += 0.5 * error * error;
                }
                else
                {
                    sumLoss += _delta * (absError - 0.5 * _delta);
                }
            }

            huberloss = sumLoss / _actualBuffer.Count;
        }

        IsHot = _index >= WarmupPeriod;
        return huberloss;
    }

}
