namespace QuanTAlib;

public class EMA
{
    private double lastEma, lastEmaCandidate, k;
    private int period, i;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public EMA(int period)
    {
        Init(period);
    }

    public void Init(int period)
    {
        this.period = period;
        this.k = 2.0 / (period + 1);
        this.lastEma = this.lastEmaCandidate = double.NaN;
        this.i = 0;
    }
    public TValue Update(TValue input, bool IsNew = true)
    {
        double ema;

        if (double.IsNaN(lastEma)) { lastEma = input.Value; }

        if (IsNew)
        {
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