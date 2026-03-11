# TYPPRICE: Typical Price

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (TYPPRICE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [typprice.pine](typprice.pine)                       |

- TYPPRICE computes the equal-weighted average of Open, High, and Low: $(O + H + L) \times \frac{1}{3}$.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `1` bars of warmup before first valid output (IsHot = true).
- Equivalent to `TBar.OHL3` computed property.

TYPPRICE computes the equal-weighted average of Open, High, and Low: $(O + H + L) \times \frac{1}{3}$. This three-component mean captures the opening price and the full intra-bar range without including the settlement (Close). By excluding Close, Typical Price isolates the session's initial positioning and range extremes, making it useful as an input where you want a price representative that is independent of closing action. The calculation is stateless and costs a single FMA instruction per bar.

## Historical Context

The OHL3 variant of Typical Price represents the average of the bar's opening level and its range extremes. Unlike the more common HLC3 formulation (which TA-Lib implements as `TA_TYPPRICE`), OHL3 excludes the closing price entirely. This makes it suitable for analysis where the settlement price should not influence the representative price, for example when studying intra-session price discovery or when the closing price is already used as a separate signal component.

In QuanTAlib, `TBar.OHL3` provides the same value as a zero-cost computed property. The `Typprice` indicator class wraps this in the streaming `ITValuePublisher` interface with bar correction, NaN safety, and event chaining.

## Architecture & Physics

### 1. Core Formula

$$\text{TypPrice}_t = (O_t + H_t + L_t) \times \tfrac{1}{3}$$

Implemented as FMA with a precomputed reciprocal constant:

$$\text{TypPrice}_t = \text{FMA}\!\left(O_t,\; \tfrac{1}{3},\; (H_t + L_t) \times \tfrac{1}{3}\right)$$

The constant $\frac{1}{3}$ is stored as `private const double OneThird = 1.0 / 3.0`, evaluated at compile time. No runtime division occurs.

### 2. State Management

Stateless per bar. State exists only for:

- **Last-valid substitution**: Non-finite O, H, or L values are replaced with the last known finite value for that component.
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

```text
function TYPPRICE(bar):
    const OneThird ← 1.0 / 3.0   // compile-time constant

    o, h, l ← bar.Open, bar.High, bar.Low

    // Substitute last-valid for non-finite inputs
    if !finite(o): o ← lastValidOpen
    if !finite(h): h ← lastValidHigh
    if !finite(l): l ← lastValidLow

    result ← FMA(o, OneThird, (h + l) × OneThird)
    return result
```

### Output Interpretation

| Context | Meaning |
|---------|---------|
| Close > TYPPRICE | Close above session's OHL center (bullish settlement relative to range) |
| Close < TYPPRICE | Close below session's OHL center (bearish settlement relative to range) |
| TYPPRICE trending up | Opening levels and range are rising |
| TYPPRICE as input | Useful where Close independence is desired |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (H+L) | 1 | 1 | 1 |
| MUL ((H+L) × OneThird) | 1 | 3 | 3 |
| FMA (O × OneThird + prev) | 1 | 4 | 4 |
| **Total (hot)** | **3** | | **~8 cycles** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: element-wise arithmetic, no inter-bar dependency |
| Optimal strategy | `Vector<double>` over O/H/L spans with broadcast OneThird |
| Memory | $O(1)$ streaming; $O(n)$ batch output span |
| Throughput | Near memory-bandwidth bound for large series |

## Resources

- **QuanTAlib** `TBar.OHL3` computed property reference.
