public class EMA
{
    private readonly int period;
    private int index;
    public TValue Value { get; private set; }
    public bool IsHot => index > period;
    public int Period => Math.Min(index, period);
    private double k;
    private double lastEMA, lastEMAcandidate;

    public EMA(int Period) {
        this.period = Period;
        Init();
    }

    public void Init() {
        this.Value = default;
        this.index = 0;
        this.k = 2.0 / (period + 1);
        this.lastEMA = 0;
        this.lastEMAcandidate = 0;
    }

    public TValue Update(TValue Input, bool IsNew = true) {
        double ma;
        if (double.IsNaN(Input.Value) || double.IsInfinity(Input.Value)) { 
            return new TValue(Input.Time, lastEMA, IsNew, index > period);
        }
        if (IsNew) {
            if (index<1) { lastEMA = Input.Value; }
            lastEMAcandidate = lastEMA;
            index++;
        } else {
            if (index<=1) { lastEMAcandidate = Input.Value; }
            lastEMA = lastEMAcandidate;
        }

        double kk = (index <= period) ? (2.0 / (index+1)) : k;
        ma = (Input.Value - lastEMA) * kk + lastEMA;
        lastEMA = ma;

        this.Value = new TValue(Input.Time, ma, IsNew, index > period);
        return this.Value;
    }
}