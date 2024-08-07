public class MAD
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private readonly int _period;
    private int _index, _hotIndex;
    private CircularBuffer _buffer;
    private double _sum;
    private double _lastValidMAD;
    private double _lastAddedValue;

    public MAD(int period)
    {
        _period = period;
        _buffer = new CircularBuffer(0);
        Init();
    }

    public MAD(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _buffer = new CircularBuffer(_period);
        _sum = 0;
        _lastValidMAD = 0;
        _lastAddedValue = 0;
        _index = 0;
        _hotIndex = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastValidMAD, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            if (_buffer.Count == _buffer.Capacity)
            {
                _sum -= _buffer[0];
            }
            _buffer.Add(input.Value);
            _sum += input.Value;
            _lastAddedValue = input.Value;
            _index++;
        }
        else
        {
            _sum = _sum - _lastAddedValue + input.Value;
            _buffer[_buffer.Count - 1] = input.Value;
            _lastAddedValue = input.Value;
        }

        double mean = _sum / _buffer.Count;
        double madSum = 0;

        for (int i = 0; i < _buffer.Count; i++)
        {
            madSum += Math.Abs(_buffer[i] - mean);
        }

        double mad = madSum / _buffer.Count;
        _lastValidMAD = mad;

        Tick = new TValue(input.Time, mad, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args)
    {
        Update(args.Tick);
    }
}