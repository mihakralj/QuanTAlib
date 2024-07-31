public class SMA
{
    public CircularBuffer buffer = null!;
    private readonly int period;
    public double sum;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public SMA(int period)
    {
        this.period = period;
        Init();
    }

    public void Init()
    {
        this.buffer = new CircularBuffer(period);
        this.sum = 0;
        this.IsHot = false;
        this.Value = default;
    }

    public TValue Update(TValue input, bool isNew = true)
    {
        if (buffer.Count == 0)
        {
            // If buffer is empty, always add the value regardless of isNew
            buffer.Add(input.Value, true);
            sum = input.Value;
        }
        else if (isNew && buffer.Count == buffer.Capacity)
        {
            // If buffer is full and it's a new value, remove oldest
            sum -= buffer[0];
            buffer.Add(input.Value, true);
            sum += input.Value;
        }
        else
        {
            // If it's not new, or if buffer isn't full yet
            if (!isNew)
            {
                // Remove the last value if we're updating
                sum -= buffer[buffer.Count - 1];
            }
            buffer.Add(input.Value, isNew);
            sum += input.Value;
        }

        double sma = sum / buffer.Count;
        IsHot = buffer.Count >= period;
        Value = new TValue(input.Time, sma, isNew, IsHot);
        return Value;
    }
}