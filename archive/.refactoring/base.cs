using System;

public readonly record struct TValue(DateTime Time, double Value, bool IsNew = true, bool IsHot = true)
{
    public DateTime Time { get; init; } = Time;
public double Value { get; init; } = Value;
public bool IsNew { get; init; } = IsNew;
public bool IsHot { get; init; } = IsHot;

public TValue() : this(DateTime.UtcNow, 0) { }
public TValue(double value) : this(DateTime.UtcNow, value) { }
public TValue((DateTime time, double value) tuple) : this(tuple.time, tuple.value) { }

public static implicit operator double(TValue tv) => tv.Value;
public static implicit operator DateTime(TValue tv) => tv.Time;
public static implicit operator TValue(double value) => new TValue(DateTime.UtcNow, value);

public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: {Value:F2}]";
}


public readonly record struct TBar(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true)
{
    public DateTime Time { get; init; } = Time;
public double Open { get; init; } = Open;
public double High { get; init; } = High;
public double Low { get; init; } = Low;
public double Close { get; init; } = Close;
public double Volume { get; init; } = Volume;
public bool IsNew { get; init; } = IsNew;

public TBar() : this(DateTime.UtcNow, 0, 0, 0, 0, 0) { }
public TBar(double open, double high, double low, double close, double volume) : this(DateTime.UtcNow, open, high, low, close, volume) { }
public TBar((DateTime time, double open, double high, double low, double close, double volume) tuple) : this(tuple.time, tuple.open, tuple.high, tuple.low, tuple.close, tuple.volume) { }

public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}

/////////////////////
///
/////////////////////

public class GBM_Feed {
    private readonly double _mu;
    private readonly double _sigma;
    private readonly Random _random;
    private double _lastClose;
    private double _lastHigh;
    private double _lastLow;

    public GBM_Feed(double initialPrice, double mu, double sigma) {
        _lastClose = initialPrice;
        _lastHigh = initialPrice;
        _lastLow = initialPrice;
        _mu = mu;
        _sigma = sigma;
        _random = Random.Shared;
    }

    public TBar Generate(bool IsNew = true) {
        DateTime time = DateTime.UtcNow;
        double dt = 1.0 / 252; // Assuming daily steps in a trading year of 252 days
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * NormalRandom();
        double newClose = _lastClose * Math.Exp(drift + diffusion);

        double open = _lastClose;
        double high = Math.Max(open, newClose) * (1 + _random.NextDouble() * 0.01);
        double low = Math.Min(open, newClose) * (1 - _random.NextDouble() * 0.01);
        double volume = 1000 + _random.NextDouble() * 1000; // Random volume between 1000 and 2000

        if (!IsNew) {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        } else {
            _lastClose = newClose;
        }

        _lastHigh = high;
        _lastLow = low;

        return new TBar(time, open, high, low, newClose, volume, IsNew);
    }

    private double NormalRandom() {
        // Box-Muller transform to generate standard normal random variable
        double u1 = 1.0 - _random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}


/// <summary>
/// ////////////////
/// </summary>

public class EMA {
    private double lastEma, lastEmaCandidate, k;
    private int period, i;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public EMA(int period) {
        Init(period);
    }

    public void Init(int period) {
        this.period = period;
        this.k = 2.0 / (period + 1);
        this.lastEma = this.lastEmaCandidate = double.NaN;
        this.i = 0;
    }
    public TValue Update(TValue input, bool IsNew = true) {
        double ema;

        if (double.IsNaN(lastEma)) { lastEma = input.Value; }

        if (IsNew) {
            lastEma = lastEmaCandidate;
            i++;
        }

        double kk = (i < period) ? (2.0 / (i + 1)) : k;
        ema = lastEma + kk * (input.Value - lastEma);
        lastEmaCandidate = ema;

        IsHot = i >= period;
        Value = new TValue(input.Time, ema, IsNew, IsHot);
        return Value;
    }
}

/////////////////
///

public class SMA {
    private CircularBuffer<double> buffer;
    private int period;
    private double sum;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public SMA(int period) {
        Init(period);
    }

    public void Init(int period) {
        this.period = period;
        this.buffer = new CircularBuffer<double>(period);
        this.sum = 0;
        this.IsHot = false;
        this.Value = default;
    }

    public TValue Update(TValue input, bool IsNew = true) {
        if (IsNew) {
            if (buffer.Count == period) {
                sum -= buffer[0];
            }
            buffer.Add(input);
            sum += input.Value;
        } else {
            if (buffer.Count > 0) {
                sum -= buffer[buffer.Count - 1];
                sum += input.Value;
                buffer[buffer.Count - 1] = input;
            } else {
                buffer.Add(input);
                sum += input.Value;
            }
        }

        double sma = buffer.Count > 0 ? sum / buffer.Count : double.NaN;
        IsHot = buffer.Count >= period;
        Value = new TValue(input.Time, sma, IsNew, IsHot);
        return Value;
    }
}

/////////////////////
///
/////////////////////


public class CircularBuffer<double> {
    private double[] _buffer;
    private int _start;
    private int _size;

    public CircularBuffer(int capacity) {
        _buffer = new double[capacity];
        _start = 0;
        _size = 0;
    }

    public int Capacity => _buffer.Length;
    public int Count => _size;

    public void Add(double item) {
        if (_size < Capacity) {
            _buffer[(_start + _size) % Capacity] = item;
            _size++;
        } else {
            _buffer[_start] = item;
            _start = (_start + 1) % Capacity;
        }
    }

    public double this[int index] {
        get {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            return _buffer[(_start + index) % Capacity];
        }
        set {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            _buffer[(_start + index) % Capacity] = value;
        }
    }
}