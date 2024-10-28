using System.Runtime.CompilerServices;
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
    public System.DateTime Time { get; set; }
    public double Value { get; set; }
    public bool IsNew { get; set; }
    public bool IsHot { get; set; }
    public TValue Input { get; set; }
    public TValue Input2 { get; set; }
    public TBar BarInput { get; set; }
    public TBar BarInput2 { get; set; }
    public string Name { get; set; } = "";
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
    /// Checks if the input value is valid (not NaN or Infinity).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsValidValue(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Creates a new TValue with the current state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TValue CreateTValue(System.DateTime time, double value, bool isNew, bool isHot = false)
    {
        return new TValue(time, value, isNew, isHot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source, in ValueEventArgs args) => Calc(args.Tick);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source1, object source2, in ValueEventArgs args1, in ValueEventArgs args2) =>
        Calc(args1.Tick, args2.Tick);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sub(object source, in TBarEventArgs args) => Calc(args.Bar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Init()
    {
        _index = 0;
        _lastValidValue = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(TValue input)
    {
        Input = input;
        Input2 = CreateTValue(input.Time, double.NaN, input.IsNew, input.IsHot);
        return Process(input.Value, input.Time, input.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(double value, bool isNew)
    {
        Input = CreateTValue(Time, value, isNew);
        Input2 = CreateTValue(Time, double.NaN, false);
        return Process(Input.Value, Input.Time, Input.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(TBar barInput)
    {
        BarInput = barInput;
        return Process(barInput.Close, barInput.Time, barInput.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(TValue input1, TValue input2)
    {
        Input = input1;
        Input2 = input2;
        return Process(input1.Value, input2.Value, input1.Time, input1.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(TBar input1, TBar input2)
    {
        BarInput = input1;
        BarInput2 = input2;
        return Process(input1.Close, input2.Close, input1.Time, input1.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual TValue Calc(double value1, double value2)
    {
        var now = System.DateTime.Now;
        Input = CreateTValue(now, value1, true, true);
        Input2 = CreateTValue(now, value2, true, true);
        return Process(value1, value2, now, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual TValue Process(double value, System.DateTime time, bool isNew)
    {
        if (!IsValidValue(value))
        {
            return Process(CreateTValue(time, GetLastValid(), isNew, IsHot));
        }
        Value = Calculation();
        return Process(CreateTValue(time, Value, isNew, IsHot));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual TValue Process(double value1, double value2, System.DateTime time, bool isNew)
    {
        if (!IsValidValue(value1) || !IsValidValue(value2))
        {
            return Process(CreateTValue(time, GetLastValid(), isNew, IsHot));
        }
        Value = Calculation();
        return Process(CreateTValue(time, Value, isNew, IsHot));
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual double GetLastValid()
    {
        return Value;
    }

    protected abstract void ManageState(bool isNew);

    protected abstract double Calculation();
}
