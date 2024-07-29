namespace QuanTAlib;

public class SMA
{
    private CircularBuffer buffer = null!;
    private int period;
    private double sum;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public SMA(int period)
    {
        Init(period);
    }

    public void Init(int period)
    {
        this.period = period;
        this.buffer = new CircularBuffer(period);
        this.sum = 0;
        this.IsHot = false;
        this.Value = default;
    }

    public TValue Update(TValue input, bool IsNew = true)
    {
        buffer.Add(input.value, IsNew);

        //calculate rolling sum

        double sma = sum / buffer.Count;
        Value = new TValue(input.Time, sma, isNew, IsHot);
        return Value;
        }

        double sma = buffer.Count > 0 ? sum / buffer.Count : double.NaN;
        IsHot = buffer.Count >= period;
        Value = new TValue(input.Time, sma, IsNew, IsHot);
        return Value;
    }
}
