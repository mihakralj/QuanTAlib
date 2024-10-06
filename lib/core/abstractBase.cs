namespace QuanTAlib;

/// <summary>
/// Provides a base implementation for financial indicators in the QuanTAlib library.
/// </summary>
/// <remarks>
/// This abstract class implements the iTValue interface and defines common properties
/// and methods used by inheriting indicator types. It handles the basic flow of
/// receiving data, performing calculations, and publishing results.
/// </remarks>
public abstract class AbstractBase : ITValue
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
    public bool IsNew { get; set; }
    public bool IsHot { get; set; }
    public TValue Input { get; set; }
    public String Name { get; set; } = "";
    public int WarmupPeriod { get; set; }
    public TValue Tick => new(Time, Value, IsNew, IsHot);
    public event ValueSignal Pub = delegate { };
    protected int _index;
    protected double _lastValidValue;

    protected AbstractBase()
    {
        // Add parameters into constructor if needed
    }

    /// <summary>
    /// Subscribes to a data source and triggers calculations on new data.
    /// </summary>
    /// <param name="source">The class publishing the data.</param>
    /// <param name="args">The argument containing the new data point.</param>
    public void Sub(object source, in ValueEventArgs args) => Calc(args.Tick);

    /// <summary>
    /// Initializes the indicator's state.
    /// </summary>
    public virtual void Init()
    {
        _index = 0;
        _lastValidValue = 0;
    }

    /// <summary>
    /// Calculates the indicator value based on the input.
    /// </summary>
    /// <param name="input">The input value for the calculation.</param>
    /// <returns>A TValue representing the calculated indicator value.</returns>
    /// <remarks>
    /// This method calls the specific Calculation() method where the actual implementation is.
    /// If the input value is NaN or infinity, it returns the last valid value instead.
    /// </remarks>
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

    /// <summary>
    /// Retrieves the last valid calculated value.
    /// </summary>
    /// <returns>The last valid value of the indicator.</returns>
    protected virtual double GetLastValid()
    {
        return this.Value;
    }

    /// <summary>
    /// Manages the state of the indicator based on whether a new data point is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new data point.</param>
    protected abstract void ManageState(bool isNew);

    /// <summary>
    /// Performs the actual calculation of the indicator value.
    /// </summary>
    /// <returns>The calculated indicator value.</returns>
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
