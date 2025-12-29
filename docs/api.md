# QuanTAlib API Documentation

QuanTAlib employs a **Tri-Modal Architecture** to unify high-performance batch processing with low-latency streaming updates. This design segregates the indicator lifecycle into three distinct mathematical modes, solving the "two-world problem" of quantitative finance (backtesting vs. live trading).

All indicators inherit from `AbstractBase` and implement the `ITValuePublisher` interface, ensuring a consistent API across the entire library.

> **Note:** The examples below use `Sma` (Simple Moving Average), but this pattern applies to all indicators in the library.

## 1. Core Interface (`AbstractBase`)

Every indicator exposes the following core properties and methods:

### Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Name` | `string` | Descriptive name (e.g., `"Sma(14)"`). |
| `Last` | `TValue` | The most recent calculated value (Time + Value). |
| `IsHot` | `bool` | `true` if the indicator has processed enough data to be valid. |
| `WarmupPeriod` | `int` | Number of samples required before `IsHot` becomes true. |
| `Pub` | `event` | Event fired whenever a new value is calculated (Reactive). |

### Methods

| Method | Description |
| ------ | ----------- |
| `Update` | Updates the indicator with a new value (Streaming). |
| `Batch` | Static method for high-performance bulk calculation (Batch). |
| `Prime` | Initializes state from history without full processing (Priming). |
| `Reset` | Resets the indicator to its initial state. |

---

## 2. Mode A: Batch (Stateless)

**Purpose:** Backtesting, Data Analysis, Optimization
**Method:** `static Batch`

Batch mode provides stateless, SIMD-accelerated processing of historical arrays. It is optimized for maximum throughput and zero heap allocation.

### Span-Based (Zero Allocation)

The most efficient method. Uses SIMD instructions (AVX2/AVX512/Neon) and operates directly on memory spans.

```csharp
// Signature
public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period);

// Usage
double[] prices = ...; // Large dataset
double[] results = new double[prices.Length];

// Fast, in-place calculation (Zero Allocation)
Sma.Batch(prices, results, 14);
```

### TSeries-Based (Convenience)

A wrapper for `TSeries` objects that returns a new series with aligned timestamps.

```csharp
TSeries history = ...;
TSeries sma = Sma.Batch(history, 14);
```

---

## 3. Mode B: Streaming (Stateful)

**Purpose:** Live Trading, Event Processing
**Method:** `Update`

Streaming mode handles real-time data ingestion using O(1) complexity per update. It maintains internal state (circular buffers, running sums) to process ticks with minimal latency.

### Standard Update

Adds a new value and returns the updated result.

```csharp
var indicator = new Sma(14);
TValue result = indicator.Update(new TValue(time, price));
```

### Bar Correction (`isNew`)

Handles intra-bar updates (re-calculation of the current bar) without corrupting state.

```csharp
// New bar opens
indicator.Update(new TValue(t, 100), isNew: true);

// Price updates within the same bar (correction)
indicator.Update(new TValue(t, 101), isNew: false);
indicator.Update(new TValue(t, 102), isNew: false);

// Next bar opens
indicator.Update(new TValue(t+1, 105), isNew: true);
```

### Reactive Chaining

Indicators can subscribe to other `ITValuePublisher` sources (like `TSeries` or other indicators).

```csharp
TSeries source = ...;

// Chain: Source -> SMA(14) -> EMA(5)
var sma = new Sma(source, 14);
var ema = new Ema(sma, 5);

// Updates flow automatically
source.Add(new TValue(time, price));
// sma updates, then ema updates automatically
```

---

## 4. Mode C: Priming (The Bridge)

**Purpose:** Switching from Batch to Streaming
**Method:** `Prime`

Priming mode hydrates a streaming instance using the minimal required tail of historical data. It calculates the intersection of *History Available* and *State Required*, allowing an indicator to become "Hot" without processing the entire history.

```csharp
// Signature
public void Prime(ReadOnlySpan<double> source);

// Usage
var indicator = new Sma(14);
double[] history = ...; // e.g., 100,000 bars

// Efficiently processes only the last 'period' bars needed to fill the buffer
// O(Warmup) initialization instead of O(History)
indicator.Prime(history);

// Indicator is now "Hot" and ready for the next live tick
Console.WriteLine(indicator.IsHot); // true
```

---

## 5. One-Shot Hybrid (`Calculate`)

A high-level helper that combines Batch and Priming modes. It calculates the entire history and returns a "hot" instance ready for immediate real-time updates.

```csharp
// Signature
public static (TSeries Results, Sma Indicator) Calculate(TSeries source, int period);

// Usage
TSeries history = ...;
var (results, indicator) = Sma.Calculate(history, 14);

// 'results' contains the full calculated history (Batch Mode)
// 'indicator' is fully warmed up (Priming Mode) and ready for live ticks
indicator.Update(newTick); // Streaming Mode
```

---

## 6. Validity & Convergence (`IsHot`)

The `IsHot` property indicates whether the indicator has processed enough data to produce mathematically valid results.

### Streaming Context

`IsHot` becomes `true` once the required warmup period is satisfied.

```csharp
var sma = new Sma(10);
// First 9 updates: IsHot = false
// 10th update: IsHot = true
```

### Batch Context

The initial portion of the output contains "cold" values.

**How many values are cold?**

- **Fixed-Window** (SMA, RSI): `WarmupPeriod` (usually `period - 1`).
- **Recursive** (EMA, MACD): Technically infinite, practically `3-4 * period`.

**Checking Validity:**

- **Property:** Use `WarmupPeriod` to determine how many initial values to skip.
- **Process API:** The returned instance's `IsHot` property confirms if the batch was long enough.

---

## Architecture Diagram

```mermaid
graph LR
    H[Historical Data]
    L[Live Data]

    subgraph "Mode A: Batch"
    H -->|Batch| R[Backtest Results]
    end

    subgraph "Mode C: Priming"
    H -->|Prime| S[Hydrated State]
    end

    subgraph "Mode B: Streaming"
    S --> I[Indicator Instance]
    L -->|Update| I
    I -->|Update| O[Live Results]
    end

    I -.->|IsHot| V[Valid State]
