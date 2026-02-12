# Detrended Price Oscillator (DPO)

## Overview

The **Detrended Price Oscillator (DPO)** removes the trend component from price data by displacing a Simple Moving Average (SMA), isolating short-term price cycles. Unlike most oscillators, DPO is not aligned to the latest price—it references a past SMA value to filter out long-term trends.

## Formula

```
displacement = floor(period / 2) + 1
DPO = price − SMA(period)[displacement bars ago]
```

Where:
- **period** — SMA lookback window (default: 20)
- **displacement** — number of bars the SMA is shifted backward
- **SMA** — Simple Moving Average of the source series

## Architecture

```
Source ──→ RingBuffer(period) ──→ SMA ──→ RingBuffer(displacement+1) ──→ DPO
           [running sum]         [O(1)]    [stores SMA history]
```

### Streaming (O(1) per bar)

| Component | Role |
|-----------|------|
| `_smaBuffer` | `RingBuffer(period)` — maintains running sum for O(1) SMA via `Sum / period` |
| `_smaHistory` | `RingBuffer(displacement + 1)` — stores past SMA values; `.Oldest` gives the displaced SMA |

### Bar Correction

Uses `Snapshot()` / `Restore()` on both RingBuffers for intra-bar updates (`isNew = false`).

### Warmup

`WarmupPeriod = period + displacement` — need `period` bars to compute the first SMA, then `displacement` more bars before the displaced SMA is available.

## Performance Profile

| Metric | Value |
|--------|-------|
| Time complexity | O(1) per bar (streaming) |
| Space complexity | O(period + displacement) |
| Allocations | Zero per update |
| NaN handling | Last valid value substitution |
| SIMD | Not applicable (displacement dependency) |

## Usage

```csharp
// Streaming
var dpo = new Dpo(period: 20);
TValue result = dpo.Update(new TValue(time, price));

// Event-based
var source = new TSeries();
var dpo = new Dpo(source, period: 20);

// Batch
TSeries results = Dpo.Batch(source, period: 20);

// Span
Dpo.Batch(sourceSpan, outputSpan, period: 20);
```

## Interpretation

* **Zero Line Crossovers:**
  - DPO crosses above zero: Price is above the displaced moving average (short-term bullish)
  - DPO crosses below zero: Price is below the displaced moving average (short-term bearish)

* **Cycle Identification:**
  - DPO peaks and troughs correspond to short-term price cycles
  - Distance between peaks estimates the dominant cycle period
  - Works best when the dominant cycle length approximates the DPO period

* **Overbought/Oversold:**
  - Extreme DPO values suggest price has deviated significantly from its trend
  - No fixed bounds; context-dependent interpretation

* **Divergence:**
  - Bullish: Price makes lower lows while DPO makes higher lows
  - Bearish: Price makes higher highs while DPO makes lower highs

## Validation

Cross-validated against:
- **Tulip Indicators** (`dpo`) — exact match within 1e-9 tolerance
- **Manual SMA computation** — independent verification of displaced SMA algorithm

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `period` | int | 20 | > 0 | SMA lookback period |

## References

- William Blau, *Momentum, Direction, and Divergence*, 1995
- Thomas Dorsey, *Point and Figure Charting*, 2007
- PineScript reference: `dpo.pine`
