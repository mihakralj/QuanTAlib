using System;

public readonly struct TValue {
    public DateTime Timestamp { get; }
    public double Value { get; }

    public TValue(DateTime timestamp, double value) {
        Timestamp = timestamp;
        Value = value;
    }
    public TValue() : this(DateTime.Now, 0) { }
    public TValue(double value) : this(DateTime.Now, value) { }
    public static implicit operator double(TValue tv) => tv.Value;
    public static implicit operator DateTime(TValue tv) => tv.Timestamp;
    public static implicit operator TValue(double value) => new TValue(DateTime.Now, value);


    public override string ToString() {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}: {Value:F2}]";
    }

    public override bool Equals(object obj) {
        return obj is TValue other && Equals(in other);
    }

    public bool Equals(in TValue other) {
        return Timestamp == other.Timestamp && Value == other.Value;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Timestamp, Value);
    }
}


public readonly struct TBar
{
    public DateTime Timestamp { get; }
    public double Open { get; }
    public double High { get; }
    public double Low { get; }
    public double Close { get; }
    public double Volume { get; }

    public TBar(DateTime timestamp, double open, double high, double low, double close, double volume)
    {
        Timestamp = timestamp;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
    }

    public override bool Equals(object obj)
    {
        return obj is TBar other && Equals(in other);
    }

    public bool Equals(in TBar other)
    {
        return Timestamp == other.Timestamp &&
               Open == other.Open &&
               High == other.High &&
               Low == other.Low &&
               Close == other.Close &&
               Volume == other.Volume;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Timestamp, Open, High, Low, Close, Volume);
    }
}



public class EventArg<T> : EventArgs
{
    public T Data { get; }
    public bool IsClosed { get; }
    public bool IsHot { get; }

    public EventArg(T data, bool isClosed, bool isHot)
    {
        Data = data;
        IsClosed = isClosed;
        IsHot = isHot;
    }
}