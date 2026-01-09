# STC: Schaff Trend Cycle

> "Because MACD is a trend indicator, it has the same problems as all trend indicators: lag. The STC solves this by using a Cycle component to identify trends faster."

The Schaff Trend Cycle (STC) is a technical indicator developed by **Doug Schaff** in the 1990s. It combines the trend-following benefits of the **MACD** (Moving Average Convergence Divergence) with the cyclic sensitivity of the **Stochastic Oscillator**. By applying a double-smoothing stochastic process to the MACD line, the STC attempts to identify overbought and oversold conditions with greater accuracy and speed than MACD alone, while minimizing the "whipsaws" common in fast stochastics.

## Historical Context

In the late 90s, Doug Schaff sought to solve the pivotal problem of currency trading: trends are profitable, but trend indicators lag. Oscillators are timely, but noisy. Schaff's insight was to treat the specific "trendiness" of price (measured by MACD) as the *source* data for a cycle analysis (Stochastic).

The result is a bounded oscillator (0-100) that moves in distinct "regimes": stabilizing at 0 in downtrends, 100 in uptrends, and cycling cleanly between them during reversals. It is particularly noted for its "sigmoid" wave shape, often spending extended time at extremes rather than oscillating sinusoidally.

## Architecture & Physics

The STC is essentially a **recursive fractal**: it applies the Stochastic formula to the MACD, smoothes the result, and then applies the Stochastic formula *again* to that smoothed result.

1. **MACD Foundation**: The core signal is the difference between Fast and Slow EMAs of price.
2. **First Derivative (Stoch #1)**: Normalizes the MACD into a 0-100 range based on its recent range (`Cycle Length`).
3. **Smoothing**: An EMA (typically length 3, factor 0.5) is applied to Stoch #1.
4. **Second Derivative (Stoch #2)**: The Stochastic formula is applied again to the *smoothed Stoch #1*.
5. **Final Smoothing**: The result is smoothed again (or transformed via Sigmoid/Digital logic).

This "Stoch of a Stoch of MACD" architecture filters out high-frequency noise while compressing the trend signal into a binary-like wave. The inertia of the double-smoothing creates a "heavy" indicator that resists changing direction until the evidence is overwhelming, reducing false signals.

### The Smoothing Challenge

Standard STC uses a simple EMA for smoothing. However, QuanTAlib offers three modes to adapt the signal shape to modern algorithmic needs:

* **EMA (Standard)**: Classic Schaff behavior.
* **Sigmoid**: Applies a logistic function to force values to extremes, creating a "square wave" effect that reduces noise in the middle range (40-60).
* **Digital**: A strict trinary output (0, 100, or Hold) for hard-logic trading systems.

## Mathematical Foundation

The calculation involves a cascade of EMAs and Normalizations.

### 1. MACD

$$ \text{MACD} = \text{EMA}(Close, L_{fast}) - \text{EMA}(Close, L_{slow}) $$

### 2. First Stochastic (%K1) on MACD

$$ \%K_1 = 100 \times \frac{\text{MACD} - \text{LLV}(\text{MACD}, L_{k})}{\text{HHV}(\text{MACD}, L_{k}) - \text{LLV}(\text{MACD}, L_{k})} $$

### 3. Smoothed %D1

$$ \%D_1 = \text{EMA}(\%K_1, L_{d}) $$

### 4. Second Stochastic (%K2) on %D1

$$ \%K_2 = 100 \times \frac{\%D_1 - \text{LLV}(\%D_1, L_{k})}{\text{HHV}(\%D_1, L_{k}) - \text{LLV}(\%D_1, L_{k})} $$

### 5. Final STC Output

Depending on `StcSmoothing`:

* **None**: $\text{STC} = \%K_2$
* **EMA**: $\text{STC} = \text{EMA}(\%K_2, 3)$
* **Sigmoid**: $\text{STC} = \frac{100}{1 + e^{-0.1 \times (\%K_2 - 50)}}$
* **Digital**:
    $$
    \text{STC} = \begin{cases}
    100 & \text{if } \%K_2 > 75 \\
    0 & \text{if } \%K_2 < 25 \\
    \text{STC}_{prev} & \text{otherwise}
    \end{cases}
    $$

## Performance Profile

STC is computationally intensive due to the multiple layers of history required (MACD history -> Stoch history -> Stoch history).

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 120 ns/bar | Moderate. Requires valid MACD & Stoch history buffers. |
| **Allocations** | 0 | Zero-allocation in hot path (RingBuffers used). |
| **Complexity** | O(1) | Lookbacks are fixed windows, managed via rolling updates. |
| **Accuracy** | 9/10 | Matches PineScript/Standard implementations precisely. |
| **Timeliness** | 7/10 | Double smoothing induces lag, but Cycle logic compensates. |
| **Smoothness** | 10/10 | Extremely smooth, almost binary oscillatory behavior. |

## Validation

Compared against Skender.Stock.Indicators (Standard EMA mode).

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pinescript** | ✅ | Core logic matches `stc.pine`. |
| **Skender** | ✅ | Validated against `GetStc(10, 23, 50)`. |
| **TA-Lib** | N/A | Not available in standard TA-Lib. |

## Usage

```csharp
using QuanTAlib;

// 1. Standard STC (K=10, D=3, Fast=23, Slow=50, Sigmoid Smoothing)
var stc = new Stc(kPeriod: 10, dPeriod: 3, fastLength: 23, slowLength: 50, smoothing: StcSmoothing.Sigmoid);

// 2. Feed data
stc.Update(new TValue(time, price));

// 3. Access result
double value = stc.Last.Value;

// 4. Chain from another indicator
var macd = new Macd(26, 50, 9);
var stcFromMacd = new Stc(source: macd, kPeriod: 10, dPeriod: 3);
