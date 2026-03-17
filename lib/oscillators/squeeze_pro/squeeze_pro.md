# SQUEEZE_PRO: LazyBear's Squeeze Pro

> *Standard Squeeze uses one Keltner width. Squeeze Pro adds two more — because the market doesn't only compress one way.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (20), `bbMult` (2.0), `kcMultWide` (2.0), `kcMultNormal` (1.5), `kcMultNarrow` (1.0), `momLength` (12), `momSmooth` (6), `useSma` (true)                      |
| **Outputs**      | Dual: Momentum (double) + SqueezeLevel (int 0–3)                       |
| **Output range** | Momentum: unbounded; SqueezeLevel: {0, 1, 2, 3}                     |
| **Warmup**       | `max(period, momLength + momSmooth)` bars                          |
| **PineScript**   | [squeeze_pro.pine](squeeze_pro.pine)                       |

- LazyBear's Squeeze Pro enhances the standard TTM Squeeze by replacing the single Keltner Channel width with three graduated Keltner widths (wide, normal, narrow), and substituting MOM+SMA smoothing for linear regression momentum.
- **Similar:** [SQUEEZE](../squeeze/Squeeze.md), [TTM_SQUEEZE](../../dynamics/ttm_squeeze/TtmSqueeze.md), [BBS](../bbs/Bbs.md) | **Complementary:** ATR, BB | **Trading note:** Level 3 (narrow) = tightest compression, expect explosive breakout. Level 0 = expansion phase.
- Cross-validated streaming vs batch and across SMA/EMA smoothing modes.

## Historical Context

LazyBear's Squeeze Pro appeared on TradingView as an enhanced version of John Carter's TTM Squeeze, addressing a fundamental limitation: the original Squeeze only uses a single Keltner Channel width, providing a binary "squeeze on/off" signal. In practice, volatility compression exists on a spectrum — a market can be lightly compressed (BB barely inside KC) or severely compressed (BB well inside even a narrow KC). The three-level classification captures this gradient: wide squeeze (initial compression), normal squeeze (significant compression), and narrow squeeze (extreme compression that often precedes the largest moves). The momentum component was simplified from Carter's linear regression approach to a straightforward MOM(close, n) smoothed by SMA or EMA, making the indicator more responsive and easier to interpret.

## Architecture & Physics

### Computational Stages

1. **SMA + Standard Deviation** (Bollinger Bands): Circular buffer with running sum and sum-of-squares for O(1) variance computation. BB upper/lower = SMA $\pm$ bbMult $\times$ StdDev.

2. **EMA + ATR via RMA** (Keltner Channels): A single EMA and ATR computation shared across all three KC widths. Only the multiplier differs:
   - KC Wide: EMA $\pm$ kcMultWide $\times$ ATR
   - KC Normal: EMA $\pm$ kcMultNormal $\times$ ATR
   - KC Narrow: EMA $\pm$ kcMultNarrow $\times$ ATR

3. **Squeeze classification:** Hierarchical check from tightest to widest:
   - Level 3 (narrow): BB inside KC_narrow
   - Level 2 (normal): BB inside KC_normal but not KC_narrow
   - Level 1 (wide): BB inside KC_wide but not KC_normal
   - Level 0 (off): BB outside KC_wide

4. **Momentum (MOM):** Simple momentum = close $-$ close\[momLength bars ago\]. Requires a circular buffer of `momLength` close values.

5. **Smooth MOM:** SMA or EMA of the raw momentum values over `momSmooth` period.

### Warmup Compensation

EMA and RMA stages use the $e = \beta^n$ warmup tracking with correction factor $c = 1/(1-e)$ to eliminate initial bias.

## Mathematical Foundation

**Bollinger Bands** (SMA + StdDev via running sums):

$$\mu = \frac{\Sigma x}{n}, \quad \sigma = \sqrt{\frac{\Sigma x^2}{n} - \mu^2}$$

$$BB_{upper} = \mu + m_{bb} \cdot \sigma, \quad BB_{lower} = \mu - m_{bb} \cdot \sigma$$

**Keltner Channel** (EMA + ATR):

$$EMA_t = \frac{\hat{E}_t}{1 - \beta^t}, \quad ATR_t = \frac{\hat{R}_t}{1 - \beta_r^t}$$

$$KC_{upper}^{(w)} = EMA + m_w \cdot ATR, \quad KC_{lower}^{(w)} = EMA - m_w \cdot ATR$$

where $w \in \{wide, normal, narrow\}$.

**Squeeze level:**

$$SqueezeLevel = \begin{cases} 3 & \text{if } BB \subset KC_{narrow} \\ 2 & \text{if } BB \subset KC_{normal} \setminus KC_{narrow} \\ 1 & \text{if } BB \subset KC_{wide} \setminus KC_{normal} \\ 0 & \text{otherwise (expansion)} \end{cases}$$

**Momentum:**

$$MOM_t = close_t - close_{t - momLength}$$

$$Momentum_t = SMA(MOM, momSmooth) \text{ or } EMA(MOM, momSmooth)$$

## Performance Profile

| Operation | Count per bar |
| --- | --- |
| ADD/SUB | ~20 |
| MUL | ~12 |
| DIV | 4 |
| CMP | 6 |
| SQRT | 1 |
| FMA | 8 |

Three circular buffers (`period` + `momLength` + `momSmooth`) with snapshot/rollback for bar correction. Memory: $O(period + momLength + momSmooth)$ per instance.

## Validation

| Library | Status | Notes |
| --- | --- | --- |
| pandas-ta | Algorithm reference | Verified algorithm from source |
| Self-consistency | ✅ Pass | Streaming = Batch = Eventing |
| Determinism | ✅ Pass | Same seed → identical output |

## Common Pitfalls

1. **KC multiplier ordering:** Ensure kcMultWide > kcMultNormal > kcMultNarrow for meaningful level classification. The algorithm works with any positive values, but inverted ordering produces unintuitive results.
2. **Momentum warmup:** First `momLength` bars produce MOM = 0 (no lagged close available). Full momentum accuracy requires `momLength + momSmooth` bars.
3. **SMA vs EMA smoothing:** SMA produces equal-weight smoothing (more stable); EMA gives more weight to recent momentum (more responsive). Both produce valid signals but differ numerically.
4. **Squeeze level vs squeeze state:** Level 0 doesn't mean "no squeeze ever happened" — it means BB is currently outside KC_wide (expansion phase). The transition from level 3→0 is the breakout signal.
5. **Memory footprint:** Three circular buffers plus three snapshot arrays. For very large `period`, ArrayPool is used automatically in batch mode.

## References

- LazyBear, "Squeeze Momentum Indicator [LazyBear]" (TradingView)
- pandas-ta `squeeze_pro` implementation (GitHub)
- John Carter, *Mastering the Trade* (2005) — original TTM Squeeze concept
