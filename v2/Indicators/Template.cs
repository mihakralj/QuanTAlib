public class Template
{
    private CircularBuffer buffer = null!;
    private readonly int period;
    private int index;
    public TValue Value { get; private set; }
    public bool IsHot { get; private set; }

    public Template(int Period) {
        this.period = Period;
        Init();
    }

    public void Init() {
        this.buffer = new CircularBuffer(period);
        this.IsHot = false;
        this.Value = default;
        this.index = 0;
    }

    public TValue Update(TValue Input, bool IsNew = true) {
        this.buffer.Add(Input,IsNew);
        if (this.index == 0) {
            if (IsNew) { this.index++; }
            this.Value = new TValue(Input.Time, Input.Value, IsNew, true); 
            return this.Value;
        }

    if (IsNew) {
        // starting a new bar, fresh calc
        index++;
    } else {
        // updating existing bar, recalc
    }
        double ma = Input.Value;

        this.Value = new TValue(Input.Time, ma, IsNew, buffer.Count >= period);
        return this.Value;
    }
}