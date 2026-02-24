# TYPPRICE: Typical Price

TYPPRICE computes the equal-weighted average of High, Low, and Close: $(H + L + C) \times \frac{1}{3}$. This three-component mean is the most widely used "representative price" in technical analysis, serving as the default input for CCI, MFI, and many other indicators. By including Close but excluding Open, Typical Price captures both the range extremes and the settlement point, giving slightly more weight to closing action than AVGPRICE does. The calculation is stateless and costs a single FMA instruction per bar.

## Historical Context

Typical Price became the standard price transform through its adoption by Donald Lambert in his 1980 Commodity Channel Index (CCI), which explicitly requires $(H+L+C)/3$ as its input. Gene Quong and Avrum Soudack used it in the Money Flow Index (MFI) in 1989. The TA-Lib function `TA_TYPPRICE` codified it as a standalone operation. TradingView exposes it as the `hlc3` built-in source selector.

The choice of three components rather than four is not arbitrary. Excluding Open removes the overnight gap component, which reflects news-driven repositioning rather than intra-session supply and demand. For intraday analysis, this makes Typical Price a purer measure of within-session fair value than AVGPRICE. For daily bars on instruments with significant gaps (equities, futures at session boundaries), the distinction matters; for 24-hour markets (forex, crypto), it is negligible.

In QuanTAlib, `TBar.HLC3` provides the same value as a zero-cost computed property. The `Typprice` indicator class wraps this in the streaming `ITValuePublisher` interface with bar correction, NaN safety, and event chaining.

## Architecture & Physics

### 1. Core Formula

$$\text{TypPrice}_t = (H_t + L_t + C_t) \times \tfrac{1}{3}$$

Implemented as FMA with a precomputed reciprocal constant:

$$\text{TypPrice}_t = \text{FMA}\!\left(H_t,\; \tfrac{1}{3},\; (L_t + C_t) \times \tfrac{1}{3}\right)$$

The constant $\frac{1}{3}$ is stored as `private const double OneThird = 1.0 / 3.0`, evaluated at compile time. No runtime division occurs.

### 2. State Management

Stateless per bar. State exists only for:

- **Last-valid substitution**: Non-finite H, L, or C values are replaced with the last known finite value for that component.
- **Bar correction**: `isNew=false` rolls back to previous state for same-timestamp rewrites.

### 3. Complexity

$O(1)$ per bar. One addition, one FMA. No memory allocation. Always hot after the first bar.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

### Why Not Divide by 3?

Division by a non-power-of-two constant is 4-5x more expensive than multiplication on modern x86 CPUs (~15 cycles vs ~3 cycles). Precomputing $\frac{1}{3}$ as a `const double` and multiplying eliminates the division entirely. The compiler constant-folds `1.0 / 3.0` to the IEEE 754 double `0x3FD5555555555555` at compile time, so the hot path sees only multiply/FMA operations.

### Pseudo-code

```
function TYPPRICE(bar):
    const OneThird ← 1.0 / 3.0   // compile-time constant

    h, l, c ← bar.High, bar.Low, bar.Close

    // Substitute last-valid for non-finite inputs
    if !finite(h): h ← lastValidHigh
    if !finite(l): l ← lastValidLow
    if !finite(c): c ← lastValidClose

    result ← FMA(h, OneThird, (l + c) × OneThird)
    return result
```

### Output Interpretation

| Context | Meaning |
|---------|---------|
| Close > TYPPRICE | Close above session's HLC center (bullish settlement) |
| Close < TYPPRICE | Close below session's HLC center (bearish settlement) |
| TYPPRICE trending up | Both range and settlement are rising |
| TYPPRICE as CCI input | Standard; CCI = (Price - SMA(Price)) / (0.015 × MeanDeviation) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (L+C) | 1 | 1 | 1 |
| MUL ((L+C) × OneThird) | 1 | 3 | 3 |
| FMA (H × OneThird + prev) | 1 | 4 | 4 |
| **Total (hot)** | **3** | | **~8 cycles** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: element-wise arithmetic, no inter-bar dependency |
| Optimal strategy | `Vector<double>` over H/L/C spans with broadcast OneThird |
| Memory | $O(1)$ streaming; $O(n)$ batch output span |
| Throughput | Near memory-bandwidth bound for large series |

## Resources

- **Lambert, D.R.** "Commodity Channel Index: Tools for Trading Cyclical Trends." *Technical Analysis of Stocks & Commodities*, 1980.
- **Quong, G. & Soudack, A.** "Volume-Weighted RSI: Money Flow." *Technical Analysis of Stocks & Commodities*, 1989.
- **TA-Lib** `TA_TYPPRICE` function reference.
