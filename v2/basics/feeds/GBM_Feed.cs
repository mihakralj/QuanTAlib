public class EventObject
{
    public event Signal? Pub;
    public TValue Tick { get; private set; }

    internal void Invoke(object sender, ValueEventArgs args) {
        Tick = args.Data;
        Pub?.Invoke(sender, args);
    }

    public void Subscribe(Signal handler) {
        Pub += handler;
    }

    public void Publish(object sender, ValueEventArgs args) {
        Invoke(sender, args);
    }

    public static explicit operator double(EventObject eo) {
        return eo.Tick.v;
    }
}

public class GBM_Feed
{
    private readonly double _mu;
    private readonly double _sigma;
    private readonly Random _random;
    private double _lastClose;
    private double _lastHigh;
    private double _lastLow;
    public TBar Bar { get; private set; }
    private readonly EventObject _eventObject = new EventObject();

    public event BarSignal? PubBar;
    public EventObject Open { get; } = new EventObject();
    public EventObject High { get; } = new EventObject();
    public EventObject Low { get; } = new EventObject();
    public EventObject Close { get; } = new EventObject();
    public EventObject Volume { get; } = new EventObject();

    // Aliases
    public EventObject o => Open;
    public EventObject h => High;
    public EventObject l => Low;
    public EventObject c => Close;
    public EventObject v => Volume;


    public GBM_Feed(double initialPrice, double mu, double sigma)
    {
        _lastClose = initialPrice;
        _lastHigh = initialPrice;
        _lastLow = initialPrice;
        _mu = mu;
        _sigma = sigma;
        _random = new Random();
    }

    public TBar Update(bool isNew = true)
    {
        DateTime time = DateTime.UtcNow;
        double dt = 1.0 / 252; // Assuming daily steps in a trading year of 252 days
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * NormalRandom();
        double newClose = _lastClose * Math.Exp(drift + diffusion);

        double open = _lastClose;
        double high = Math.Max(open, newClose) * (1 + _random.NextDouble() * 0.01);
        double low = Math.Min(open, newClose) * (1 - _random.NextDouble() * 0.01);
        double volume = 1000 + _random.NextDouble() * 1000; // Random volume between 1000 and 2000

        if (!isNew)
        {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        }
        else
        {
            _lastClose = newClose;
        }

        _lastHigh = high;
        _lastLow = low;

        TBar bar = new(time, open, high, low, newClose, volume, isNew);
        this.Bar = bar;

        InvokeBar(this, new BarEventArgs(bar));
        Open.Invoke(this, new ValueEventArgs(new TValue(time, open, isNew)));
        High.Invoke(this, new ValueEventArgs(new TValue(time, high, isNew)));
        Low.Invoke(this, new ValueEventArgs(new TValue(time, low, isNew)));
        Close.Invoke(this, new ValueEventArgs(new TValue(time, newClose, isNew)));
        Volume.Invoke(this, new ValueEventArgs(new TValue(time, volume, isNew)));

        return bar;
    }

    private double NormalRandom()
    {
        // Box-Muller transform to generate standard normal random variable
        double u1 = 1.0 - _random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    public void Pub(TValue tick) {
        _eventObject.Publish(this, new ValueEventArgs(tick));
    }

    public void Subscribe(Signal handler)
    {
        _eventObject.Subscribe(handler);
    }

    private void InvokeBar(object sender, BarEventArgs args) => PubBar?.Invoke(sender, args);
}
