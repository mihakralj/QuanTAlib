# ADXR: Average Directional Movement Rating

> If ADX is the speedometer, ADXR is the cruise control setting. It smooths out the acceleration to tell you if the trend has staying power.

The Average Directional Movement Rating (ADXR) is a smoothed version of the ADX. It dampens the volatility of the ADX itself, providing a more stable—albeit significantly more lagging—measure of trend strength. It is primarily used to rate the efficacy of trend-following strategies before capital is committed.

## The 1978 Standard

J. Welles Wilder Jr. introduced ADXR alongside ADX in *New Concepts in Technical Trading Systems* (1978). His goal was simple: ADX can be erratic. By averaging the current ADX with a past ADX, he created a metric that ignores short-term fluctuations in trend strength.

It is effectively a "momentum of momentum" indicator, smoothed to the point of geological stability.

## Architecture & Physics

ADXR is a composite indicator. It does not interact with price directly; it interacts with the output of the ADX.

1. **Dependency**: It instantiates and maintains a full `Adx` indicator internally.
2. **History**: It maintains a circular buffer of historical ADX values.
3. **Averaging**: It computes the arithmetic mean of the current ADX and the ADX from `Period - 1` bars ago.

### The Lag Trade-off

ADXR is intentionally slow.

- **ADX** lags price because of its multiple smoothing layers.
- **ADXR** lags ADX because it averages the current value with a value from the distant past.

This double lag makes ADXR useless for entry timing. Its only valid architectural purpose is **regime filtering**: determining *if* a trend-following system should be active, not *when* it should trade.

## Mathematical Foundation

The formula is deceptively simple, but relies on the complex ADX calculation underneath.

$$
ADXR_t = \frac{ADX_t + ADX_{t-(n-1)}}{2}
$$

Where:

- $ADX_t$ is the current ADX value.
- $n$ is the Period (typically 14).
- $ADX_{t-(n-1)}$ is the ADX value from `n-1` periods ago.

*Note: The `n-1` lag is used to match TA-Lib's implementation exactly. Some sources cite `n`, but standard reference implementations use `n-1`.*

## Performance Profile

The performance cost is dominated by the underlying ADX calculation. The ADXR step itself is trivial.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~6ns / bar | Slightly slower than ADX due to history lookup |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Ring buffer access is constant time |
| **Memory** | O(N) | Requires a buffer of size `Period` for ADX history |

## Validation

Validation is performed against **TA-Lib**.

- **Lag Alignment**: The lag (`Period - 1`) is explicitly aligned to match TA-Lib's behavior.
- **Warmup**: ADXR requires significantly more warmup than ADX.
  - ADX Warmup: $\approx 2 \times Period$
  - ADXR Warmup: $ADX\_Warmup + Period$
- **Convergence**: Matches TA-Lib to within `1e-9` once fully warmed up.

### Common Pitfalls

- **Using for Entries**: Do not use ADXR crossovers for entries. The signal is too late.
- **Short Periods**: Using a short period (e.g., 3) defeats the purpose of ADXR. If you want responsiveness, use ADX. ADXR is for stability.
