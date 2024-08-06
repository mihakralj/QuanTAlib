//public delegate void Signal(object source, TValue args);
using System.Reflection;

public class SMA
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private double _sum;
    private double _lastValidSMA;
    private CircularBuffer _buffer;
    private double _lastAddedValue;
    private readonly int _period;
    private int _index, _hotIndex;

    public SMA(int period) {
        _period = period;
        _buffer = new CircularBuffer(0);
        Init();
    }

    public SMA(object source, int period) : this(period) {
        //Console.WriteLine($"{source.GetType()}");
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _buffer = new CircularBuffer(_period);
        _sum = 0;
        _lastValidSMA = 0;
        _lastAddedValue = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value)) {
            Tick = new TValue(input.Time, _lastValidSMA, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew) {
            if (_buffer.Count == _buffer.Capacity) {
                _sum -= _buffer[0];
            }
            _buffer.Add(input.Value);
            _sum += input.Value;
            _lastAddedValue = input.Value;
            _index++;
        } else {
            _sum = _sum - _lastAddedValue + input.Value;
            _buffer[_buffer.Count - 1] = input.Value;
            _lastAddedValue = input.Value;
        }

        double sma = _sum / _buffer.Count;
        _lastValidSMA = sma;

        Tick = new TValue(input.Time, sma, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args) {
        //Console.WriteLine($"SMA received event: {args.Tick.Value}");

        Update(args.Tick);
    }
}