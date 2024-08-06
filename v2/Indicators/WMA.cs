public class WMA
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private CircularBuffer _buffer;
    private double _lastValidWMA;
    private double _lastAddedValue;
    private readonly int _period;
    private int _index, _hotIndex;

    public WMA(int period)
    {
        _period = period;
        _buffer = new CircularBuffer(period);
        Init();
    }

    public WMA(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _buffer = new CircularBuffer(_period);
        _lastValidWMA = 0;
        _lastAddedValue = 0;
        _index = 0;
        _hotIndex = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastValidWMA, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        _buffer.Add(input.Value, input.IsNew);
        _lastAddedValue = input.Value;

        if (input.IsNew)
        {
            _index++;
        }

        double wma = CalculateWMA();
        _lastValidWMA = wma;

        Tick = new TValue(input.Time, wma, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    private double CalculateWMA()
    {
        double sum = 0;
        int weight = _buffer.Count;
        int weightSum = (weight * (weight + 1)) / 2;

        for (int i = 0; i < _buffer.Count; i++)
        {
            sum += _buffer[i] * (i + 1);
        }

        return sum / weightSum;
    }

    public void Sub(object source, ValueEventArgs args) {
        Update(args.Tick);
    }
}