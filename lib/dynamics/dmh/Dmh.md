# DMH: Ehlers Directional Movement with Hann Windowing

> *Ehlers takes Wilder's directional movement and cleans it up with EMA smoothing and a Hann-windowed FIR filter — the result is a zero-centered trend direction indicator with minimal lag and maximum smoothness.*

| Property         | Value                                    |
| ---------------- | ---------------------------------------- |
| **Category**     | Dynamics                                 |
| **Inputs**       | High + Low (TBar)                        |
| **Parameters**   | `period` (default: 14)                   |
| **Outputs**      | Single series (DMH)                      |
| **Output range** | Unbounded, zero-centered                 |
| **Warmup**       | `period + 1` bars                        |
| **PineScript**   | [dmh.pine](dmh.pine)                     |

- DMH applies a three-stage pipeline to Wilder's directional movement: raw DM extraction, EMA smoothing, and Hann-windowed FIR filtering. The result is a smooth, zero-centered oscillator where positive values indicate uptrend dominance and negative values indicate downtrend dominance.
- **Similar:** [DMX](../dmx/Dmx.md), [DX](../dx/Dx.md), [ADX](../adx/Adx.md) | **Complementary:** +DI/-DI for direction | **Trading note:** Zero crossings signal trend direction changes. More responsive than standard DMI with less noise.
- Self-consistency validated (no external library implements DMH).

## Historical Context

John Ehlers introduced DMH in TASC Magazine (December 2021) as "An Improved Directional Movement Indicator." Ehlers recognized that Wilder's original DM system (1978) produces sparse, spiky directional movement values that require aggressive smoothing. Instead of Wilder's RMA, Ehlers applies a simple EMA followed by a Hann-windowed FIR filter. The Hann window provides excellent spectral leakage suppression — it attenuates sidelobes by ~32 dB compared to a rectangular window — resulting in a much smoother output without the lag penalty of longer RMA periods. The key innovation is decoupling the smoothing into two stages: the EMA creates a continuous signal from the sparse DM spikes, and the Hann FIR provides the final polish.

## Architecture & Physics

### 1. Directional Movement (Wilder's Original)

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$+DM = \begin{cases} \text{UpMove} & \text{if UpMove} > \text{DownMove and UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

### 2. EMA Smoothing

$$\text{SF} = \frac{1}{N}$$

$$\text{EMA}_t = \text{SF} \times (+DM - (-DM)) + (1 - \text{SF}) \times \text{EMA}_{t-1}$$

### 3. Hann FIR Filter

$$w(k) = 1 - \cos\!\left(\frac{2\pi k}{N + 1}\right) \quad \text{for } k = 1 \ldots N$$

$$\text{DMH} = \frac{\sum_{k=1}^{N} w(k) \cdot \text{EMA}_{t-k+1}}{\sum_{k=1}^{N} w(k)}$$

### 4. Complexity

- **Time:** $O(N)$ per bar — Hann FIR scan over period-length buffer
- **Space:** $O(N)$ — RingBuffer stores EMA history
- **Warmup:** $N + 1$ bars (one bar for DM calculation + $N$ EMA values in buffer)

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 1$ |

### DMH vs DMX/DMI Comparison

| Property | DMI (Wilder) | DMX (Jurik) | DMH (Ehlers) |
|----------|-------------|-------------|--------------|
| Smoothing | RMA | JMA | EMA + Hann FIR |
| Lag | High | Low | Moderate |
| Noise rejection | Moderate | High | Very high |
| Uses True Range | Yes | Yes | No |
| Output range | 0-100 per DI | Unbounded | Unbounded |
| Available in TA-Lib | Yes | No | No |

### Key Properties

- **Zero-centered:** Positive = uptrend dominance, Negative = downtrend dominance
- **No True Range:** Uses only DM difference, not DI normalization
- **Inside days:** Both PlusDM and MinusDM are zero → EMA decays toward zero

## Performance Profile

### Operation Count (Streaming Mode)

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB × 2 (UpMove, DownMove) | 2 | 1 | 2 |
| CMP × 2 (DM directional guards) | 2 | 1 | 2 |
| FMA × 1 (EMA update) | 1 | 4 | 4 |
| RingBuffer Add × 1 | 1 | 3 | 3 |
| FMA × N (Hann FIR scan) | N | 4 | 4N |
| ADD × N (coef accumulation) | N | 1 | N |
| DIV × 1 (normalize) | 1 | 15 | 15 |
| **Total** | — | — | **~5N + 26** |

For N=14: ~96 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| DM computation | Yes | VSUBPD + VCMPPD |
| EMA smoothing | **No** | Recursive IIR — sequential |
| Hann FIR scan | Yes (per bar) | VFMADDPD inner loop |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | FMA-precise computation |
| **Timeliness** | 7/10 | N+1 warmup; Hann adds minor lag |
| **Smoothness** | 9/10 | Dual-stage smoothing (EMA + Hann FIR) |
| **Noise Rejection** | 9/10 | Hann window suppresses spectral leakage |

## Resources

- Ehlers, J.F. — "The DMH: An Improved Directional Movement Indicator" (TASC, December 2021)
- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `dmh.pine` in indicator directory
