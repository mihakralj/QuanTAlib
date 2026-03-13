# CHOP: Choppiness Index

> *Choppiness index quantifies how range-bound a market is — high values mean sideways, low values mean trending.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (CHOP)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [chop.pine](chop.pine)                       |

- The Choppiness Index is a non-directional regime indicator that measures whether the market is trending or trading sideways.
- **Similar:** [ADX](../adx/Adx.md), [VHF](../vhf/Vhf.md) | **Complementary:** BBands for range boundaries | **Trading note:** Choppiness Index; high values = choppy/ranging, low values = trending. Range 0–100.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Choppiness Index is a non-directional regime indicator that measures whether the market is trending or trading sideways. It compares total price movement (sum of True Range) to net price movement (high-low channel width) using a logarithmic ratio, producing a bounded value where high readings indicate choppy/consolidating conditions and low readings indicate trending conditions. CHOP does not indicate direction — only whether directional strategies are likely to succeed. The logarithmic scaling normalizes the output to approximately 0-100 regardless of price level or volatility magnitude.

## Historical Context

Australian commodity trader E.W. Dreiss created the Choppiness Index to help traders avoid whipsaw losses by identifying market conditions unsuitable for trend-following strategies. The core insight is geometric: in a perfect trend, total bar-by-bar movement (sum of True Range) roughly equals the net distance traveled (channel width). In a choppy market, total movement greatly exceeds net progress — the market thrashes back and forth, accumulating True Range while the net channel stays narrow. The ratio between these two quantities, log-scaled to normalize across instruments and timeframes, produces a clean regime classifier. The conventional thresholds (38.2 and 61.8) are deliberately chosen as Fibonacci levels, though their efficacy is empirical rather than mathematical.

## Architecture & Physics

### 1. True Range Accumulation

$$TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

A rolling sum maintains $\sum_{i=1}^{N} TR_i$ over the lookback window.

### 2. Price Channel Width

The net price movement over the same window:

$$\text{Channel} = \max(H_{t-N+1:t}) - \min(L_{t-N+1:t})$$

### 3. Choppiness Index

$$\text{CHOP} = 100 \times \frac{\log_{10}\!\left(\dfrac{\sum TR_N}{\text{Channel}}\right)}{\log_{10}(N)}$$

The denominator $\log_{10}(N)$ normalizes the output so that the theoretical maximum approaches 100 (when $\sum TR = N \times \text{Channel}$, which occurs when every bar traverses the full channel).

### 4. Complexity

- **Time:** $O(N)$ per bar for min/max scanning of high/low buffers; rolling sum is $O(1)$
- **Space:** $O(N)$ — three ring buffers (TR, highs, lows)
- **Warmup:** $N$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Interpretation

| CHOP Value | Market Regime | Strategy Implication |
|------------|---------------|---------------------|
| > 61.8 | High choppiness | Avoid trend-following; favor range strategies |
| 38.2 - 61.8 | Ambiguous | Mixed conditions; reduced position sizing |
| < 38.2 | Low choppiness | Market trending; favor momentum/breakout strategies |

### Geometric Intuition

- **Perfect trend (straight line):** $\sum TR \approx \text{Channel}$, so $\log_{10}(1) = 0$, CHOP $\to 0$
- **Maximum chop (full traversal every bar):** $\sum TR \approx N \times \text{Channel}$, so $\log_{10}(N) / \log_{10}(N) = 1$, CHOP $\to 100$

### Non-Directional Property

CHOP is completely direction-agnostic. A strong uptrend and a strong downtrend produce identical low CHOP readings. Direction must be determined by a separate indicator (AMAT, ADX directional components, or simple price comparison).

## Performance Profile

### Operation Count (Streaming Mode)

CHOP needs True Range sum over N bars (running sum from RingBuffer) and ATR-N (highest high minus lowest low over N bars).

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| TR computation (SUB×3, ABS×2, MAX×2) | 7 | 1 | 7 |
| RingBuffer write + oldest sub (TR sum) | 2 | 1 | 2 |
| Deque update × 2 (high/low window extrema) | 4 | 1 | 4 |
| SUB (highest_high − lowest_low = range) | 1 | 1 | 1 |
| DIV (TR_sum / range) | 1 | 15 | 15 |
| LOG10 (normalize to period) | 1 | 20 | 20 |
| DIV (scale by log10(N)) | 1 | 15 | 15 |
| MUL (scale to 100) | 1 | 3 | 3 |
| **Total** | **18** | — | **~67 cycles** |

For default $N=14$: ~67 cycles per bar. The LOG10 call is the dominant cost.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR computation | Yes | VSUBPD + VABSPD + VMAXPD per bar |
| Prefix-sum TR | Partial | Inclusive prefix sum with SIMD subtract-lag |
| Sliding high/low extrema | Partial | Lemire deque or sparse table; ArgMax/ArgMin scan |
| LOG10 + scaling | Yes | SVML vlog10 or Taylor approx; scalar fallback |

With AVX2 and Intel SVML for vectorized log, batch mode achieves ~3× throughput for large datasets.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | LOG10 precision sufficient; FMA could be applied to TR computation |
| **Timeliness** | 6/10 | N-bar lookback; instantaneous response to volatility regime changes |
| **Smoothness** | 5/10 | Raw ratio is noisy; often used with EMA smoothing externally |
| **Noise Rejection** | 6/10 | Logarithmic scaling reduces extreme value sensitivity |

## Resources

- Dreiss, E.W. — Choppiness Index (original development)
- PineScript reference: `chop.pine` in indicator directory