# TEMA: Triple Exponential Moving Average

> "Patrick Mulloy looked at the lag of an EMA and took it personally. TEMA is what happens when you apply algebra to impatience."

<!-- QUICK REFERENCE CARD (scan in 5 seconds) -->

| Property     | Value |
|--------------|-------|
| Category     | Trend (IIR MA) |
| Inputs       | Source (close) |
| Parameters   | `period` (int, default: 30, valid: >= 1) |
| Outputs      | Single series (TEMA) |
| Output range | Tracks input |
| Warmup       | `period * 3` bars |
| **Signature**    | [tema_signature](tema_signature) |

### Key takeaways

- TEMA combines three cascaded EMAs using a weighted formula to dramatically reduce lag while maintaining smoothness.
- It achieves near-zero-lag tracking by mathematically canceling out the delay inherent in exponential smoothing.
- Particularly effective for fast-moving markets where traditional EMAs are too sluggish.
- The aggressive responsiveness comes at the cost of increased noise and occasional overshoot on sharp reversals.
- Often used as a replacement for EMA in MACD and other trend-following systems requiring minimal delay.

## Historical Context

Introduced by Patrick Mulloy in *Technical Analysis of Stocks & Commodities* (Jan 1994), "Smoothing Data With Less Lag." Mulloy's goal was to replace the standard moving averages in MACD and other indicators to reduce the delay in signal generation.

## What It Measures and Why It Matters

TEMA measures the smoothed trend of price action with dramatically reduced lag compared to traditional exponential moving averages. It mathematically compensates for the inherent delay in exponential smoothing by combining three cascaded EMAs in a weighted formula that effectively "looks ahead" in the trend.

This matters because traditional EMAs introduce significant lag - an EMA with period N takes approximately 3.45×(N+1) bars to converge, creating delayed signals in fast-moving markets. TEMA reduces this lag by 60-80% while maintaining the smoothness and noise reduction properties of exponential smoothing. Traders use TEMA when they need responsive trend signals without the whipsaw noise of simple moving averages, particularly in MACD-based systems where lag can significantly degrade performance.

## Architecture & Physics

TEMA is not just "EMA applied three times." That would be $EMA(EMA(EMA(x)))$. TEMA is a composite:
$$ TEMA = 3 \cdot EMA_1 - 3 \cdot EMA_2 + EMA_3 $$

This formula effectively projects the trend forward to compensate for the delay inherent in smoothing.

### Convergence Speed

Because of the aggressive weighting, TEMA converges (warms up) faster than a standard EMA. While an EMA takes $\approx 3.45(N+1)$ steps to converge to 99.9%, TEMA stabilizes quicker due to the subtraction terms canceling out the initial error.

## Interpretation and Signals

### Trend Direction

TEMA tracks price trends with minimal lag, making it excellent for identifying trend changes. When TEMA slopes upward, it indicates bullish momentum; downward slope indicates bearish momentum. The reduced lag means TEMA will turn direction sooner than traditional EMAs during trend changes.

### Crossover Signals

TEMA crossovers with price or other moving averages provide entry/exit signals:

- **Price crossovers**: When price crosses above TEMA, it suggests bullish momentum; crossing below suggests bearish momentum.
- **TEMA/EMA crossovers**: TEMA crossing above a slower EMA indicates accelerating bullish momentum.

### Divergence Analysis

TEMA divergences from price can signal potential reversals:

- **Bullish divergence**: Price makes lower lows while TEMA makes higher lows.
- **Bearish divergence**: Price makes higher highs while TEMA makes lower highs.

### Signal Quality Factors

- **Strength**: The steeper the TEMA slope, the stronger the trend momentum.
- **Smoothness**: Despite reduced lag, TEMA maintains reasonable smoothness for reliable signals.
- **Confirmation**: Best used with volume confirmation and other momentum indicators.

## Mathematical Foundation

### 1. The Cascade

$$ EMA_1 = EMA(Price) $$
$$ EMA_2 = EMA(EMA_1) $$
$$ EMA_3 = EMA(EMA_2) $$

### 2. The Combination

$$ TEMA = (3 \times EMA_1) - (3 \times EMA_2) + EMA_3 $$

## Performance Profile

### Operation Count (Streaming Mode)

TEMA requires 3 cascaded EMA updates plus the combination formula:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| EMA update (×3) | 3 | 7 | 21 |
| MUL (3×e1, 3×e2) | 2 | 3 | 6 |
| SUB (3×e1 - 3×e2) | 1 | 1 | 1 |
| ADD (+ e3) | 1 | 1 | 1 |
| **Total (hot)** | **7** | — | **~29 cycles** |

During warmup, each EMA stage has additional compensator overhead (~21 cycles × 3 = ~63 cycles).

**Total during warmup:** ~92 cycles/bar; **Post-warmup:** ~29 cycles/bar.

### Batch Mode (SIMD Analysis)

TEMA is inherently recursive due to cascaded EMAs. SIMD parallelization across bars is not possible. Each EMA stage must complete before feeding the next:

| Optimization | Operations | Cycles Saved |
| :--- | :---: | :---: |
| FMA in each EMA stage | 3 FMA vs 3×(MUL+ADD) | ~6 cycles |
| Inline combination | Avoid intermediate stores | ~2 cycles |

**Per-bar efficiency:** ~29 cycles is 4× EMA cost, as expected for 3 EMA stages + combiner.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib exactly |
| **Timeliness** | 10/10 | Extremely low lag; nearly zero-lag tracking |
| **Overshoot** | 5/10 | Significant overshoot on sharp reversals |
| **Smoothness** | 6/10 | Less smooth than SMA/EMA due to high responsiveness |

