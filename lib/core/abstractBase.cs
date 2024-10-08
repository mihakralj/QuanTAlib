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
    public TValue Input2 { get; set; }
    public TBar BarInput { get; set; }
    public TBar BarInput2 { get; set; }
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

    public void Sub(object source1, object source2, in ValueEventArgs args1, in ValueEventArgs args2) =>
        Calc(args1.Tick, args2.Tick);

    public void Sub(object source, in TBarEventArgs args) => Calc(args.Bar);

    /// <summary>
    /// Initializes the indicator's state.
    /// </summary>
    public virtual void Init()
    {
        _index = 0;
        _lastValidValue = 0;
    }

    public virtual TValue Calc(TValue input)
    {
        Input = input;
        Input2 = new(Time: Input.Time, Value: double.NaN, IsNew: Input.IsNew, IsHot: Input.IsHot);
        return HandleErrorCalculations(input.Value, input.Time, input.IsNew);
    }

    public virtual TValue Calc(TBar barInput)
    {
        BarInput = barInput;
        return HandleErrorCalculations(barInput.Close, barInput.Time, barInput.IsNew);
    }

    public virtual TValue Calc(TValue input1, TValue input2)
    {
        Input = input1;
        Input2 = input2;
        return HandleErrorCalculations(input1.Value, input2.Value, input1.Time, input1.IsNew);
    }

    public virtual TValue Calc(TBar input1, TBar input2)
    {
        BarInput = input1;
        BarInput2 = input2;
        return HandleErrorCalculations(input1.Close, input2.Close, input1.Time, input1.IsNew);
    }

    /// <summary>
    /// Handles error calculations and invalid input values.
    /// </summary>
    /// <param name="value">The primary input value to check.</param>
    /// <param name="time">The timestamp of the input.</param>
    /// <param name="isNew">Indicates if the input is new.</param>
    /// <returns>A TValue object with the calculated or last valid value.</returns>
    /// <remarks>
    /// This method checks for NaN or infinity in the input value. If an invalid value is detected,
    /// it returns the last valid value. Otherwise, it proceeds with the calculation.
    /// </remarks>
    protected virtual TValue HandleErrorCalculations(double value, DateTime time, bool isNew)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return Process(new TValue(time, GetLastValid(), isNew, this.IsHot));
        }
        this.Value = Calculation();
        return Process(new TValue(Time: time, Value: this.Value, IsNew: isNew, IsHot: this.IsHot));
    }

    /// <summary>
    /// Handles error calculations for inputs with two values.
    /// </summary>
    /// <param name="value1">The first input value to check.</param>
    /// <param name="value2">The second input value to check.</param>
    /// <param name="time">The timestamp of the input.</param>
    /// <param name="isNew">Indicates if the input is new.</param>
    /// <returns>A TValue object with the calculated or last valid value.</returns>
    /// <remarks>
    /// This method checks for NaN or infinity in both input values. If any invalid value is detected,
    /// it returns the last valid value. Otherwise, it proceeds with the calculation.
    /// </remarks>
    protected virtual TValue HandleErrorCalculations(double value1, double value2, DateTime time, bool isNew)
    {
        if (double.IsNaN(value1) || double.IsInfinity(value1) ||
            double.IsNaN(value2) || double.IsInfinity(value2))
        {
            return Process(new TValue(time, GetLastValid(), isNew, this.IsHot));
        }
        this.Value = Calculation();
        return Process(new TValue(Time: time, Value: this.Value, IsNew: isNew, IsHot: this.IsHot));
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
