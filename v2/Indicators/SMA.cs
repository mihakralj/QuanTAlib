
public class SMA
{
    private readonly int _period;
    private int _index, _hotIndex;
    public TValue Value { get; private set; }
    public bool IsHot => _index >= _period;
    public int Period => Math.Min(_index, _period);
    private double _sum;
    private double _lastValidSMA;
    private CircularBuffer _buffer;
    private double _lastAddedValue;

    public SMA(int period) {
        _period = period;
        Init();
    }

    public SMA(object source, int period) : this(period) {
        var sourceType = source.GetType();
        var updateMethod = sourceType.GetMethod("Update", new[] { typeof(TValue) });
        
        if (updateMethod != null) {
            var pubEvent = sourceType.GetEvent("Pub");
            if (pubEvent != null && pubEvent.EventHandlerType == typeof(Signal)) {
                pubEvent.AddEventHandler(source, new Signal(Sub));
            } else {
                throw new ArgumentException("Source object must have a Pub event of type NewValue.");
            }
        } else {
            throw new ArgumentException("Source object must have an Update(TValue) method.");
        }
    }

    public void Init() {
        _buffer = new CircularBuffer(_period);
        _sum = 0;
        _lastValidSMA = 0;
        Value = default;
        _index = _hotIndex = 0;
        _lastAddedValue = 0;
    }

    public TValue Update(TValue input) {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value)) {
            Value = new TValue(input.Time, _lastValidSMA, input.IsNew, _index > _period);
            Pub?.Invoke(this, Value);
            return Value;
        }

        if (input.IsNew) {
            if (_buffer.Count == _buffer.Capacity) {
                _sum -= _buffer[0];
            }
            _buffer.Add(input.Value, true);
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

        Value = new TValue(input.Time, sma, input.IsNew, _index > (_period + _hotIndex));
        Pub?.Invoke(this, Value);
        return Value;
    }

    public void Sub(object source, TValue arg) {
        Update(arg);
    }

    public event Signal Pub;
}