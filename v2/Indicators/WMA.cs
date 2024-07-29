namespace QuanTAlib;

public class WMA
{
    private CircularBuffer buffer = null!;
    private CircularBuffer weights = null!;
    private int period;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public WMA(int period)
    {
        Init(period);
    }

    public void Init(int period)
    {
        this.period = period;
        this.buffer = new CircularBuffer(period);
        this.weights = new CircularBuffer(period);
        CalculateWeights();
        this.IsHot = false;
        this.Value = default;
    }

    public TValue Update(TValue input, bool IsNew = true)
    {
        if (IsNew)
        {
            buffer.Add(input);
        }
        else if (buffer.Count > 0)
        {
            buffer[buffer.Count - 1] = input;
        }
        else
        {
            buffer.Add(input);
        }

        double wma = 0;
        double totalWeights = 0;

        for (int i = 0; i < buffer.Count; i++)
        {
            wma += buffer[i] * weights[i];
            totalWeights += weights[i];
        }

        wma /= totalWeights;

        IsHot = buffer.Count >= period;
        Value = new TValue(input.Time, wma, IsNew, IsHot);
        return Value;
    }

    private void CalculateWeights()
    {
        for (int i = 1; i <= period; i++)
        {
            weights.Add(i);
        }
    }
}