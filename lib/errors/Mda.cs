namespace QuanTAlib;

public class Mda : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    public Mda(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mda(period={period})";
        Init();
    }

    public Mda(object source, int period) : this(period)
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

        double mda = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumDirectionalAccuracy = 0;
            for (int i = 1; i < _actualBuffer.Count; i++)
            {
                double actualDirection = Math.Sign(actualValues[i] - actualValues[i - 1]);
                double predictedDirection = Math.Sign(predictedValues[i] - predictedValues[i - 1]);
                sumDirectionalAccuracy += (actualDirection == predictedDirection) ? 1 : 0;
            }

            mda = sumDirectionalAccuracy / (_actualBuffer.Count - 1);
        }

        IsHot = _index >= WarmupPeriod;
        return mda;
    }
}
