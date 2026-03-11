# AVGPRICE: Average Price

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (AVGPRICE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [avgprice.pine](avgprice.pine)                       |

- AVGPRICE computes the arithmetic mean of a bar's four canonical prices: Open, High, Low, and Close.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

AVGPRICE computes the arithmetic mean of a bar's four canonical prices: Open, High, Low, and Close. The formula $\frac{O + H + L + C}{4}$ produces a single representative price that weights all four price components equally, unlike Typical Price (which excludes Open) or Weighted Close (which double-weights Close). This equal weighting makes AVGPRICE the least biased single-bar summary statistic, useful as a neutral input to downstream indicators when no particular price component deserves emphasis. The calculation is stateless, requires no warmup, and costs a single FMA instruction per bar.

## Historical Context

Average Price is one of the oldest price transforms in technical analysis, predating computer-based charting by decades. Its inclusion in the TA-Lib function set (`TA_AVGPRICE`) standardized it as a canonical operation alongside MEDPRICE, TYPPRICE, and WCLPRICE. The four-price average gained popularity because it distributes weight across the full intra-bar range: Open captures the session's starting sentiment, High and Low bound the extremes where supply and demand exhausted themselves, and Close reflects the final consensus.

In practice, AVGPRICE and OHLC4 are identical. QuanTAlib exposes both: `TBar.OHLC4` as a zero-cost computed property for inline use, and `Avgprice` as a streaming indicator class supporting bar correction, event chaining, and batch processing. The indicator form exists because downstream consumers (Quantower adapters, chained indicator pipelines) require the `ITValuePublisher` interface and `isNew` rollback semantics that a bare struct property cannot provide.

## Architecture & Physics

### 1. Core Formula

$$\text{AvgPrice}_t = \frac{O_t + H_t + L_t + C_t}{4}$$

Implemented as FMA to avoid division on the hot path:

$$\text{AvgPrice}_t = \text{FMA}(O_t + H_t,\; 0.25,\; (L_t + C_t) \times 0.25)$$

### 2. State Management

No rolling window, no lookback buffer. The indicator is stateless per bar. State exists only for:

- **Last-valid substitution**: If any OHLC component is `NaN`/`Infinity`, the last finite value for that component is used.
- **Bar correction**: `isNew=false` rolls back to previous state, enabling same-timestamp rewrites.

### 3. Complexity

$O(1)$ per bar. Two additions, one FMA. No memory allocation. Always hot after the first bar.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

### Relationship to TBar Properties

| Transform | Formula | TBar Property | Indicator Class |
|-----------|---------|---------------|-----------------|
| Average Price | $(O+H+L+C) \times 0.25$ | `OHLC4` | `Avgprice` |
| Median Price | $(H+L) \times 0.5$ | `HL2` | `Medprice` |
| Typical Price | $(H+L+C) \times \frac{1}{3}$ | `HLC3` | `Typprice` |
| Weighted Close | $(H+L+2C) \times 0.25$ | `HLCC4` | `Wclprice` |

### Pseudo-code

```
function AVGPRICE(bar):
    o, h, l, c ← bar.Open, bar.High, bar.Low, bar.Close

    // Substitute last-valid for non-finite inputs
    if !finite(o): o ← lastValidOpen
    if !finite(h): h ← lastValidHigh
    if !finite(l): l ← lastValidLow
    if !finite(c): c ← lastValidClose

    result ← FMA(o + h, 0.25, (l + c) × 0.25)
    return result
```

### Output Interpretation

| Context | Meaning |
|---------|---------|
| AVGPRICE > Close | Intra-bar action skewed higher than settlement |
| AVGPRICE < Close | Close settled above the bar's center of mass |
| AVGPRICE $\approx$ Close | Symmetric bar (doji-like) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (O+H) | 1 | 1 | 1 |
| ADD (L+C) | 1 | 1 | 1 |
| MUL ((L+C) × 0.25) | 1 | 3 | 3 |
| FMA ((O+H) × 0.25 + prev) | 1 | 4 | 4 |
| **Total (hot)** | **4** | | **~9 cycles** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: element-wise arithmetic, no inter-bar dependency |
| Optimal strategy | `Vector<double>` over OHLC spans; 4-wide on AVX2, 8-wide on AVX-512 |
| Memory | $O(1)$ streaming; $O(n)$ batch output span |
| Throughput | Near memory-bandwidth bound for large series |

## Resources

- **TA-Lib** `TA_AVGPRICE` function reference.
- **Murphy, J.J.** *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
