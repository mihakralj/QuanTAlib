# Momentum Indicators Test Implementation Plan

> **Objective:** Bring all 13 momentum indicators to full compliance with testprotocol.md

## Executive Summary

- **Total Missing Tests:** ~72 tests across 12 indicators
- **Estimated Effort:** 4-6 hours
- **Priority:** Start with MACD (most deficient), end with VEL (closest to compliant)

---

## Phase 1: Critical Deficiencies (MACD, BOP)

### 1.1 MACD - Add 10 Tests

**File:** `lib/momentum/macd/Macd.Tests.cs`

```csharp
// ADD THESE TESTS:

[Fact]
public void Constructor_InvalidParameters_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => new Macd(0, 26, 9));
    Assert.Throws<ArgumentException>(() => new Macd(12, 0, 9));
    Assert.Throws<ArgumentException>(() => new Macd(12, 26, 0));
    Assert.Throws<ArgumentException>(() => new Macd(26, 12, 9)); // fast >= slow
}

[Fact]
public void Calc_IsNew_AcceptsParameter()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 49; i++)
        macd.Update(series.Close[i], isNew: true);
    
    var val1 = macd.Update(series.Close[49], isNew: true);
    var val2 = macd.Update(new TValue(DateTime.UtcNow, series.Close[49].Value + 1), isNew: true);
    
    Assert.NotEqual(val1.Value, val2.Value);
}

[Fact]
public void Calc_IsNew_False_UpdatesValue()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 49; i++)
        macd.Update(series.Close[i]);
    
    var val1 = macd.Update(series.Close[49], isNew: true);
    var val2 = macd.Update(new TValue(series.Close[49].Time, series.Close[49].Value + 5), isNew: false);
    
    Assert.Equal(val1.Time, val2.Time);
    Assert.NotEqual(val1.Value, val2.Value);
}

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        macd.Update(series.Close[i]);
    
    var originalValue = macd.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TValue(series.Close[49].Time, series.Close[49].Value + m);
        macd.Update(modified, isNew: false);
    }
    
    var restored = macd.Update(series.Close[49], isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void Reset_ClearsState()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < series.Count; i++)
        macd.Update(series.Close[i]);
    
    macd.Reset();
    
    Assert.Equal(0, macd.Last.Value);
    Assert.False(macd.IsHot);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    Assert.False(macd.IsHot);
    
    for (int i = 0; i < series.Count; i++)
    {
        macd.Update(series.Close[i]);
        if (i >= 40) break; // Should be hot by warmup
    }
    
    Assert.True(macd.IsHot);
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 40; i++)
        macd.Update(series.Close[i]);
    
    var result = macd.Update(new TValue(DateTime.UtcNow, double.NaN));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var macd = new Macd(12, 26, 9);
    var gbm = new GBM();
    var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 40; i++)
        macd.Update(series.Close[i]);
    
    var result = macd.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    var gbm = new GBM(seed: 123);
    var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchMacd = new Macd(12, 26, 9);
    var batchResult = batchMacd.Update(series.Close);
    double expected = batchResult.Last.Value;
    
    // 2. Span Mode
    var spanOutput = new double[series.Count];
    Macd.Calculate(series.Close.Values, spanOutput, 12, 26);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode
    var streamMacd = new Macd(12, 26, 9);
    for (int i = 0; i < series.Count; i++)
        streamMacd.Update(series.Close[i]);
    double streamResult = streamMacd.Last.Value;
    
    // 4. Eventing Mode
    var pubSource = new TSeries();
    var eventMacd = new Macd(pubSource, 12, 26, 9);
    for (int i = 0; i < series.Count; i++)
        pubSource.Add(series.Close[i]);
    double eventResult = eventMacd.Last.Value;
    
    Assert.Equal(expected, spanResult, 9);
    Assert.Equal(expected, streamResult, 9);
    Assert.Equal(expected, eventResult, 9);
}

[Fact]
public void SpanBatch_ValidatesInput()
{
    double[] source = [1, 2, 3, 4, 5];
    double[] output = new double[5];
    double[] wrongSize = new double[3];
    
    Assert.Throws<ArgumentException>(() => Macd.Calculate(source, wrongSize, 12, 26));
    Assert.Throws<ArgumentException>(() => Macd.Calculate(source, output, 0, 26));
    Assert.Throws<ArgumentException>(() => Macd.Calculate(source, output, 12, 0));
}
```

