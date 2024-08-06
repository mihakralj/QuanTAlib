public class GBM_Feed {
    private readonly double _mu, _sigma;
    private readonly Random _random;
    private double _lastClose, _lastHigh, _lastLow;
    public TBar Bar { get; private set; }
    public GenericPub Open { get; }
    public GenericPub High { get; }
    public GenericPub Low { get; }
    public GenericPub Close { get; }
    public GenericPub Volume { get; }

    public event BarSignal? BarPub;

    public GBM_Feed(double initialPrice = 100.0, double mu = 0.05, double sigma = 0.2) {
        _lastClose = _lastHigh = _lastLow = initialPrice;
        _mu = mu;
        _sigma = sigma;
        _random = new Random();
        Open = new GenericPub(b => b.Open);
        High = new GenericPub(b => b.High);
        Low = new GenericPub(b => b.Low);
        Close = new GenericPub(b => b.Close);
        Volume = new GenericPub(b => b.Volume);
    }

    public TBar Update(bool isNew = true) {
        DateTime time = DateTime.UtcNow;
        double dt = 1.0 / 252;
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * NormalRandom();
        double newClose = _lastClose * Math.Exp(drift + diffusion);

        double open = _lastClose;
        double high = Math.Max(open, newClose) * (1 + _random.NextDouble() * 0.01);
        double low = Math.Min(open, newClose) * (1 - _random.NextDouble() * 0.01);
        double volume = 1000 + _random.NextDouble() * 1000;

        if (isNew) {
            _lastClose = newClose;
        } else {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        }

        _lastHigh = high;
        _lastLow = low;

        TBar bar = new(time, open, high, low, newClose, volume, isNew);
        this.Bar = bar;
        BarPub?.Invoke(this, new BarEventArgs(Bar));
        Open.Publish(Bar);
        High.Publish(Bar);
        Low.Publish(Bar);
        Close.Publish(Bar);
        Volume.Publish(Bar);
        return bar;
    }

    private double NormalRandom() {
        // Box-Muller transform to generate standard normal random variable
        double u1 = 1.0 - _random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    public class GenericPub {
        public event Signal? Pub;
        private readonly Func<TBar, double> _selector;

        public GenericPub(Func<TBar, double> selector) {
            _selector = selector;
        }

        public void Publish(TBar bar) {
            Pub?.Invoke(this, new ValueEventArgs(new TValue(bar.Time, _selector(bar), bar.IsNew, true)));
        }
    }
}