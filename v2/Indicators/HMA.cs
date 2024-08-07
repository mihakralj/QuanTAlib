public class HMA
{
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private readonly int _period;
    private int _index, _hotIndex;

    private WMA _wmaHalf;
    private WMA _wmaFull;
    private WMA _wmaFinal;
    private CircularBuffer _diffBuffer;
    private double _lastValidHMA;

    public HMA(int period)
    {
        _period = period;
        Init();
    }

    public HMA(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        _wmaHalf = new WMA(_period / 2);
        _wmaFull = new WMA(_period);
        _wmaFinal = new WMA((int)Math.Sqrt(_period));
        _diffBuffer = new CircularBuffer((int)Math.Sqrt(_period));
        _lastValidHMA = 0;
        _index = 0;
        _hotIndex = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastValidHMA, input.IsNew, IsHot);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            _index++;
        }

        // Calculate WMA with period/2 and period
        var wmaHalf = _wmaHalf.Update(input);
        var wmaFull = _wmaFull.Update(input);

        // Calculate 2 * WMA(n/2) - WMA(n)
        double diffWma = 2 * wmaHalf.Value - wmaFull.Value;

        // Add the difference to the buffer for final WMA calculation
        _diffBuffer.Add(diffWma, input.IsNew);

        // Calculate final WMA
        var hma = _wmaFinal.Update(new TValue(input.Time, diffWma, input.IsNew, input.IsHot));

        _lastValidHMA = hma.Value;

        Tick = new TValue(input.Time, hma.Value, input.IsNew, IsHot);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args)
    {
        Update(args.Tick);
    }
}