### 1.2 BOP - Add 9 Tests

**File:** `lib/momentum/bop/Bop.Tests.cs`

```csharp
// ADD THESE TESTS:

[Fact]
public void Calc_IsNew_AcceptsParameter()
{
    var bop = new Bop();
    var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    var bar2 = new TBar(DateTime.UtcNow, 15, 25, 10, 20, 100);
    
    bop.Update(bar1, isNew: true);
    var val1 = bop.Last.Value;
    
    bop.Update(bar2, isNew: true);
    var val2 = bop.Last.Value;
    
    Assert.NotEqual(val1, val2);
}

[Fact]
public void Calc_IsNew_False_UpdatesValue()
{
    var bop = new Bop();
    var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    var bar2 = new TBar(DateTime.UtcNow, 10, 25, 5, 20, 100);
    
    var val1 = bop.Update(bar1, isNew: true);
    var val2 = bop.Update(bar2, isNew: false);
    
    Assert.Equal(val1.Time, val2.Time);
    Assert.NotEqual(val1.Value, val2.Value);
}

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var bop = new Bop();
    var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    
    var originalValue = bop.Update(bar, isNew: true);
    
    for (int i = 0; i < 5; i++)
    {
        var modified = new TBar(bar.Time, bar.Open, bar.High + i, bar.Low, bar.Close, bar.Volume);
        bop.Update(modified, isNew: false);
    }
    
    var restored = bop.Update(bar, isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void Reset_ClearsState()
{
    var bop = new Bop();
    var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    
    bop.Update(bar);
    bop.Reset();
    
    Assert.Equal(0, bop.Last.Value);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var bop = new Bop();
    
    Assert.False(bop.IsHot);
    
    var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    bop.Update(bar);
    
    Assert.True(bop.IsHot); // BOP is hot immediately (no warmup needed)
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var bop = new Bop();
    var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    var barNaN = new TBar(DateTime.UtcNow, double.NaN, 20, 5, 15, 100);
    
    bop.Update(bar1);
    var result = bop.Update(barNaN);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var bop = new Bop();
    var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
    var barInf = new TBar(DateTime.UtcNow, double.PositiveInfinity, 20, 5, 15, 100);
    
    bop.Update(bar1);
    var result = bop.Update(barInf);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    var gbm = new GBM(seed: 123);
    var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = Bop.Batch(bars);
    double expected = batchResult.Last.Value;
    
    // 2. Span Mode
    var spanOutput = new double[bars.Count];
    Bop.Calculate(bars.Open.Values, bars.High.Values, bars.Low.Values, bars.Close.Values, spanOutput);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode
    var streamBop = new Bop();
    for (int i = 0; i < bars.Count; i++)
        streamBop.Update(bars[i]);
    double streamResult = streamBop.Last.Value;
    
    Assert.Equal(expected, spanResult, 9);
    Assert.Equal(expected, streamResult, 9);
}

[Fact]
public void SpanBatch_ValidatesInput()
{
    double[] open = [1, 2, 3];
    double[] high = [2, 3, 4];
    double[] low = [0, 1, 2];
    double[] close = [1.5, 2.5, 3.5];
    double[] output = new double[3];
    double[] wrongSize = new double[2];
    
    Assert.Throws<ArgumentException>(() => Bop.Calculate(open, high, low, close, wrongSize));
}
```

---

## Phase 2: Medium Deficiencies (DMX, CFB)

