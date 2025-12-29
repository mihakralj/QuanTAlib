# Integration Guides

QuanTAlib is designed to be platform-agnostic. It can be integrated into any .NET environment.

## Quantower

Quantower allows custom indicators via C#.

1. **Reference the DLL**:
    - Build QuanTAlib or download the NuGet package.
    - In your Quantower indicator project, add a reference to `QuanTAlib.dll`.

2. **Wrapper Class**:
    - Create a class that inherits from `Indicator`.
    - Instantiate the QuanTAlib indicator in `OnInit`.
    - Call `Update` in `OnUpdate`.

```csharp
using Quantower.API.Indicators;
using QuanTAlib;

public class MySmaIndicator : Indicator
{
    private Sma _sma;

    [InputParameter("Period", 10, 1000, 1, 0)]
    public int Period = 14;

    public override void OnInit()
    {
        _sma = new Sma(Period);
        AddLineSeries("SMA", Color.Yellow, LineStyle.Solid, 2);
    }

    public override void OnUpdate(UpdateArgs args)
    {
        // Get price from Quantower
        double price = ClosePrice;

        // Update QuanTAlib
        // Note: Quantower handles bar updates, so a check is performed to determine whether this is a new bar or an update
        bool isNew = args.Reason == UpdateReason.NewBar;
        var result = _sma.Update(new TValue(DateTime.UtcNow, price), isNew);

        // Set value to Quantower series
        SetValue(result.Value);
    }
}
```

## NinjaTrader 8

NinjaTrader 8 uses .NET Framework 4.8, but can interop with .NET Standard libraries.

1. **Copy DLL**: Place `QuanTAlib.dll` in `Documents\NinjaTrader 8\bin\Custom`.
2. **Add Reference**: In NinjaScript Editor, right-click > References > Add `QuanTAlib.dll`.

```csharp
protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        Name = "QuanTAlib SMA";
        // ...
    }
    else if (State == State.DataLoaded)
    {
        _sma = new QuanTAlib.Sma(Period);
    }
}

protected override void OnBarUpdate()
{
    // NinjaTrader calls OnBarUpdate for every tick (if Calculate = OnEachTick)
    // or once per bar (if Calculate = OnBarClose)

    bool isNew = IsFirstTickOfBar; // Logic depends on Calculate mode
    var result = _sma.Update(new TValue(Time[0], Close[0]), isNew);

    Value[0] = result.Value;
}
```

## QuantConnect (LEAN)

LEAN supports custom libraries.

1. **NuGet**: Add `QuanTAlib` to your `config.json` or project file.
2. **Usage**: Use inside `OnData`.

```csharp
public class MyAlgorithm : QCAlgorithm
{
    private Sma _mySma;

    public override void Initialize()
    {
        _mySma = new Sma(14);
    }

    public override void OnData(Slice data)
    {
        if (data.Bars.ContainsKey("SPY"))
        {
            var bar = data.Bars["SPY"];
            var result = _mySma.Update(new TValue(bar.EndTime, (double)bar.Close));

            if (_mySma.IsHot)
            {
                Plot("Indicators", "SMA", result.Value);
            }
        }
    }
}
```

## Custom Platform Integration

For proprietary trading engines, the **Streaming Mode** is usually the best fit.

### Key Considerations

1. **Time Handling**: QuanTAlib uses `DateTime.UtcNow`. Ensure your platform provides UTC timestamps or convert them.
2. **Double Precision**: All calculations use `double`. If your platform uses `decimal`, cast to `double` for input and back to `decimal` for output.
3. **State Management**: Persist the indicator instance for the lifetime of the symbol/strategy. Do not recreate the indicator on every tick.
4. **Concurrency**: `Update` is not thread-safe for the same instance. If processing multiple symbols in parallel, use separate indicator instances for each symbol.
