# DEM: DeMarker Oscillator

> *The trend is your friend — right up until DeMark starts counting against it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Dem)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [dem.pine](dem.pine)                       |

- DEM (DeMarker Oscillator) is a bounded [0, 1] momentum oscillator that measures sequential demand pressure by comparing each bar's high and low aga...
- Parameterized by `period` (default 14).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

DEM (DeMarker Oscillator) is a bounded [0, 1] momentum oscillator that measures sequential demand pressure by comparing each bar's high and low against the previous bar's high and low. It isolates bullish demand momentum in the numerator and bearish supply pressure in the denominator, then normalizes their ratio with SMA smoothing over a configurable period. Values near 0.7 signal overbought exhaustion; values near 0.3 signal oversold exhaustion. Neither external library in common use (TA-Lib, Skender, Tulip, Ooples) implements DeMarker, so self-consistency tests against batch/streaming/span modes serve as the primary validation.

## Historical Context

Tom DeMark introduced the indicator in his 1994 book *The New Science of Technical Analysis*, published by John Wiley & Sons. DeMark's central thesis was that standard momentum indicators like RSI conflate bar-level price structure with inter-bar price continuation, producing a muddied signal. His fix was surgical: extract only the directional component of each bar by asking specifically whether the current bar's extreme extended beyond the prior bar's corresponding extreme.

The comparison is asymmetric by design. DeMax measures whether buyers pushed today's high above yesterday's high — pure buying initiative. DeMin measures whether sellers pushed today's low below yesterday's low — pure selling initiative. Bars where today's range falls entirely inside yesterday's range contribute zero to both, leaving the rolling SMA unchanged. This innards-of-the-range filtering is what distinguishes DEM from RSI, which responds to close-to-close changes and therefore blurs intrabar range dynamics with inter-session momentum.

DeMark's original publication discussed the oscillator in the context of his broader market timing research, which emphasized exhaustion patterns, sequential countdown structures (TD Sequential), and supply/demand imbalances. The 14-bar default period mirrors RSI's universal default, making side-by-side comparison natural. DEM tends to lead RSI at local turning points because it responds to bar-level range extensions rather than net close-to-close displacement.

The oscillator's bounded [0, 1] output — rather than RSI's [0, 100] — is a matter of convention. Some platforms scale DEM to [0, 100] by multiplying by 100. This implementation uses [0, 1] throughout, consistent with the normalized ratio form from DeMark's original derivation.

## Architecture and Physics

### 1. Per-Bar Demand Extraction

On each bar, two non-negative scalars are computed:

$$
\text{DeMax}_i = \max(H_i - H_{i-1},\; 0)
$$

$$
\text{DeMin}_i = \max(L_{i-1} - L_i,\; 0)
$$

DeMax is positive when today's high exceeded yesterday's high — buyers extended the range. DeMin is positive when today's low undercut yesterday's low — sellers extended the range. If neither condition holds (inside bar), both contributions are zero.

The `max(0, ...)` clamp is load-bearing: it prevents inside bars from creating phantom negative pressure in the running sums. Inside bars carry no directional information in DeMark's framework.

### 2. SMA Smoothing via Circular Buffers

Both DeMax and DeMin are smoothed over $N$ bars by simple moving average. The implementation maintains two circular buffers of size $N$ with O(1) running sums:

| Buffer | Contents | Running Sum |
| :--- | :--- | :--- |
| `deMaxBuf` | $\text{DeMax}_i$ per bar | SMA numerator sum |
| `deMinBuf` | $\text{DeMin}_i$ per bar | SMA denominator sum |

On each new bar: subtract the outgoing slot value from the running sum, write the new value to the slot, add the new value to the running sum, advance the index modulo $N$. Cost: 2 subtractions + 2 additions + 2 array writes per bar regardless of period.

$$
\overline{\text{DeMax}}_t = \frac{1}{N} \sum_{i=t-N+1}^{t} \text{DeMax}_i
$$

$$
\overline{\text{DeMin}}_t = \frac{1}{N} \sum_{i=t-N+1}^{t} \text{DeMin}_i
$$

