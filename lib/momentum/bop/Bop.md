# BOP: Balance of Power

> "The market is a tug of war between buyers and sellers. BOP tells you who's pulling harder."

The Balance of Power (BOP) indicator measures the strength of buying and selling pressure by comparing the closing price to the opening price, relative to the high-low range. It oscillates between -1 and 1, providing a clear picture of market dominance.

## Historical Context

Developed by Igor Livshin and published in the August 2001 issue of *Stocks & Commodities* magazine, BOP was designed to expose the underlying action of price movement. Unlike trend-following indicators that lag, BOP is a momentum oscillator that can identify hidden accumulation or distribution patterns.

## Architecture & Physics

BOP is a stateless, zero-lag indicator in its raw form. It evaluates each bar independently, calculating the ratio of the body (Close - Open) to the range (High - Low).

* **Inertia**: None (raw).
* **Momentum**: Instantaneous.
* **Range**: Bounded [-1, 1].

### The Zero-Range Challenge

A key architectural challenge is handling bars where `High == Low`. In these cases, the range is zero, leading to a potential division by zero. QuanTAlib handles this by returning 0, indicating a neutral balance of power (no movement).

## Mathematical Foundation

The formula is deceptively simple:

$$ BOP = \frac{Close - Open}{High - Low} $$

Where:

* **Close > Open**: Positive BOP (Buyers dominate)
* **Close < Open**: Negative BOP (Sellers dominate)
* **Close = Open**: Zero BOP (Balance)
* **High = Low**: Zero BOP (No movement)

## Performance Profile

BOP is extremely lightweight, requiring minimal computation.

### Zero-Allocation Design

The implementation uses `stackalloc` and `Span<T>` where applicable, ensuring no heap allocations during the `Update` cycle. The `Calculate` method is fully vectorized using SIMD instructions (AVX2) when available, processing multiple bars in parallel.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 1ns | O(1) per bar, SIMD-optimized. |
| **Allocations** | 0 | Zero allocations in the hot path. |
| **Complexity** | O(1) | Constant time per update. |
| **Accuracy** | 10/10 | Exact mathematical calculation. |
| **Timeliness** | 10/10 | Zero lag. |
| **Overshoot** | 0/10 | Bounded -1 to 1. |
| **Smoothness** | 0/10 | Raw signal, very noisy. |

## Validation

BOP is validated against major technical analysis libraries to ensure correctness.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_BOP` exactly. |
| **Skender** | ✅ | Matches `GetBop`. |
| **Tulip** | ✅ | Matches `ti.bop`. |
| **Ooples** | ✅ | Matches `CalculateBalanceOfPower`. |

### Common Pitfalls

* **Noise**: The raw BOP is very volatile. It is often smoothed with a Moving Average (e.g., SMA-14) to identify trends. QuanTAlib provides the raw signal, allowing you to chain any smoothing algorithm you prefer.
* **Doji Candles**: When Open equals Close, BOP is 0. This is mathematically correct but can be interpreted as a lack of momentum.

## Usage

```csharp
using QuanTAlib;

// 1. Streaming (Real-time)
var bop = new Bop();
TValue result = bop.Update(new TBar(time, open, high, low, close, volume));
Console.WriteLine($"BOP: {result.Value}");

// 2. Batch (Historical)
var bars = new TBarSeries(...);
var bopSeries = Bop.Batch(bars);

// 3. Chaining (Smoothing)
var smoothedBop = new Sma(14);
var bop = new Bop();
// ... inside loop ...
var raw = bop.Update(bar);
var smooth = smoothedBop.Update(raw);
