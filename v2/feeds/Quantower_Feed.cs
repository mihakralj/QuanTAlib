public class Quantower_Feed {
    private double _lastClose, _lastHigh, _lastLow;
    public TBar Bar { get; private set; }
    public GenericPub Open { get; }
    public GenericPub High { get; }
    public GenericPub Low { get; }
    public GenericPub Close { get; }
    public GenericPub Volume { get; }

    public event BarSignal? BarCreated;

    public Quantower_Feed(double initialPrice = 100.0) {
        _lastClose = _lastHigh = _lastLow = initialPrice;
        Open = new GenericPub(b => b.Open);
        High = new GenericPub(b => b.High);
        Low = new GenericPub(b => b.Low);
        Close = new GenericPub(b => b.Close);
        Volume = new GenericPub(b => b.Volume);
    }

    public void Update(DateTime time, double open, double high, double low, double close, double volume, bool isNew) {
        if (isNew) {
            _lastClose = close;
        } else {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        }

        _lastHigh = high;
        _lastLow = low;

        TBar bar = new(time, open, high, low, close, volume, isNew);
        this.Bar = bar;
        OnUpdate(new BarEventArgs(bar));
        PublishBarData(bar);
    }

    protected virtual void OnUpdate(BarEventArgs e) {
        BarCreated?.Invoke(this, e);
    }

    private void PublishBarData(TBar bar) {
        Open.Publish(bar);
        High.Publish(bar);
        Low.Publish(bar);
        Close.Publish(bar);
        Volume.Publish(bar);
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