using System;

public struct TValue
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }

    public TValue(DateTime timestamp, double value)
    {
        Timestamp = timestamp;
        Value = value;
    }

    public override string ToString()
    {
        return $"[{this.Timestamp:yyyy-MM-dd HH:mm:ss}: {this.Value:F2}]";
    }
    public override bool Equals(object obj)
    {
        if (obj is TValue other)
        {
            return Timestamp == other.Timestamp && Value == other.Value;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Timestamp, Value);
    }
}


public struct TBar
{
    public DateTime Timestamp { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }

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
        return $"[{this.Timestamp:yyyy-MM-dd HH:mm:ss}: O={this.Open:F2}, H={this.High:F2}, L={this.Low:F2}, C={this.Close:F2}, V={this.Volume:F2}]";
    }

    public override bool Equals(object obj)
    {
        if (obj is TBar other)
        {
            return Timestamp == other.Timestamp &&
                   Open == other.Open &&
                   High == other.High &&
                   Low == other.Low &&
                   Close == other.Close &&
                   Volume == other.Volume;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Timestamp, Open, High, Low, Close, Volume);
    }
}



public class TValueEventArg<T> : EventArgs
{
    public T Data { get; }
    public bool IsClosed { get; }
    public bool IsHot { get; }

    public TValueEventArg(T data, bool isClosed, bool isHot)
    {
        Data = data;
        IsClosed = isClosed;
        IsHot = isHot;
    }
}