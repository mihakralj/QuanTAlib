# MEDPRICE: Median Price

> *The midpoint of high and low captures the bar's central tendency, ignoring where it opened or closed.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (MEDPRICE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [medprice.pine](medprice.pine)                       |

- MEDPRICE computes the midpoint of a bar's High and Low: $(H + L) \times 0.5$.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

MEDPRICE computes the midpoint of a bar's High and Low: $(H + L) \times 0.5$. This is the simplest possible estimate of a bar's "fair value," splitting the difference between the session's extremes while ignoring both the opening gap and closing settlement. The result represents the geometric center of the bar's vertical range. Because it excludes Open and Close, MEDPRICE responds purely to the supply/demand boundaries that the market tested, making it a useful input for range-based indicators like CCI or as a detrending reference. Stateless, zero-warmup, one addition and one multiply per bar.

## Historical Context

Median Price (also called "Mid Price" or "HL/2") is among the most elemental price transforms, used long before computers entered trading floors. The TA-Lib function `TA_MEDPRICE` standardized the computation, and most charting platforms expose it as a built-in price source. The name "Median Price" is a slight misnomer in the statistical sense: it is the midrange (arithmetic mean of extremes), not the median of a distribution. The name stuck through decades of usage.

The key distinction from Typical Price ($HLC/3$) is the exclusion of Close. This matters when the closing price diverges significantly from the bar's center, as happens with gap-up closes, stop runs, or end-of-session order flow. MEDPRICE treats the bar as a symmetric range and asks: where was the midpoint of price exploration?

In QuanTAlib, `TBar.HL2` provides the same value as a zero-cost computed property. The `Medprice` indicator class wraps this in the streaming `ITValuePublisher` interface with bar correction, NaN safety, and event chaining support.

## Architecture & Physics

### 1. Core Formula

$$\text{MedPrice}_t = (H_t + L_t) \times 0.5$$

No FMA benefit here: the pattern is $(a + b) \times c$, not $a \times b + c$.

### 2. State Management

Stateless per bar. State exists only for:

- **Last-valid substitution**: Non-finite High or Low values are replaced with the last known finite value for that component.
- **Bar correction**: `isNew=false` rolls back to previous state for same-timestamp rewrites.

### 3. Complexity

$O(1)$ per bar. One addition, one multiply. No memory allocation. Always hot after the first bar.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

### Price Transform Comparison

| Transform | Components | Weights | Bias |
|-----------|:----------:|---------|------|
| MEDPRICE | H, L | Equal | Range-centered; ignores O/C |
| TYPPRICE | H, L, C | Equal | Close-influenced |
| AVGPRICE | O, H, L, C | Equal | Fully balanced |
| WCLPRICE | H, L, C | C double-weighted | Close-biased |

### Output Interpretation

| Context | Meaning |
|---------|---------|
| Close > MEDPRICE | Close above the range midpoint (bullish bar body) |
| Close < MEDPRICE | Close below the range midpoint (bearish bar body) |
| Close $\approx$ MEDPRICE | Close near center of range (indecision) |
| MEDPRICE expanding | Increasing bar ranges (volatility expanding) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (H+L) | 1 | 1 | 1 |
| MUL (× 0.5) | 1 | 3 | 3 |
| **Total (hot)** | **2** | | **~4 cycles** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: element-wise add + multiply, no inter-bar dependency |
| Optimal strategy | `Vector<double>` over High/Low spans |
| Memory | $O(1)$ streaming; $O(n)$ batch output span |
| Throughput | Memory-bandwidth bound; trivial compute |

## Resources

- **TA-Lib** `TA_MEDPRICE` function reference.
- **Murphy, J.J.** *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.