### 3. DEM Ratio and Division Guard

$$
\text{DEM}_t = \frac{\overline{\text{DeMax}}_t}{\overline{\text{DeMax}}_t + \overline{\text{DeMin}}_t}
$$

When the denominator is zero (flat market or inside-bar sequence contributing nothing to either SMA), the output falls back to 0.5 — the neutral midpoint. This is the mathematically correct neutral state: zero demand pressure and zero supply pressure are indistinguishable from equilibrium.

### 4. Warmup Semantics

DEM requires `period + 1` bars before the first valid output. The first bar establishes `prevHigh` and `prevLow` with no DeMax/DeMin contribution (bootstrap). The following `period` bars fill the circular buffer. `IsHot` flips to `true` after `period + 1` bars have been processed.

## Mathematical Foundation

### Parameter Mapping

| Parameter | Symbol | Default | Range | Description |
| :--- | :---: | :---: | :--- | :--- |
| Period | $N$ | 14 | $[1, 5000]$ | SMA smoothing window |

### Range Proof

**Claim:** $\text{DEM}_t \in [0, 1]$ always (excluding the zero-denominator guard, which returns exactly 0.5).

**Proof:** Both running sums are non-negative by construction (`max(0, ...)` clamps). The numerator $\overline{\text{DeMax}}_t \geq 0$ and the denominator $\overline{\text{DeMax}}_t + \overline{\text{DeMin}}_t \geq \overline{\text{DeMax}}_t$. Therefore the ratio is at most 1. Since the numerator is non-negative and the denominator is at least as large, the ratio is at least 0. QED.

### Relationship to RSI

RSI computes a ratio of average gains to average gains plus average losses over close-to-close differences:

$$
\text{RSI}_t = \frac{\overline{U}_t}{\overline{U}_t + \overline{D}_t}
$$

where $U_i = \max(C_i - C_{i-1}, 0)$ and $D_i = \max(C_{i-1} - C_i, 0)$.

DEM substitutes bar-level range extensions for close-to-close differences:

$$
\text{DEM}_t = \frac{\overline{\text{DeMax}}_t}{\overline{\text{DeMax}}_t + \overline{\text{DeMin}}_t}
$$

Both are normalized ratios with the same algebraic structure. DEM's advantage at turning points is that inside bars — which RSI treats as momentum continuation if the close is unchanged — contribute zero to DEM, reducing response to consolidation noise.

### Z-Domain Transfer Function

The SMA stage has transfer function:

$$
H(z) = \frac{1}{N} \cdot \frac{1 - z^{-N}}{1 - z^{-1}}
$$

This produces $N-1$ zeros on the unit circle at angles $2\pi k/N$ for $k = 1, \ldots, N-1$, suppressing all harmonics of the fundamental $1/N$ cycle. The DEM ratio is then a nonlinear combination of two FIR-filtered series.

## Performance Profile


### Operation Count (Streaming Mode)

DeMarker compares high/low extremes vs prior bar to build smoothed directional sums.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB × 2 (DeMax, DeMin raw) | 2 | 1 | 2 |
| MAX × 2 (clip to 0) | 2 | 1 | 2 |
| FMA × 2 (SMA/EMA smooth DeMax, DeMin) | 2 | 4 | 8 |
| DIV (DeMax / (DeMax + DeMin)) | 1 | 15 | 15 |
| CMP (div-by-zero guard) | 1 | 1 | 1 |
| **Total** | **8** | — | **~28 cycles** |

~28 cycles per bar. O(1) EMA smoothing on two running values.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| DeMax / DeMin computation | Yes | VSUBPD + VMAXPD (clip to 0) |
| EMA smoothing × 2 | **No** | Recursive IIR — sequential |
| Division | Yes | VDIVPD after EMA passes |

