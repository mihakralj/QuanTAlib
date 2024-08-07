public class Slope
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private readonly int _period;
    private int _index, _hotIndex;
    private CircularBuffer _buffer;
    private double _sumX, _sumY, _sumXY, _sumX2;
    private double _lastValidSlope;

    public Slope(int period)
    {
        _period = period;
        Init();
    }

    public Slope(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _buffer = new CircularBuffer(_period);
        _sumX = 0;
        _sumY = 0;
        _sumXY = 0;
        _sumX2 = 0;
        _lastValidSlope = 0;
        _index = 0;
        _hotIndex = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastValidSlope, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            if (_buffer.Count == _buffer.Capacity)
            {
                double oldestY = _buffer[0];
                _sumY -= oldestY;
                _sumXY -= oldestY * (_index - _period);
                _sumX -= _index - _period;
                _sumX2 -= (_index - _period) * (_index - _period);
            }

            _buffer.Add(input.Value);
            _sumY += input.Value;
            _sumXY += input.Value * _index;
            _sumX += _index;
            _sumX2 += _index * _index;
            _index++;
        }
        else
        {
            double lastY = _buffer[_buffer.Count - 1];
            _sumY = _sumY - lastY + input.Value;
            _sumXY = _sumXY - (lastY * (_index - 1)) + (input.Value * (_index - 1));
            _buffer[_buffer.Count - 1] = input.Value;
        }

        double n = _buffer.Count;
        double slope = (n * _sumXY - _sumX * _sumY) / (n * _sumX2 - _sumX * _sumX);

        if (!double.IsNaN(slope) && !double.IsInfinity(slope))
        {
            _lastValidSlope = slope;
        }

        Tick = new TValue(input.Time, _lastValidSlope, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args)
    {
        Update(args.Tick);
    }
}