namespace QuanTAlib;

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

