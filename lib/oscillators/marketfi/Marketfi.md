# MARKETFI: Market Facilitation Index

> *Price moves in an empty room; volume tells you how many people showed up.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (MARKETFI)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `> 1` bars                          |
| **PineScript**   | [marketfi.pine](marketfi.pine)                       |

- The Market Facilitation Index answers a single question with arithmetic directness: how much price moved per unit of volume traded?
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Market Facilitation Index answers a single question with arithmetic directness: how much price moved per unit of volume traded? One division. No lookback period. No smoothing. No parameter to debate. What you get is raw market efficiency — the price range a market delivers for each unit of liquidity consumed.

Bill Williams introduced BW MFI in *Trading Chaos* (1995) as part of his Profitunity trading system, alongside the Awesome Oscillator and Accelerator Oscillator. His central insight was that price and volume carry independent signals, and only their *combination* reveals whether a trend has genuine participation. A wide range bar on thin volume suggests ease of movement but not conviction. A narrow range bar on heavy volume suggests absorption — large players defending a level.

## Historical Context

Williams drew on the earlier work of Mark Chakin, Jesse Livermore's tape-reading intuitions, and his own experience in commodities markets to argue that most technical indicators analyze price alone — a single dimension of a fundamentally two-dimensional market. Volume, he argued, is the missing dimension. The MFI is his most direct formulation of that intuition: strip everything away until you have a pure ratio.

The index predates modern market microstructure theory but anticipated it. Economists later formalized the Kyle lambda (price impact per unit of order flow) and Amihud's illiquidity ratio, both of which are statistical close relatives of MFI's per-bar instantiation. Williams was doing microstructure analysis with a pocket calculator thirty years before the term was fashionable.

Two objections are legitimate. First, raw volume is not normalized across instruments or time — a bitcoin MFI reading is incommensurable with a T-bill MFI reading. Second, the formula is sensitive to bar granularity: the same market on 1-minute bars versus daily bars produces categorically different MFI values on the same trade. Williams intended daily bars. Users who apply MFI to intraday data are extrapolating beyond its design envelope.

## Architecture & Physics

### 1. Formula

$$\text{MARKETFI} = \frac{\text{High} - \text{Low}}{\text{Volume}}$$

**Zero-volume guard:** When `Volume = 0`, the result is `0.0`. Dividing by zero is undefined; returning `0.0` is the correct semantic choice — if no trades occurred, the market delivered zero facilitation per trade unit.

### 2. State

Unlike period-based indicators, MARKETFI carries essentially no state. The computation is stateless per bar. The implementation stores only `LastValid` (for NaN substitution) and `Count` (for `IsHot`). No ring buffers, no warmup counters beyond the first bar.

```
State = { LastValid: double, Count: int }
```

`IsHot` fires on bar 1. `WarmupPeriod = 1`.

### 3. NaN / Infinity Handling

| Input | Behavior |
|-------|---------|
| `NaN` High or Low | Substituted with `LastValid`; result uses substituted values |
| `NaN` or `Infinity` Volume | Treated as `0` → result = `0.0` |
| Result `NaN` or `Infinity` | Substituted with `LastValid` |

### 4. Bar Correction (`isNew = false`)

The `isNew` parameter follows the standard QuanTAlib rollback contract. On `isNew = true`, `_ps = _s` is saved. On `isNew = false`, `_s = _ps` is restored before recomputing. Since MARKETFI state is entirely scalar (no buffers), rollback is a single struct copy — the cheapest possible correction.

## Mathematical Foundation

$$\text{MARKETFI}_t = \frac{H_t - L_t}{V_t}, \quad V_t > 0$$

This is equivalent to the per-share price impact in a simplified zero-latency model. In Kyle's framework, the price impact $\lambda$ satisfies:

$$\Delta P = \lambda \cdot Q$$

where $Q$ is order flow. MARKETFI inverts this: given $\Delta P = H - L$ (range as proxy for price impact), and $V$ as volume, $\text{MARKETFI} = \Delta P / V \approx \lambda$ at bar granularity.

The four-quadrant interpretation Williams used:

| MFI vs Previous | Volume vs Previous | Quadrant | Interpretation |
|-----------------|-------------------|----------|----------------|
| ↑ | ↑ | Green | Trend acceleration — price and volume agree |
| ↑ | ↓ | Fade | Price moves easily; volume not confirming |
| ↓ | ↑ | Squat | Volume absorbed; breakout pending (brown) |
| ↓ | ↓ | Fake | No trend, no volume — false move likely |

