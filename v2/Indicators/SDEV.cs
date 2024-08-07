public class StdDev
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private double _sum;
    private double _sumSquared;
    private CircularBuffer _buffer;
    private double _lastAddedValue;
    private readonly int _period;
    private int _index, _hotIndex;
    private double _lastValidStdDev;

    public StdDev(int period)
    {
        _period = period;
        Init();
    }

    public StdDev(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _buffer = new CircularBuffer(_period);
        _sum = 0;
        _sumSquared = 0;
        _lastValidStdDev = 0;
        _lastAddedValue = 0;
        _index = 0;
        _hotIndex = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastValidStdDev, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            if (_buffer.Count == _buffer.Capacity)
            {
                _sum -= _buffer[0];
                _sumSquared -= _buffer[0] * _buffer[0];
            }
            _buffer.Add(input.Value);
            _sum += input.Value;
            _sumSquared += input.Value * input.Value;
            _lastAddedValue = input.Value;
            _index++;
        }
        else
        {
            _sum = _sum - _lastAddedValue + input.Value;
            _sumSquared = _sumSquared - (_lastAddedValue * _lastAddedValue) + (input.Value * input.Value);
            _buffer[_buffer.Count - 1] = input.Value;
            _lastAddedValue = input.Value;
        }

        double mean = _sum / _buffer.Count;
        double variance = (_sumSquared / _buffer.Count) - (mean * mean);
        double stdDev = Math.Sqrt(Math.Max(0, variance));  // Ensure non-negative value under the sqrt

        _lastValidStdDev = stdDev;

        Tick = new TValue(input.Time, stdDev, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args)
    {
        Update(args.Tick);
    }
}