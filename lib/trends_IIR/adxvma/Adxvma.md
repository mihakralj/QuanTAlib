# ADXVMA: ADX Variable Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Adxvma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period * 2` bars                          |

### TL;DR

- ADXVMA is an adaptive IIR filter that uses the Average Directional Index (ADX) as its smoothing constant.
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires `period * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Use ADX to measure trend strength, then feed that measurement back as the smoothing constant. When the trend is strong, track fast. When it is not, stand still. The market tells you how much to listen."

ADXVMA is an adaptive IIR filter that uses the Average Directional Index (ADX) as its smoothing constant. When ADX is high (strong trend), the smoothing factor approaches 1.0 and the filter tracks price aggressively. When ADX is low (range-bound), the smoothing factor approaches 0.0 and the filter barely moves. This creates a moving average that automatically switches between responsive trend-following and noise-immune range-holding without external regime detection.

## Historical Context

ADXVMA combines two well-established concepts: Welles Wilder's ADX (1978) as a trend-strength measure, and the adaptive moving average framework pioneered by Perry Kaufman's AMA (1995). While Kaufman used an efficiency ratio (net displacement / total path) to adapt smoothing, ADXVMA substitutes ADX, which measures trend directionality through the divergence of positive and negative directional movement indicators.

The ADX-based adaptation has a practical advantage over efficiency-ratio methods: ADX responds to the consistency of directional movement, not just net displacement. A market that trends steadily but slowly produces high ADX but low efficiency ratio. Conversely, a market with a sharp one-bar spike produces high efficiency ratio but low ADX (because the spike is not sustained). For trend-following applications, the ADX criterion better matches the trading requirement of sustained directional moves.

The implementation uses Wilder's RMA (Recursive Moving Average, equivalent to EMA with $\alpha = 1/N$) for all internal smoothing components (TR, +DM, -DM, DX), with warmup compensation to produce valid output from the first bar. The warmup compensator $c = 1/(1-\beta^n)$ corrects the exponential bias during the initial transient, eliminating the need for a multi-bar initialization period.

## Architecture & Physics

### 1. True Range and Directional Movement

Per-bar computation of True Range (TR), Plus Directional Movement (+DM), and Minus Directional Movement (-DM) using Wilder's definitions.

### 2. Wilder's RMA with Warmup Compensation

Each of TR, +DM, -DM, and DX is smoothed using RMA ($\alpha = 1/N$). The warmup compensator tracks the decay factor $e = \beta^n$ and divides the raw exponential accumulation by $(1-e)$, providing unbiased estimates from bar 1.

### 3. ADX Computation

$$
\text{ADX} = \text{RMA}\left(\frac{|+DI - -DI|}{+DI + -DI} \times 100\right)
$$

### 4. Adaptive Smoothing

The ADX value is clamped to $[0, 100]$ and divided by 100 to produce a smoothing constant $sc \in [0, 1]$:

$$
\text{ADXVMA}_t = \text{ADXVMA}_{t-1} + sc \times (\text{source}_t - \text{ADXVMA}_{t-1})
$$

## Mathematical Foundation

**Directional indicators:**

$$
+DI = \frac{100 \cdot \text{RMA}(+DM, N)}{\text{RMA}(TR, N)}, \quad -DI = \frac{100 \cdot \text{RMA}(-DM, N)}{\text{RMA}(TR, N)}
$$

**Directional Index:**

$$
DX = \frac{100 \cdot |+DI - -DI|}{+DI + -DI}
$$

**ADX:**

$$
\text{ADX} = \text{RMA}(DX, N)
$$

**Adaptive output:**

$$
sc = \text{clamp}\left(\frac{\text{ADX}}{100}, 0, 1\right)
$$

$$
\text{ADXVMA}_t = \text{ADXVMA}_{t-1} + sc \cdot (x_t - \text{ADXVMA}_{t-1})
$$

**Effective time constant:** When ADX = 50, $sc = 0.5$, equivalent to an EMA with period 3. When ADX = 20, $sc = 0.2$, equivalent to period 9. When ADX = 80, $sc = 0.8$, equivalent to period 1.5.

**Default parameters:** `period = 14`, `minPeriod = 1`. Requires OHLC data for TR/DM computation.

**Pseudo-code (streaming):**

```
alpha = 1/period; beta = 1 - alpha

// RMA with warmup compensation for TR, +DM, -DM, DX
raw_tr  = raw_tr * beta + tr * alpha
e_tr   *= beta
comp_tr = raw_tr / (1 - e_tr)   // warmup-compensated

// ... same for +DM, -DM ...

+DI = 100 * comp_pdm / comp_tr
-DI = 100 * comp_ndm / comp_tr
DX  = 100 * |+DI - -DI| / (+DI + -DI)

raw_dx = raw_dx * beta + DX * alpha
ADX    = raw_dx / (1 - e_dx)

sc = clamp(ADX / 100, 0, 1)
result = result + sc * (source - result)
```

## Resources

- Wilder, J.W. (1978). *New Concepts in Technical Trading Systems*. Trend Research. Chapter 6: Directional Movement.
- Kaufman, P.J. (1995). *Smarter Trading*. McGraw-Hill. Chapter 7: Adaptive Techniques.
- Chande, T.S. (2001). *Beyond Technical Analysis*, 2nd ed. John Wiley & Sons.

## Performance Profile

### Operation Count (Streaming Mode)

ADXVMA(N) runs a full 4-RMA ADX pipeline internally, then uses the resulting ADX value as the EMA alpha. Each RMA update is one FMA. The adaptive VMA update is one additional FMA. Total: 5 EMA/RMA updates plus the TR/DM preprocessing.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| TR: max(H-L, |H-C₁|, |L-C₁|) | 5 | 1 | ~5 |
| +DM / -DM directional moves | 4 | 1 | ~4 |
| RMA TR: FMA (α×TR + decay×prev) | 1 | 4 | ~4 |
| RMA +DM: FMA | 1 | 4 | ~4 |
| RMA -DM: FMA | 1 | 4 | ~4 |
| +DI / -DI: 2 divisions | 2 | 8 | ~16 |
| DX: ABS + ADD + DIV | 3 | 5 | ~15 |
| RMA DX: FMA | 1 | 4 | ~4 |
| ADX-to-alpha conversion | 2 | 3 | ~6 |
| Adaptive VMA update: FMA | 1 | 4 | ~4 |
| **Total** | **21** | — | **~66 cycles** |

O(1) per bar. State is 4 RMA scalars + OHLC history + VMA output. WarmupPeriod = 2 × period (ADX requires full ADX convergence before meaningful adaptive tracking).

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR / DM preprocessing | Yes | `VSUBPD`, `VABSPD`, `VMAXPD`; independent per bar |
| RMA passes (TR, +DM, -DM, DX) | No | Recursive IIR; each value depends on previous |
| +DI / -DI divisions | Yes | `VDIVPD` once RMA series are completed |
| ADX computation | Partial | Vectorizable ratio except for recursive RMA |
| Adaptive VMA | No | Recursive IIR (alpha depends on computed ADX) |

All four RMA passes and the adaptive VMA are recursive IIR — inherently sequential. Batch mode can vectorize TR and DM computation (pure per-bar arithmetic) then run scalar RMA sweeps. Net batch speedup for large series: ~1.5× (TR/DM vectorization only).