QuanTAlib computes the raw scalar index. The four-quadrant coloring requires comparing current bar against prior bar — implementable in the Quantower adapter or downstream.

## Performance Profile

| Aspect | Detail |
|--------|--------|
| Time complexity | O(1) per bar |
| Space complexity | O(1) — 2 scalar fields |
| Allocations (hot path) | 0 |
| SIMD applicable | No — single division, no vectorizable loop |
| FMA applicable | No — no `a*b + c` pattern |
| Warmup bars | 1 |
| Buffer | None |

### Operation Count (Streaming Mode)

MarketFi is pure O(1): a single division of range by volume, no lookback or state.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Range = High - Low | 1 | 1 cy | ~1 cy |
| MFI = Range / Volume | 1 | 5 cy | ~5 cy |
| Zero-volume guard | 1 | 1 cy | ~1 cy |
| **Total** | **O(1)** | — | **~7 cycles** |

Absolute minimum complexity — one subtraction and one division per bar. No circular buffers, no warmup, no state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Range = High - Low | Yes | `Vector<double>` subtract |
| MFI = Range / Volume | Yes | `Vector` divide with zero-guard mask |

Fully vectorizable — both operations are elementwise with no data dependencies across bars.

**Operation count per bar:** 1 subtraction + 1 division + 1 comparison (zero guard) = 3 FP ops. This is the minimum possible for any meaningful price indicator.

**Quality metrics (1–10):**

| Dimension | Score | Note |
|-----------|-------|------|
| Mathematical precision | 9 | Exact division — no approximation |
| Interpretability | 7 | Simple ratio; requires context to act on |
| Lag | 10 | Zero lag — pure current-bar measure |
| Noise sensitivity | 4 | Sensitive to outlier bars; no smoothing |
| Volume dependency | — | Requires real volume data; meaningless on synthetic feeds |

## Validation

No external library (TA-Lib, Skender.Stock.Indicators, Tulip, OoplesFinance) implements the Market Facilitation Index. Validation is via:

| Test | Method |
|------|--------|
| Identity | `(H - L) / V` matches direct computation for known inputs |
| Scaling | Doubling volume halves MFI; doubling range doubles MFI |
| Batch == Streaming | All bars agree to 1e-10 |
| Determinism | Two instances with same seed produce identical output |
| Non-negativity | `H >= L` always → `MFI >= 0` always |
| Zero-volume guard | Volume=0 → MFI=0 for all range values |

## Common Pitfalls

1. **Zero volume on data gaps.** Many data providers fill weekend or holiday bars with `Volume = 0`. These produce `MFI = 0`, which is correct but may be misread as a "squat" signal. Guard your data feed or filter these bars upstream.

2. **Tick data vs bar data.** MFI on tick bars is nonsensical — range is nearly always nonzero, volume is always 1. Use OHLCV bars with meaningful aggregation periods.

3. **Cross-instrument comparison.** A crude oil MFI of 0.0001 and a gold MFI of 0.00003 say nothing relative to each other. Normalize by ATR or recent MFI average before comparing instruments.

4. **Intraday granularity.** Williams calibrated BW MFI on daily commodity bars. Applying it to 1-minute equity bars produces different distributional properties. Signals may not transfer.

5. **Treating MFI alone as a signal.** Williams explicitly used MFI in conjunction with AO and AC. Raw MFI without the quadrant comparison (prior bar) loses most of its analytical content.

6. **Volume data quality.** Crypto exchanges report volume in base currency; futures report contracts; equities report shares. Ensure units are consistent within a single instrument's time series before trusting MFI readings.

## References

- Williams, Bill (1995). *Trading Chaos: Applying Expert Techniques to Maximize Your Profits.* Wiley.
- Williams, Bill and Justine Gregory-Williams (2004). *Trading Chaos: Maximize Profits with Proven Technical Techniques* (2nd ed.). Wiley.
- Kyle, Albert S. (1985). "Continuous Auctions and Insider Trading." *Econometrica*, 53(6), 1315–1335.
- Amihud, Yakov (2002). "Illiquidity and stock returns: cross-section and time-series effects." *Journal of Financial Markets*, 5(1), 31–56.