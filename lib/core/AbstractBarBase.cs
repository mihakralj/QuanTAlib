using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Provides a base implementation for financial indicators that work with bar data in the QuanTAlib library.
/// </summary>
/// <remarks>
/// This abstract class implements the iTValue interface and defines common properties
/// and methods used by inheriting indicator types. It handles the basic flow of
/// receiving bar data, performing calculations, and publishing results.
/// </remarks>
public abstract class AbstractBarBase : ITValue
{
    public System.DateTime Time { get; set; }
    public double Value { get; set; }
    public bool IsNew { get; set; }
    public bool IsHot { get; set; }
    public TBar Input { get; set; }
    public string Name { get; set; } = "";
    public int WarmupPeriod { get; set; }

    public TValue Tick => new(Time, Value, IsNew, IsHot);

    public event ValueSignal Pub = delegate { };

    protected int _index;
    protected double _lastValidValue;

    protected AbstractBarBase()
    {
        // Add parameters into constructor if needed
    }

    /// <summary>
    /// Subscribes to bar data updates.
    /// </summary>
    /// <param name="source">The source of the bar data.</param>
    /// <param name="args">The event arguments containing the bar data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source, in TBarEventArgs args) => Calc(args.Bar);

    /// <summary>
    /// Initializes the indicator's state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Init()
    {
        _index = 0;
        _lastValidValue = 0;
    }

    /// <summary>
    /// Checks if the input value is valid (not NaN or Infinity).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is valid, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsValidValue(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Creates a new TValue with the current state.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>A new TValue instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TValue CreateTValue(double value)
    {
        return new TValue(Time: Input.Time, Value: value, IsNew: Input.IsNew, IsHot: IsHot);
    }

    /// <summary>
    /// Calculates the indicator value based on the input bar.
    /// </summary>
    /// <param name="input">The input bar data.</param>
    /// <returns>A TValue containing the calculated result.</returns>
    public virtual TValue Calc(TBar input)
    {
        Input = input;
        if (!IsValidValue(input.Close))
        {
            return Process(CreateTValue(GetLastValid()));
        }

        Value = Calculation();
        return Process(CreateTValue(Value));
    }

    /// <summary>
    /// Retrieves the last valid calculated value.
    /// </summary>
    /// <returns>The last valid value of the indicator.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual double GetLastValid()
    {
        return Value;
    }

    /// <summary>
    /// Manages the state of the indicator based on whether a new bar is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new bar.</param>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual TValue Process(TValue value)
    {
        Time = value.Time;
        Value = value.Value;
        IsNew = value.IsNew;
        IsHot = value.IsHot;
        Pub?.Invoke(this, new ValueEventArgs(value));
        return value;
    }
}
