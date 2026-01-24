# Integration Guides

QuanTAlib is platform-agnostic. Any .NET environment that can reference a DLL can use it. The complexity lies not in the library but in understanding each platform's quirks.

## Quantower

Quantower accepts custom indicators written in C#. Integration follows a wrapper pattern.

### Setup

1. Build QuanTAlib or grab the NuGet package
2. Add reference to `QuanTAlib.dll` in the Quantower indicator project
3. Create wrapper class inheriting from `Indicator`

### Example: SMA Indicator

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
        double price = ClosePrice;

        // Quantower handles bar lifecycle; check UpdateReason
        bool isNew = args.Reason == UpdateReason.NewBar;
        var result = _sma.Update(new TValue(DateTime.UtcNow, price), isNew);

        SetValue(result.Value);
    }
}
```

### Available Quantower Bundles

Pre-built adapters exist for common indicators:

| Bundle | Indicators | Notes |
| :----- | :--------- | :---- |
| Trends (IIR) | HemaIndicator, ZlemaIndicator, EmaIndicator, etc. | Exponential family |
| Trends (FIR) | SmaIndicator, WmaIndicator, HmaIndicator, etc. | Finite response family |
| Volatility | AtrIndicator, AdrIndicator | Range-based volatility |
| Dynamics | AdxIndicator, SuperTrendIndicator | Trend strength |

### Quantower Gotchas

**UpdateReason matters.** Quantower calls `OnUpdate` for both new bars and intra-bar ticks. The `args.Reason` check determines `isNew` flag behavior. Getting this wrong causes state corruption that manifests as mysteriously wrong indicator values.

**Historical data loads first.** Quantower calls `OnUpdate` repeatedly during historical load before live data arrives. The indicator warms up during this phase.

## NinjaTrader 8

NinjaTrader 8 runs on .NET Framework 4.8. QuanTAlib targets .NET Standard, enabling interop.

### Setup

1. Copy `QuanTAlib.dll` to `Documents\NinjaTrader 8\bin\Custom`
2. In NinjaScript Editor: right-click ’ References ’ Add `QuanTAlib.dll`

### Example: SMA Indicator

```csharp
private QuanTAlib.Sma _sma;

[Range(1, int.MaxValue)]
[NinjaScriptProperty]
public int Period { get; set; } = 14;

protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        Name = "QuanTAlib SMA";
        Calculate = Calculate.OnBarClose;
    }
    else if (State == State.DataLoaded)
    {
        _sma = new QuanTAlib.Sma(Period);
    }
}

protected override void OnBarUpdate()
{
    // isNew depends on Calculate mode
    // OnBarClose: every call is a new bar
    // OnEachTick: use IsFirstTickOfBar
    bool isNew = Calculate == Calculate.OnBarClose || IsFirstTickOfBar;

    var result = _sma.Update(new TValue(Time[0], Close[0]), isNew);
    Value[0] = result.Value;
}
```

### NinjaTrader Gotchas

**Calculate mode affects isNew logic.** With `Calculate.OnBarClose`, every `OnBarUpdate` call represents a completed bar. With `Calculate.OnEachTick`, only the first tick of each bar should use `isNew = true`. Mixing these concepts produces indicators that work in backtest but fail live.

**Historical vs real-time.** NinjaTrader processes historical bars differently from real-time bars. The `State` property indicates the current phase. Indicator warmup should complete during historical processing.

## QuantConnect (LEAN)

LEAN supports custom libraries through NuGet integration.

### Setup

1. Add `QuanTAlib` to project dependencies
2. Instantiate indicators in `Initialize()`
3. Update in `OnData()`

### Example: Algorithm with SMA

```csharp
public class MyAlgorithm : QCAlgorithm
{
    private Sma _mySma;
    private Symbol _symbol;

    public override void Initialize()
    {
        SetStartDate(2020, 1, 1);
        SetEndDate(2023, 12, 31);
        SetCash(100000);

        _symbol = AddEquity("SPY", Resolution.Daily).Symbol;
        _mySma = new Sma(14);
    }

    public override void OnData(Slice data)
    {
        if (!data.Bars.ContainsKey(_symbol))
            return;

        var bar = data.Bars[_symbol];
        var result = _mySma.Update(new TValue(bar.EndTime, (double)bar.Close));

        if (_mySma.IsHot)
        {
            Plot("Indicators", "SMA", result.Value);

            // Trading logic here
            if (!Portfolio[_symbol].Invested && result.Value < (double)bar.Close)
            {
                SetHoldings(_symbol, 0.5);
            }
        }
    }
}
```

### LEAN Gotchas

**Decimal to double conversion.** LEAN uses `decimal` for prices; QuanTAlib uses `double`. Cast on input, cast back on output if needed. The precision difference rarely matters for indicator calculations.

**Resolution affects bar timing.** Daily bars have different `EndTime` semantics than minute bars. UTC timestamps prevent timezone confusion.

## Custom Platform Integration

For proprietary trading engines, Streaming Mode fits most use cases.

### Integration Checklist

| Consideration | Requirement | Consequence of Ignoring |
| :------------ | :---------- | :---------------------- |
| **Time handling** | UTC timestamps | Timezone bugs in historical analysis |
| **Numeric precision** | `double` input/output | Cast from/to `decimal` if platform uses it |
| **State persistence** | One instance per symbol | Recreating indicators loses warmup state |
| **Thread safety** | Separate instances per thread | Concurrent access corrupts internal state |
| **Bar correction** | Proper `isNew` flag usage | State accumulation errors |

### Minimal Integration Pattern

```csharp
public class MyTradingEngine
{
    // One indicator instance per symbol, persisted for session lifetime
    private readonly Dictionary<string, Sma> _indicators = new();

    public void OnSymbolAdded(string symbol, int smaPeriod)
    {
        _indicators[symbol] = new Sma(smaPeriod);
    }

    public void OnTick(string symbol, DateTime time, double price, bool isNewBar)
    {
        if (!_indicators.TryGetValue(symbol, out var sma))
            return;

        var result = sma.Update(new TValue(time, price), isNewBar);

        if (sma.IsHot)
        {
            // Use result.Value for trading logic
            ProcessSignal(symbol, result.Value, price);
        }
    }

    public void OnSymbolRemoved(string symbol)
    {
        _indicators.Remove(symbol);
    }
}
```

### The isNew Flag: Getting It Right

The `isNew` flag determines whether `Update()` advances to the next bar or corrects the current one. Correct implementation depends on data source semantics:

| Data Source Type | isNew = true When | isNew = false When |
| :--------------- | :---------------- | :----------------- |
| Bar-based feed | New bar arrives | Never (each bar final) |
| Tick-based, bar aggregation | First tick after bar close | Subsequent ticks within bar |
| Streaming with corrections | Timestamp advances | Same timestamp, updated price |

**Testing approach:** Feed identical data through the indicator in streaming mode (tick by tick with correct `isNew` flags) and batch mode (complete series at once). Compare final values. Mismatch indicates `isNew` flag logic error.