### 2.1 DMX - Add 7 Tests

**File:** `lib/momentum/dmx/Dmx.Tests.cs`

```csharp
// ADD THESE TESTS:

[Fact]
public void Constructor_InvalidParameters_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => new Dmx(0));
    Assert.Throws<ArgumentException>(() => new Dmx(-1));
}

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var dmx = new Dmx(14);
    var gbm = new GBM();
    var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        dmx.Update(bars[i]);
    
    var originalValue = dmx.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
        dmx.Update(modified, isNew: false);
    }
    
    var restored = dmx.Update(bars[49], isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var dmx = new Dmx(14);
    var gbm = new GBM();
    var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    Assert.False(dmx.IsHot);
    
    for (int i = 0; i < bars.Count; i++)
    {
        dmx.Update(bars[i]);
        if (dmx.IsHot) break;
    }
    
    Assert.True(dmx.IsHot);
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var dmx = new Dmx(14);
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 30; i++)
        dmx.Update(bars[i]);
    
    var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
    var result = dmx.Update(nanBar);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var dmx = new Dmx(14);
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 30; i++)
        dmx.Update(bars[i]);
    
    var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
    var result = dmx.Update(infBar);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    var gbm = new GBM(seed: 123);
    var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = Dmx.Batch(bars, 14);
    double expected = batchResult.Last.Value;
    
    // 2. Streaming Mode
    var streamDmx = new Dmx(14);
    for (int i = 0; i < bars.Count; i++)
        streamDmx.Update(bars[i]);
    double streamResult = streamDmx.Last.Value;
    
    Assert.Equal(expected, streamResult, 9);
}

[Fact]
public void SpanBatch_ValidatesInput()
{
    // Add if DMX has span API
}
```

### 2.2 CFB - Add 5 Tests

**File:** `lib/momentum/cfb/Cfb.Tests.cs`

```csharp
// ADD THESE TESTS:

[Fact]
public void Constructor_InvalidParameters_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => new Cfb(Array.Empty<int>()));
    Assert.Throws<ArgumentException>(() => new Cfb(new[] { 0, 10 }));
    Assert.Throws<ArgumentException>(() => new Cfb(new[] { -1, 10 }));
}

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var cfb = new Cfb();
    var gbm = new GBM();
    var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        cfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
    
    var originalValue = cfb.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TValue(bars.Close.Times[49], bars.Close.Values[49] + m);
        cfb.Update(modified, isNew: false);
    }
    
    var restored = cfb.Update(new TValue(bars.Close.Times[49], bars.Close.Values[49]), isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var cfb = new Cfb(new[] { 5, 10 });
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < bars.Count; i++)
    {
        cfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
        if (cfb.IsHot) break;
    }
    
    Assert.True(cfb.IsHot);
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var cfb = new Cfb();
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 30; i++)
        cfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
    
    var result = cfb.Update(new TValue(DateTime.UtcNow, double.NaN));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var cfb = new Cfb();
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 30; i++)
        cfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
    
    var result = cfb.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    var gbm = new GBM(seed: 123);
    var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = Cfb.Batch(bars.Close);
    double expected = batchResult.Last.Value;
    
    // 2. Span Mode
    var spanOutput = new double[bars.Count];
    Cfb.Batch(bars.Close.Values.ToArray(), spanOutput);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode
    var streamCfb = new Cfb();
    for (int i = 0; i < bars.Count; i++)
        streamCfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
    double streamResult = streamCfb.Last.Value;
    
    Assert.Equal(expected, spanResult, 9);
    Assert.Equal(expected, streamResult, 9);
}
```

---

## Phase 3: Standard Deficiencies (ADX, ADXR, AO, APO, Aroon, AroonOsc)

These 6 indicators all have the same pattern of missing tests. Create a template:

### Template for TBar-based Indicators (ADX, ADXR, AO, Aroon, AroonOsc)

