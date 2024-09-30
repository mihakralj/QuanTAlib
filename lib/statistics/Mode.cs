namespace QuanTAlib;

public class Mode : AbstractBase
{
    public readonly int Period;
    private readonly CircularBuffer _buffer;

    public Mode(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        Name = $"Mode(period={period})";
        Init();
    }

    public Mode(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
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

        double mode;
        if (_index >= Period)
        {
            var values = _buffer.GetSpan().ToArray();
            var groupedValues = values.GroupBy(v => v)
                                      .OrderByDescending(g => g.Count())
                                      .ThenBy(g => g.Key)
                                      .ToList();

            int maxCount = groupedValues.First().Count();
            var modes = groupedValues.TakeWhile(g => g.Count() == maxCount)
                                     .Select(g => g.Key)
                                     .ToList();

            mode = modes.Average(); // If there are multiple modes, we return their average
        }
        else
        {
            mode = _buffer.Average(); // Use average until we have enough data points
        }

        IsHot = _index >= WarmupPeriod;
        return mode;
    }
}
