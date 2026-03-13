# WCLPRICE: Weighted Close Price

> *Weighted close doubles the closing price's vote, acknowledging that where a bar ends matters most.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (WCLPRICE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [wclprice.pine](wclprice.pine)                       |

- WCLPRICE computes a Close-biased average of High, Low, and Close by double-weighting the closing price: $(H + L + 2C) \times 0.25$.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

WCLPRICE computes a Close-biased average of High, Low, and Close by double-weighting the closing price: $(H + L + 2C) \times 0.25$. This gives Close 50% of the total weight versus 25% each for High and Low, reflecting the widely held belief that the closing price is the most important price of the bar because it represents the final consensus of buyers and sellers. The calculation is stateless, costs a single FMA instruction per bar, and is TA-Lib compatible (`TA_WCLPRICE`).

## Historical Context

Weighted Close Price appears in technical analysis literature from the 1970s onward, typically credited to the general tradition of market technicians rather than a single inventor. The rationale is straightforward: while High and Low show where price was rejected, Close shows where participants were willing to hold positions overnight (or into the next period). Double-weighting Close amplifies this "settlement consensus" signal.

The formula $(H + L + 2C) / 4$ is algebraically equivalent to $(H + L) / 4 + C / 2$, which reveals its structure: half the weight on Close, and the other half split equally between the range extremes. This makes WCLPRICE a compromise between raw Close and the range-neutral MEDPRICE. When Close is at the midpoint of the range, WCLPRICE equals MEDPRICE; when Close diverges from the midpoint, WCLPRICE follows Close more aggressively than either TYPPRICE or AVGPRICE.

In QuanTAlib, `TBar.HLCC4` provides the same value as a zero-cost computed property. The `Wclprice` indicator class wraps this in the streaming `ITValuePublisher` interface with bar correction, NaN safety, and event chaining.

## Architecture & Physics

### 1. Core Formula

$$\text{WclPrice}_t = (H_t + L_t + 2C_t) \times 0.25$$

Implemented as FMA to avoid division:

$$\text{WclPrice}_t = \text{FMA}(C_t,\; 0.5,\; (H_t + L_t) \times 0.25)$$

This form is optimal: the FMA computes $C \times 0.5 + (H+L) \times 0.25$ in a single fused operation, avoiding the intermediate rounding that separate multiply-add would produce.

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

### Weight Distribution

| Transform | O weight | H weight | L weight | C weight |
|-----------|:--------:|:--------:|:--------:|:--------:|
| AVGPRICE | 25% | 25% | 25% | 25% |
| MEDPRICE | 0% | 50% | 50% | 0% |
| TYPPRICE | 0% | 33.3% | 33.3% | 33.3% |
| **WCLPRICE** | **0%** | **25%** | **25%** | **50%** |

### Output Interpretation

| Context | Meaning |
|---------|---------|
| WCLPRICE > TYPPRICE | Close above the HLC midpoint (strong close) |
| WCLPRICE < TYPPRICE | Close below the HLC midpoint (weak close) |
| WCLPRICE $\approx$ MEDPRICE | Close at range midpoint; balanced bar |
| WCLPRICE diverging from AVGPRICE | Open and Close on opposite sides of the range |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (H+L) | 1 | 1 | 1 |
| MUL ((H+L) × 0.25) | 1 | 3 | 3 |
| FMA (C × 0.5 + prev) | 1 | 4 | 4 |
| **Total (hot)** | **3** | | **~8 cycles** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: element-wise FMA, no inter-bar dependency |
| Optimal strategy | `Fma.MultiplyAdd` over H/L/C vectors on AVX2+ |
| Memory | $O(1)$ streaming; $O(n)$ batch output span |
| Throughput | Near memory-bandwidth bound for large series |

## Resources

- **TA-Lib** `TA_WCLPRICE` function reference.
- **Achelis, S.B.** *Technical Analysis from A to Z*. McGraw-Hill, 2000. (Weighted Close definition)