namespace QuanTAlib;

public class Smape : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    public Smape(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Smape(period={period})";
        Init();
    }

    public Smape(object source, int period) : this(period)
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

        double smape = 0;
        if (_actualBuffer.Count > 0)
        {
            var actualValues = _actualBuffer.GetSpan().ToArray();
            var predictedValues = _predictedBuffer.GetSpan().ToArray();

            double sumSymmetricAbsolutePercentageError = 0;
            int validCount = 0;

            for (int i = 0; i < _actualBuffer.Count; i++)
            {
                double denominator = Math.Abs(actualValues[i]) + Math.Abs(predictedValues[i]);
                if (denominator != 0)
                {
                    sumSymmetricAbsolutePercentageError += Math.Abs(actualValues[i] - predictedValues[i]) / denominator;
                    validCount++;
                }
            }

            smape = validCount > 0 ? (200 * sumSymmetricAbsolutePercentageError / validCount) : 0;
        }

        IsHot = _index >= WarmupPeriod;
        return smape;
    }
}
