# RAVI: Chande Range Action Verification Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `shortPeriod` (default 7), `longPeriod` (default 65)                      |
| **Outputs**      | Single series (Ravi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `longPeriod` bars (default 65)                          |

### TL;DR

- RAVI (Range Action Verification Index) measures trend strength by computing the absolute percentage divergence between a short-period SMA and a lon...
- Parameterized by `shortperiod` (default 7), `longperiod` (default 65).
- Output range: Varies (see docs).
- Requires `longPeriod` bars (default 65) of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The simplest question in technical analysis is also the most important: is this market trending or not? RAVI answers it with two moving averages and a division."

RAVI (Range Action Verification Index) measures trend strength by computing the absolute percentage divergence between a short-period SMA and a long-period SMA. Created by Tushar Chande and published in *Beyond Technical Analysis* (Wiley, 2001), the indicator classifies markets into trending (RAVI > 3%) and ranging (RAVI < 3%) regimes using a single threshold. With default parameters (short=7, long=65), RAVI requires 65 bars of warmup for the first valid reading. The core computation is three operations per bar in streaming mode: two running-sum updates and one division. No square roots, no exponentials, no recursion. The entire indicator reduces to normalized SMA spread, making it one of the cheapest dynamics classifiers available.

## Historical Context

Tushar Chande holds a PhD in engineering and has spent decades building quantitative tools for traders. His most cited work, VIDYA (Variable Index Dynamic Average), appeared in *Stocks & Commodities* in 1992, introducing the concept of volatility-adaptive smoothing constants. RAVI emerged from the same intellectual thread: if short-term and long-term averages agree on price, the market is going nowhere; if they disagree, something directional is happening.

Chande designed RAVI as a simpler alternative to Wilder's ADX. ADX requires True Range, Directional Movement (+DM/-DM), three separate Wilder smoothings, and a final DX-to-ADX smoothing pass. The computation chain is deep and the warmup period is substantial (Wilder recommended 2N bars for ADX with period N). RAVI bypasses all of that complexity. Two SMAs. One subtraction. One division. One absolute value.

The parameter choice is deliberate. The long SMA of 65 bars corresponds to approximately 13 trading weeks (one quarter), capturing the medium-term sentiment of market participants. The short SMA of 7 bars is roughly 10% of the long period, providing a responsive measure of current price relative to the quarterly trend. The 10:1 ratio between long and short periods ensures sufficient separation for meaningful divergence without the noise amplification that a 3:1 or 5:1 ratio would introduce.

The 3% threshold was Chande's empirical choice for equities. He noted that this value varies by market and timeframe. For forex pairs with lower percentage moves, thresholds of 0.1% to 0.3% are common. For volatile commodities, 5% or higher may be appropriate. The threshold is a parameter, not a constant.

Compared to its competitors in the trend-strength space: ADX is more nuanced (it captures direction via +DI/-DI) but computationally heavier and slower to respond. Kaufman's Efficiency Ratio (ER) measures net displacement versus total path length but operates on raw price changes without averaging. Choppiness Index (CHOP) uses ATR-to-range scaling on a logarithmic axis. PFE measures fractal efficiency in price-time space. RAVI trades sophistication for speed and clarity. It cannot tell you the direction of the trend (the absolute value discards sign), but it tells you whether a trend exists with minimal computational overhead and minimal warmup.

Most implementations across platforms (MetaTrader, NinjaTrader, Wealth-Lab, NanoTrader, Sierra Chart) follow Chande's original SMA-based formula. Some variants offer EMA as an alternative smoothing method, and a few preserve the sign of the difference (positive for price above long MA, negative for below) rather than taking the absolute value. This implementation follows Chande's original: SMA-only, absolute value, outputting a non-negative percentage.

## Architecture and Physics

### 1. Short-Period SMA

The fast simple moving average computes the arithmetic mean of the most recent $N_s$ close values:

$$
\text{SMA}_s(t) = \frac{1}{N_s} \sum_{i=0}^{N_s - 1} C_{t-i}
$$

In streaming mode, a circular buffer of size $N_s$ maintains a running sum. On each new bar, the oldest value is subtracted and the current close is added, achieving O(1) per update.

### 2. Long-Period SMA

The slow simple moving average operates identically over a larger window $N_l$:

$$
\text{SMA}_l(t) = \frac{1}{N_l} \sum_{i=0}^{N_l - 1} C_{t-i}
$$

A separate circular buffer of size $N_l$ with its own running sum provides the O(1) update.

### 3. Absolute Percentage Difference

The raw divergence between averages is normalized by the long SMA and scaled to percentage:

$$
\text{RAVI}_{\text{raw}}(t) = \frac{\text{SMA}_s(t) - \text{SMA}_l(t)}{\text{SMA}_l(t)} \times 100
$$

This normalization makes RAVI price-scale invariant. A $5 stock and a $500 stock with the same percentage structure produce the same RAVI values.

### 4. Absolute Value

Chande's original definition discards direction:

$$
\text{RAVI}(t) = \left| \text{RAVI}_{\text{raw}}(t) \right|
$$

The output is always non-negative. Values represent the magnitude of divergence between short-term and long-term price consensus, regardless of whether the short MA is above or below the long MA.

### 5. Threshold Classification

RAVI's primary use is binary classification:

$$
\text{Regime} = \begin{cases}
\text{Trending} & \text{if } \text{RAVI}(t) > \theta \\
\text{Ranging} & \text{if } \text{RAVI}(t) \leq \theta
\end{cases}
$$

where $\theta$ is the threshold (default 3.0%). The threshold line is plotted as a reference but is not part of the indicator's computation. Different markets and timeframes require different thresholds. Chande's 3% was calibrated for daily US equity data.

### 6. Complexity

- **Time:** O(1) per bar (two running-sum updates + one division + one absolute value). No loops, no square roots, no exponentials.
- **Space:** O($N_s + N_l$) for the two circular buffers. With defaults: $7 + 65 = 72$ doubles.
- **Warmup:** $N_l$ bars (the long SMA must fill completely). With default $N_l = 65$, the first valid RAVI appears on bar 65.
- **State footprint:** Two circular buffers ($N_s + N_l$ doubles), two running sums, two fill counters.

## Mathematical Foundation

### RAVI Derivation

Given a price series $\{C_0, C_1, \ldots, C_t\}$, the RAVI at bar $t$ with short period $N_s$ and long period $N_l$ is:

$$
\text{RAVI}(t) = \left| \frac{\text{SMA}(C, N_s, t) - \text{SMA}(C, N_l, t)}{\text{SMA}(C, N_l, t)} \right| \times 100
$$

Expanding the SMA definitions:

$$
\text{RAVI}(t) = \left| \frac{\frac{1}{N_s}\sum_{i=0}^{N_s-1} C_{t-i} - \frac{1}{N_l}\sum_{i=0}^{N_l-1} C_{t-i}}{\frac{1}{N_l}\sum_{i=0}^{N_l-1} C_{t-i}} \right| \times 100
$$

Simplifying:

$$
\text{RAVI}(t) = \left| \frac{N_l \sum_{i=0}^{N_s-1} C_{t-i} - N_s \sum_{i=0}^{N_l-1} C_{t-i}}{N_s \sum_{i=0}^{N_l-1} C_{t-i}} \right| \times 100
$$

### Bounds Analysis

**Lower bound:** When $\text{SMA}_s = \text{SMA}_l$ (price is flat or symmetrically oscillating), RAVI = 0.

**Upper bound:** RAVI has no theoretical upper bound. If the short SMA diverges sufficiently from the long SMA (e.g., a parabolic move), RAVI grows without limit. In practice, for typical equity data, RAVI values above 10% are rare and above 20% are extreme.

**Typical range:** For daily equity data with default parameters, RAVI typically oscillates between 0% and 8%. Strongly trending markets (sustained directional moves over several weeks) produce values of 5-10%. Choppy sideways markets produce values below 2%.

### Relationship to MACD

RAVI is structurally related to the Percentage Price Oscillator (PPO), which computes:

$$
\text{PPO}(t) = \frac{\text{EMA}_s(t) - \text{EMA}_l(t)}{\text{EMA}_l(t)} \times 100
$$

RAVI uses SMA instead of EMA, and takes the absolute value. PPO preserves sign and direction. If you replaced the SMAs with EMAs and dropped the absolute value, RAVI would become PPO.

### Relationship to VIDYA

VIDYA uses a ratio of short-term to long-term standard deviations to adapt its smoothing constant. RAVI uses a ratio of short-term to long-term price levels (via SMA) to measure trend presence. Both indicators reflect Chande's philosophy of comparing short-horizon behavior against long-horizon behavior, but they answer different questions: VIDYA asks "how volatile is price right now?" while RAVI asks "how far has price moved from its long-term average?"

### Parameter Mapping

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N_s$ | shortPeriod | 7 | $N_s \geq 1$ |
| $N_l$ | longPeriod | 65 | $N_l > N_s$ |
| $\theta$ | threshold | 3.0% | $\theta \geq 0$ (display only) |

| Short | Long | Ratio | Warmup | Sensitivity | Best For |
|-------|------|-------|--------|-------------|----------|
| 7 | 65 | 1:9.3 | 65 bars | Standard | Daily equity, Chande's original |
| 5 | 50 | 1:10 | 50 bars | Higher | Faster response, more noise |
| 10 | 100 | 1:10 | 100 bars | Lower | Weekly charts, long-term trends |
| 3 | 30 | 1:10 | 30 bars | High | Intraday, scalping |

Chande's rule of thumb: long period = quarterly equivalent for your timeframe; short period = 10% of long period, rounded to nearest integer.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations with circular buffers for both SMAs:

| Operation | Count | Cost (cycles) | Subtotal |
|:----------|:-----:|:-------------:|:--------:|
| SUB (remove oldest from running sum) | 2 | 1 | 2 |
| ADD (add current to running sum) | 2 | 1 | 2 |
| DIV (running sum / period, x2) | 2 | 15 | 30 |
| SUB (SMA_short - SMA_long) | 1 | 1 | 1 |
| DIV (normalize by SMA_long) | 1 | 15 | 15 |
| MUL (scale by 100) | 1 | 3 | 3 |
| ABS (absolute value) | 1 | 1 | 1 |
| **Total** | **10** | | **~54 cycles** |

RAVI is one of the cheapest indicators in the dynamics category. For comparison, ADX requires approximately 200+ cycles per bar, and PFE requires ~191 cycles per bar (for period=10). RAVI's 54 cycles makes it roughly 4x cheaper than either.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
|:----------|:-------------:|:------|
| Running sum update (short) | Yes | Prefix sum, then subtract lagged prefix sum |
| Running sum update (long) | Yes | Same pattern, different lag |
| Division (SMA computation) | Yes | VDIVPD, 4 doubles per op |
| Subtraction (SMA_s - SMA_l) | Yes | VSUBPD |
| Division (normalization) | Yes | VDIVPD |
| Absolute value | Yes | VANDPD with sign-bit mask |
| Multiply by 100 | Yes | VMULPD |

The entire `Calculate(Span)` pipeline is fully vectorizable. Both SMA computations can use the prefix-sum trick: compute a cumulative sum of the input, then $\text{SMA}(t) = (\text{prefix}[t] - \text{prefix}[t - N]) / N$. This transforms the two O($N$) naive loops into O(1) per element with a single O($n$) prefix-sum pass.

With AVX2 processing 4 doubles per instruction, the batch path achieves near-4x speedup over scalar for large arrays. No sequential dependencies exist in the final RAVI computation once both SMA arrays are materialized.

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | Exact arithmetic, no approximations, no recursive state |
| **Timeliness** | 5/10 | Long SMA ($N_l = 65$) introduces substantial lag; trend detection is delayed |
| **Smoothness** | 8/10 | SMA inherently smooth; no jitter from recursive feedback |
| **Noise Rejection** | 6/10 | SMA provides linear filtering but no adaptive bandwidth |
| **Interpretability** | 9/10 | Single percentage value with clear threshold; binary trending/ranging classification |

## Validation

| Library | Status | Notes |
|:--------|:------:|:------|
| **TA-Lib** | N/A | Not implemented in TA-Lib |
| **Skender** | N/A | Not available in Skender.Stock.Indicators |
| **Tulip** | N/A | Not implemented in Tulip Indicators |
| **OoplesFinance** | Pending | May be available; check `RangeActionVerificationIndex` |
| **Wealth-Lab** | Reference | WL5 Wiki documents RAVI with SMA/EMA option + absolute/signed option |
| **MetaTrader** | Reference | MQL5 Code Base implementations available; SmoothAlgorithms.mqh version |
| **NanoTrader** | Reference | Built-in RAVI with configurable threshold |
| **Sierra Chart** | Caution | Sierra Chart's "RAVI" is a different indicator (Rapid Adaptive Variance) using VIDYA |

Key validation points:

- For a constant price series (all closes identical), RAVI must equal exactly 0
- For a monotonically increasing series with constant increment, RAVI must be positive and stable after warmup
- RAVI must always be non-negative (absolute value constraint)
- With $N_s = N_l$, RAVI must equal 0 for all bars (same SMA)
- Warmup: first $N_l - 1$ bars produce NaN
- Division guard: if SMA_long = 0, output NaN (avoid division by zero)
- RAVI is symmetric: a market that rises X% and then falls X% back to start produces approximately equal RAVI values during both phases

## Common Pitfalls

1. **Confusing Chande's RAVI with Sierra Chart's RAVI.** Sierra Chart documents a "Rapid Adaptive Variance Indicator" that uses VIDYA internally. It shares the RAVI acronym but is a completely different indicator with different inputs, computation, and interpretation. Using Sierra Chart's formula when Chande's is intended (or vice versa) produces entirely unrelated output. Always verify which RAVI definition your platform implements.

2. **Using a fixed 3% threshold across all markets.** Chande's 3% threshold was calibrated for daily US equity data. Forex pairs with 0.5% daily ranges need thresholds of 0.1-0.3%. Crypto assets with 5-10% daily ranges may need thresholds of 8-15%. A fixed threshold misclassifies regime in roughly 30-50% of markets.

3. **Preserving sign instead of taking absolute value.** Some implementations skip the absolute value, producing a signed indicator where positive means "short MA above long MA" and negative means "short MA below long MA." This changes RAVI from a trend-strength indicator into a trend-direction indicator. Both interpretations have value, but mixing them in code that expects the other convention produces incorrect regime classification.

4. **Using EMA instead of SMA.** Wealth-Lab and some other platforms offer EMA as an alternative. EMA responds faster but introduces exponential decay, changing the effective lookback characteristics. The long EMA never fully forgets old data (IIR behavior), while the long SMA has a hard cutoff at $N_l$ bars (FIR behavior). For RAVI's threshold-based classification, this difference shifts the optimal threshold by 10-20% and changes the warmup characteristics.

5. **Setting short and long periods too close together.** Chande's 10:1 ratio (7:65) provides clear separation between timeframes. A 2:1 ratio (e.g., 30:60) means both SMAs respond to similar frequencies, and RAVI stays near zero even during trends. The indicator loses discriminating power. Maintain at least a 5:1 ratio between long and short periods.

6. **Expecting RAVI to indicate trend direction.** RAVI's absolute value explicitly discards direction. A strong uptrend and a strong downtrend produce the same RAVI value. If direction matters, use RAVI in conjunction with a directional indicator (the sign of the short-long SMA difference, a simple price-above-MA test, or MACD).

7. **Ignoring the warmup period.** RAVI requires $N_l$ bars (65 by default) before producing a valid reading. During warmup, the long SMA is undefined. Some implementations return 0 during warmup, which falsely signals a ranging market. Return NaN until the long SMA buffer is full.

## References

- Chande, Tushar S. *Beyond Technical Analysis: How to Develop and Implement a Winning Trading System*. 2nd Edition. John Wiley & Sons, 2001. ISBN: 0471415677. Chapter on RAVI, pp. 66-70.
- Chande, Tushar S. "Adapting Moving Averages to Market Volatility." *Stocks & Commodities*, V10:3, 1992. pp. 108-114. (VIDYA introduction; RAVI is the companion trend classifier.)
- Chande, Tushar S., and Kroll, Stanley. *The New Technical Trader: Boost Your Profit by Plugging into the Latest Indicators*. John Wiley & Sons, 1994. ISBN: 0471597805.
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978. (ADX reference for comparison.)
- PineScript reference: `ravi.pine` in indicator directory.