```csharp
// ADD THESE 6 TESTS TO EACH:

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var indicator = new [IndicatorName](period);
    var gbm = new GBM();
    var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        indicator.Update(bars[i]);
    
    var originalValue = indicator.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
        indicator.Update(modified, isNew: false);
    }
    
    var restored = indicator.Update(bars[49], isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var indicator = new [IndicatorName](period);
    var gbm = new GBM();
    var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    Assert.False(indicator.IsHot);
    
    for (int i = 0; i < bars.Count; i++)
    {
        indicator.Update(bars[i]);
        if (indicator.IsHot) break;
    }
    
    Assert.True(indicator.IsHot);
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var indicator = new [IndicatorName](period);
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 40; i++)
        indicator.Update(bars[i]);
    
    var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
    var result = indicator.Update(nanBar);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var indicator = new [IndicatorName](period);
    var gbm = new GBM();
    var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 40; i++)
        indicator.Update(bars[i]);
    
    var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
    var result = indicator.Update(infBar);
    
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    var gbm = new GBM(seed: 123);
    var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = [IndicatorName].Batch(bars, period);
    double expected = batchResult.Last.Value;
    
    // 2. Streaming Mode
    var streamIndicator = new [IndicatorName](period);
    for (int i = 0; i < bars.Count; i++)
        streamIndicator.Update(bars[i]);
    double streamResult = streamIndicator.Last.Value;
    
    Assert.Equal(expected, streamResult, 9);
}

[Fact]
public void SpanBatch_ValidatesInput()
{
    // Implement if indicator has Span API
}
```

### Template for TValue-based Indicator (APO)

Similar pattern but uses `series.Close[i]` instead of `bars[i]`.

---

## Phase 4: Minor Deficiencies (RSX, VEL)

### 4.1 RSX - Add 4 Tests

```csharp
[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var rsx = new Rsx(14);
    var gbm = new GBM();
    var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        rsx.Update(new TValue(series.Close.Times[i], series.Close.Values[i]));
    
    var originalValue = rsx.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TValue(series.Close.Times[49], series.Close.Values[49] + m);
        rsx.Update(modified, isNew: false);
    }
    
    var restored = rsx.Update(new TValue(series.Close.Times[49], series.Close.Values[49]), isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var rsx = new Rsx(14);
    var gbm = new GBM();
    var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    Assert.False(rsx.IsHot);
    
    for (int i = 0; i < series.Count; i++)
    {
        rsx.Update(new TValue(series.Close.Times[i], series.Close.Values[i]));
        if (rsx.IsHot) break;
    }
    
    Assert.True(rsx.IsHot);
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var rsx = new Rsx(14);
    rsx.Update(new TValue(DateTime.UtcNow, 100));
    var result = rsx.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
    
    Assert.False(double.IsInfinity(result.Value));
    Assert.InRange(result.Value, 0, 100);
}

[Fact]
public void AllModes_ProduceSameResult()
{
    int period = 14;
    var gbm = new GBM(seed: 123);
    var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = Rsx.Batch(series.Close, period);
    double expected = batchResult.Last.Value;
    
    // 2. Span Mode
    var spanOutput = new double[series.Count];
    Rsx.Batch(series.Close.Values.ToArray(), spanOutput, period);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode
    var streamRsx = new Rsx(period);
    for (int i = 0; i < series.Count; i++)
        streamRsx.Update(new TValue(series.Close.Times[i], series.Close.Values[i]));
    double streamResult = streamRsx.Last.Value;
    
    // 4. Eventing Mode
    var pubSource = new TSeries();
    var eventRsx = new Rsx(pubSource, period);
    for (int i = 0; i < series.Count; i++)
        pubSource.Add(new TValue(series.Close.Times[i], series.Close.Values[i]));
    double eventResult = eventRsx.Last.Value;
    
    Assert.Equal(expected, spanResult, 9);
    Assert.Equal(expected, streamResult, 9);
    Assert.Equal(expected, eventResult, 9);
}
```

