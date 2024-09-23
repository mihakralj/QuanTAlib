namespace QuanTAlib;

/// <summary>
/// Provides a base implementation for financial indicators in the QuanTAlib library.
/// This abstract class implements the iTValue interface and defines common properties
/// and methods used by inheriting indicator types.
/// </summary>
public abstract class AbstractBase : iTValue
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
    public bool IsNew { get; set; }
    public bool IsHot { get; set; }

    public TValue Input { get; set; }
    public String Name { get; set; } = "";
    public int WarmupPeriod { get; set; }

    public TValue Tick => new(Time, Value, IsNew, IsHot); // Stores the current value of indicator
    public event ValueSignal Pub = delegate { }; // Publisher of generated values

    protected int _index; //tracking the position of output
    protected double _lastValidValue;
    // other _internal vars defined here

    protected AbstractBase()
    {  //add parameters into constructor
    }

    /// <summary>
    /// Subscribes to a data source and triggers calculations on new data.
    /// </summary>
    /// <param name="source">The class publishing the data.</param>
    /// <param name="args">The argument containing the new data point.</param>
    public void Sub(object source, in ValueEventArgs args) => Calc(args.Tick);

    public virtual void Init()
    {
        _index = 0;
        _lastValidValue = 0;
    }

    /// <summary>
    /// Calculates the indicator value based on the input; calls specific Calculation() method
    /// where implementation is
    /// </summary>
    /// <param name="input">The input value for the calculation.</param>
    /// <returns>A TValue representing the calculated indicator value.</returns>
    public virtual TValue Calc(TValue input)
    {
        Input = input;
        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            return Process(new TValue(input.Time, GetLastValid(), input.IsNew, input.IsHot));
        }
        this.Value = Calculation();
        return Process(new TValue(Time: Input.Time, Value: this.Value, IsNew: Input.IsNew, IsHot: this.IsHot));
    }

    protected virtual double GetLastValid()
    {
        return this.Value;
    }
    protected abstract void ManageState(bool isNew);
    protected abstract double Calculation();

    /// <summary>
    /// Processes the calculated value, updates the indicator's own state,
    /// and publishes the result through an event.
    /// </summary>
    /// <param name="value">The calculated TValue to process.</param>
    /// <returns>The processed TValue.</returns>
    protected virtual TValue Process(TValue value)
    {
        this.Time = value.Time;
        this.Value = value.Value;
        this.IsNew = value.IsNew;
        this.IsHot = value.IsHot;
        Pub?.Invoke(this, new ValueEventArgs(value));
        return value;
    }
}