| Operation | Cost | Notes |
| :--- | :---: | :--- |
| Per-bar `Update` | O(1) | 2 buffer reads + 2 subtractions + 2 additions + 2 writes |
| `Batch(Span)` | O(n) | Stackalloc for period ≤ 256, ArrayPool otherwise |
| Memory | O(period) | Two `double[]` buffers + two snapshot arrays |
| Snapshot/restore | O(period) | `Array.Copy` for both buffers on isNew=true |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| Smoothness | 6/10 | SMA introduces lag proportional to period |
| Responsiveness | 7/10 | Range extensions visible before close confirms |
| Noise rejection | 6/10 | Inside bars contribute zero — selective filtering |
| SMA lag | 7 bars | At period=14, approximate half-period lag |

## Validation

No external library in the QuanTAlib test suite implements the DeMarker Oscillator:

| Library | DEM Support | Notes |
| :--- | :---: | :--- |
| TA-Lib (TALib.NETCore) | No | Has DEMA (Double EMA) — different indicator |
| Skender.Stock.Indicators | No | Not implemented |
| Tulip (Tulip.NETCore) | No | Not implemented |
| OoplesFinance | No | Not implemented |

Validation relies on self-consistency checks:

| Test | Method | Result |
| :--- | :--- | :--- |
| Streaming == Batch | Compare `Update` loop vs `Batch(Span)` | Match to 1e-12 |
| Constant price → 0.5 | Flat market, zero DeMax + DeMin | 0.5 exact |
| All rising → 1.0 | Strictly rising highs, flat lows | 1.0 exact |
| All falling → 0.0 | Flat highs, strictly falling lows | 0.0 exact |
| Math identity | Manual SMA recompute matches batch | Match to 1e-9 |
| Range bound | 500 GBM bars, all periods | All output in [0, 1] |

## Common Pitfalls

1. **Period sensitivity at extremes.** Period=1 produces binary output (0, 0.5, or 1.0 only), since a single bar's DeMax and DeMin directly determine the ratio. This is technically correct but produces a step function useless for trend identification. Periods below 5 are noisy in practice.

2. **Misidentifying DEMA as DEM.** TA-Lib contains a function named DEMA — this is the *Double Exponential Moving Average*, not the DeMarker Oscillator. The abbreviation collision has caused real confusion in the wild. Searching "DEMA" in financial code almost always returns the wrong indicator.

3. **Inside-bar sequences produce 0.5.** When the market consolidates inside a narrow range for an extended period, DeMax and DeMin both accumulate to zero. The output locks at 0.5 indefinitely. This is correct behavior, not a bug. It means "no directional information available," not "equilibrium between bulls and bears."

4. **Divergence signal timing differs from RSI.** DEM divergences tend to form 1–3 bars earlier than RSI divergences on the same data because DeMax/DeMin capture intrabar range extensions before those extensions appear in the closing price. Traders accustomed to RSI divergence timing need to adjust lookback windows.

5. **The 0.3/0.7 thresholds are not universal.** DeMark's original publication cited these levels, but they were calibrated for daily data on equity indices. Intraday futures data with frequent gap-opens will have different statistical distributions. Empirical threshold calibration per instrument is generally necessary.

6. **Warmup period is period+1, not period.** The first bar cannot contribute a DeMax or DeMin because there is no prior bar to compare against. Consumers who assume warmup equals period will have an off-by-one error in `IsHot` checks. The `WarmupPeriod` property returns `period + 1`.

7. **Flat open without a gap.** When `High[i] == High[i-1]` and `Low[i] == Low[i-1]` (exact repeat bar), the indicator produces zero for both components. This is not a degenerate case — it is a correctly priced inside bar contributing zero directional information.

## References

- DeMark, Tom (1994). *The New Science of Technical Analysis*. John Wiley & Sons. ISBN 0-471-03548-3.
- DeMark, Tom (1997). *New Market Timing Techniques*. John Wiley & Sons. ISBN 0-471-14970-5.
- Colby, Robert W. (2003). *The Encyclopedia of Technical Market Indicators* (2nd ed.). McGraw-Hill. Entry: "DeMark Indicators."
- Pring, Martin J. (2002). *Technical Analysis Explained* (4th ed.). McGraw-Hill. Chapter on oscillator construction methodology.