### 4.2 VEL - Add 4 Tests

```csharp
[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var vel = new Vel(10);
    var gbm = new GBM();
    var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 50; i++)
        vel.Update(series.Close[i]);
    
    var originalValue = vel.Last;
    
    for (int m = 0; m < 5; m++)
    {
        var modified = new TValue(series.Close[49].Time, series.Close[49].Value + m);
        vel.Update(modified, isNew: false);
    }
    
    var restored = vel.Update(series.Close[49], isNew: false);
    Assert.Equal(originalValue.Value, restored.Value, 1e-9);
}

[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var vel = new Vel(10);
    var gbm = new GBM();
    var series = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 15; i++)
        vel.Update(series.Close[i]);
    
    var result = vel.Update(new TValue(DateTime.UtcNow, double.NaN));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var vel = new Vel(10);
    var gbm = new GBM();
    var series = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    for (int i = 0; i < 15; i++)
        vel.Update(series.Close[i]);
    
    var result = vel.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
    Assert.True(double.IsFinite(result.Value));
}

[Fact]
public void AllModes_ProduceSameResult()
{
    int period = 10;
    var gbm = new GBM(seed: 123);
    var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    
    // 1. Batch Mode
    var batchResult = Vel.Batch(series.Close, period);
    double expected = batchResult.Last.Value;
    
    // 2. Span Mode
    var spanOutput = new double[series.Count];
    Vel.Batch(series.Close.Values.ToArray().AsSpan(), spanOutput.AsSpan(), period);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode
    var streamVel = new Vel(period);
    for (int i = 0; i < series.Count; i++)
        streamVel.Update(series.Close[i]);
    double streamResult = streamVel.Last.Value;
    
    // 4. Eventing Mode
    var pubSource = new TSeries();
    var eventVel = new Vel(pubSource, period);
    for (int i = 0; i < series.Count; i++)
        pubSource.Add(series.Close[i]);
    double eventResult = eventVel.Last.Value;
    
    Assert.Equal(expected, spanResult, 9);
    Assert.Equal(expected, streamResult, 9);
    Assert.Equal(expected, eventResult, 9);
}

[Fact]
public void SpanBatch_ValidatesInput()
{
    double[] source = [1, 2, 3, 4, 5];
    double[] output = new double[5];
    double[] wrongSize = new double[3];
    
    Assert.Throws<ArgumentException>(() => Vel.Batch(source.AsSpan(), wrongSize.AsSpan(), 3));
    Assert.Throws<ArgumentException>(() => Vel.Batch(source.AsSpan(), output.AsSpan(), 0));
    Assert.Throws<ArgumentException>(() => Vel.Batch(source.AsSpan(), output.AsSpan(), -1));
}
```

---

## Implementation Checklist

### Phase 1 (Priority: Critical)
- [ ] MACD.Tests.cs - Add 10 tests
- [ ] BOP.Tests.cs - Add 9 tests

### Phase 2 (Priority: High)
- [ ] DMX.Tests.cs - Add 7 tests
- [ ] CFB.Tests.cs - Add 5 tests

### Phase 3 (Priority: Medium)
- [ ] ADX.Tests.cs - Add 6 tests
- [ ] ADXR.Tests.cs - Add 6 tests
- [ ] AO.Tests.cs - Add 6 tests
- [ ] APO.Tests.cs - Add 6 tests
- [ ] Aroon.Tests.cs - Add 6 tests
- [ ] AroonOsc.Tests.cs - Add 6 tests

### Phase 4 (Priority: Low)
- [ ] RSX.Tests.cs - Add 4 tests
- [ ] VEL.Tests.cs - Add 4 tests

---

## Verification Steps

After implementing all tests:

1. Run all tests: `dotnet test lib/QuanTAlib.Tests.csproj`
2. Verify no regressions in existing tests
3. Check test coverage meets targets
4. Update docs/validation.md with compliance status