## Related Indicators

- **[EMA](../../trends_IIR/ema/Ema.md)**: Single exponential smoothing; TEMA is essentially EMA with lag cancellation.
- **[DEMA](../../trends_IIR/dema/Dema.md)**: Double exponential smoothing; TEMA extends this to triple smoothing.
- **[T3](../../trends_IIR/t3/T3.md)**: Generalized Tillson moving average; TEMA is T3 with volume factor = 1.
- **[MACD](../../oscillators/macd/Macd.md)**: Often uses TEMA instead of EMA for faster signals.
- **[KAMA](../../trends_IIR/kama/Kama.md)**: Adaptive smoothing; complementary approach to fixed-period TEMA.

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~6 ns/bar | 3× EMA overhead |
| **Allocations** | 0 bytes | Zero-allocation in hot paths |
| **Complexity** | O(1) | Constant time regardless of period |
| **State Size** | 96 bytes | Three EMA states (32 bytes each) |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_TEMA` exactly. |
| **Skender** | ✅ | Matches `GetTema` exactly. |
| **Tulip** | ✅ | Matches `tema` exactly. |
| **Ooples** | ❌ | Diverges significantly due to initialization logic. |

## C# Implementation Considerations

### State Management

TEMA maintains six EmaState instances—three current, three previous—enabling atomic rollback on bar corrections:

```csharp
private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated);

private EmaState _state1, _state2, _state3;
private EmaState _p_state1, _p_state2, _p_state3;
```

The `E` field tracks bias compensation factor for each EMA stage independently. Each state auto-transitions via `IsCompensated` flag when bias becomes negligible.

### Precomputed Constants

Constructor calculates smoothing constants once:

```csharp
_alpha = 2.0 / (period + 1);
_decay = 1 - _alpha;
```

These constants are reused across all three EMA stages, avoiding repeated division.

### FMA Usage

Each EMA update uses FusedMultiplyAdd for the standard EMA formula:

```csharp
double newEma = Math.FusedMultiplyAdd(state.Ema, _decay, _alpha * input);
```

The final TEMA combination `3*e1 - 3*e2 + e3` could use FMA but the coefficients (3, -3, 1) make chained FMA marginal; current implementation uses direct arithmetic.

### Bar Correction Pattern

TEMA's cascaded structure requires coordinated state rollback:

```csharp
if (isNew)
{
    _p_state1 = _state1;
    _p_state2 = _state2;
    _p_state3 = _state3;
}
else
{
    _state1 = _p_state1;
    _state2 = _p_state2;
    _state3 = _p_state3;
}
```

All three stages rollback atomically, ensuring consistent cascade state when `isNew=false`.

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_alpha` | double | 8B | Smoothing constant |
| `_decay` | double | 8B | 1 - alpha |
| `_state1` | EmaState | 24B | First EMA state |
| `_state2` | EmaState | 24B | Second EMA state |
| `_state3` | EmaState | 24B | Third EMA state |
| `_p_state1` | EmaState | 24B | Previous state 1 |
| `_p_state2` | EmaState | 24B | Previous state 2 |
| `_p_state3` | EmaState | 24B | Previous state 3 |
| **Total** | | **160B** | Per indicator instance |

Each EmaState contains: Ema (8B), E (8B), IsHot (1B), IsCompensated (1B) + padding (~6B) = ~24B.

### Common Pitfalls

1. **Overshoot**: TEMA is so responsive it can overshoot price turns, creating a "whiplash" effect in volatile markets.
2. **Noise**: By reducing lag, TEMA sacrifices some noise suppression. It is "nervous" compared to an SMA.
3. **Identity Crisis**: Often confused with T3 (Tillson). T3 is a generalized version; TEMA is specifically T3 with $v=1$.
4. **Warmup period**: Requires 3× period bars before producing valid output; premature signals are unreliable.
5. **Parameter sensitivity**: Small period values (< 10) create excessive noise; large values (> 50) reduce responsiveness.
6. **False signals**: In choppy, sideways markets, frequent crossovers generate misleading signals.
7. **Computational cost**: 4× more expensive than simple EMA due to cascaded calculations.

## FAQ

**Q: How does TEMA differ from a triple-smoothed EMA?**
A: A triple-smoothed EMA would be EMA(EMA(EMA(price))), which introduces massive lag. TEMA uses the formula 3×EMA₁ - 3×EMA₂ + EMA₃ to cancel out lag while maintaining the smoothing effect.

**Q: What's the relationship between TEMA and DEMA?**
A: DEMA is 2×EMA₁ - EMA₂. TEMA extends this to 3×EMA₁ - 3×EMA₂ + EMA₃. Both use weighted combinations to reduce lag, but TEMA goes further with triple smoothing.

**Q: When should I use TEMA instead of EMA?**
A: Use TEMA when you need minimal lag for timing-critical signals (like MACD triggers) but still want the smoothness of exponential smoothing. Use EMA for general trend following where some lag is acceptable.

**Q: Can TEMA be used for scalping?**
A: Yes, with short periods (5-15), but be aware of increased noise and false signals. Combine with volume confirmation and other filters to reduce whipsaws.

## References

- Mulloy, P. (1994). "Smoothing Data With Less Lag." *Technical Analysis of Stocks & Commodities*, 12(1).
- Kaufman, P. J. (1995). *Smarter Trading*. McGraw-Hill. (Chapter on adaptive moving averages)
- Tillson, T. (1998). "Generalized Moving Averages." *Technical Analysis of Stocks & Commodities*.
- Ehler, J. (2001). *Rocket Science for Traders*. Wiley. (Discussion of lag reduction techniques)
