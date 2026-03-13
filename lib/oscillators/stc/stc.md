# STC: Schaff Trend Cycle

> *Schaff Trend Cycle applies double stochastic smoothing to MACD, compressing a trend indicator into an oscillator's bounded range.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `kPeriod` (default 10), `dPeriod` (default 3), `fastLength` (default 23), `slowLength` (default 50), `smoothing` (default StcSmoothing.Ema)                      |
| **Outputs**      | Single series (Stc)                       |
| **Output range** | $0$ to $100$                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [stc.pine](stc.pine)                       |

- The Schaff Trend Cycle is a cyclometric oscillator that applies double-Stochastic normalization to MACD, extracting the cyclical phase hidden withi...
- **Similar:** [MACD](../../momentum/macd/Macd.md), [Stoch](../stoch/Stoch.md) | **Complementary:** ADX | **Trading note:** Schaff Trend Cycle; double Stochastic of MACD. Faster trend detection. 25/75 signal levels.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Schaff Trend Cycle is a cyclometric oscillator that applies double-Stochastic normalization to MACD, extracting the cyclical phase hidden within the trend itself. The recursive normalization produces a bounded 0–100 output that reaches extremes earlier than raw MACD while suppressing Stochastic jitter. Developed for currency markets, STC's tendency to flatline at 0 or 100 during strong trends signals continuation rather than reversal — a feature that distinguishes it from conventional momentum oscillators. Output converges toward a square wave in steady-state trending conditions.

## Historical Context

Doug Schaff developed STC in the 1990s while trading currency markets. His diagnosis: MACD identified trends correctly but with unacceptable lag — by signal time, much of the move had elapsed. The Stochastic oscillator was fast but noisy, generating false signals in trending markets. Schaff's synthesis recognized that trends themselves move in cycles. Rather than choosing between lagging trend detection and noisy cycle extraction, he piped MACD through the Stochastic twice. The first pass normalizes MACD within its recent range, collapsing the unbounded trend signal into a 0–100 band. The second pass normalizes the smoothed first pass, further compressing the cycle information and creating a self-normalizing oscillator. The double normalization acts as a nonlinear filter that amplifies transitions and suppresses noise during sustained moves. STC found particular traction in forex trading where the 24-hour market rewarded speed advantages over MACD. The flatline behavior at extremes — initially dismissed as a limitation — became recognized as a defining feature: sustained 0 or 100 readings indicate trend continuation with high confidence, equivalent to a digital "trend on" signal.

## Architecture & Physics

### 1. MACD Construction

Fast and slow EMAs generate the raw trend signal:

$$\alpha_f = \frac{2}{\text{fastLength} + 1}, \quad \alpha_s = \frac{2}{\text{slowLength} + 1}$$

$$\text{EMA}_{f,t} = \alpha_f \cdot P_t + (1 - \alpha_f) \cdot \text{EMA}_{f,t-1}$$

$$\text{EMA}_{s,t} = \alpha_s \cdot P_t + (1 - \alpha_s) \cdot \text{EMA}_{s,t-1}$$

$$\text{MACD}_t = \text{EMA}_{f,t} - \text{EMA}_{s,t}$$

### 2. First Stochastic (%K₁)

Normalize MACD within its recent $k$-bar range:

$$\%K_1 = 100 \times \frac{\text{MACD}_t - \min(\text{MACD}_{t-k+1:t})}{\max(\text{MACD}_{t-k+1:t}) - \min(\text{MACD}_{t-k+1:t})}$$

When $\max = \min$ (flat MACD), $\%K_1$ holds its previous value. This collapses the unbounded MACD into [0, 100].

### 3. First Smoothing (%D₁)

EMA smooth the first Stochastic to reduce whipsaw:

$$\alpha_d = \frac{2}{d\text{Period} + 1}$$

$$\%D_{1,t} = \alpha_d \cdot \%K_{1,t} + (1 - \alpha_d) \cdot \%D_{1,t-1}$$

### 4. Second Stochastic (%K₂)

Apply Stochastic normalization again to %D₁, using the same $k$-bar window:

$$\%K_2 = 100 \times \frac{\%D_{1,t} - \min(\%D_{1,t-k+1:t})}{\max(\%D_{1,t-k+1:t}) - \min(\%D_{1,t-k+1:t})}$$

This second pass further compresses the signal, amplifying transitions between trend phases.

### 5. Final Smoothing

Apply selected smoothing method to %K₂:

$$\text{STC}_t = \text{Smooth}(\%K_{2,t})$$

Smoothing options:

- **None:** Raw %K₂ output
- **EMA:** Standard EMA smoothing with $\alpha_d$
- **Sigmoid:** $S(x) = \frac{100}{1 + e^{-0.1(x - 50)}}$ — S-curve compression
- **Digital:** Threshold at 50 → output snaps to 0 or 100 (square wave)

### 6. Complexity

- **Time:** $O(k)$ per bar for min/max scanning over both Stochastic windows
- **Space:** $O(k)$ — two ring buffers of size kPeriod (MACD values and %D₁ values)
- **Warmup:** slowLength + kPeriod bars before output stabilizes

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $k$ | kPeriod | 10 | $k \geq 2$ |
| $d$ | dPeriod | 3 | $d \geq 1$ |
| $f$ | fastLength | 23 | $f \geq 1$ |
| $s$ | slowLength | 50 | $s > f$ |
| — | smoothing | EMA | None / EMA / Sigmoid / Digital |

### Signal Characteristics

| Condition | Output Behavior |
|-----------|----------------|
| Strong uptrend | Flatlines at 100 (square wave high) |
| Strong downtrend | Flatlines at 0 (square wave low) |
| Trend transition | Rapid swing between extremes |
| Ranging market | Oscillates mid-range (25–75) |
| Above 75 | Overbought zone |
| Below 25 | Oversold zone |

### Cycle Length Heuristic

Setting $k \approx f/2$ targets the half-cycle of the MACD's dominant frequency, aligning the Stochastic window with the trend's internal oscillation period.

### SIMD Applicability

The recursive EMA dependencies and sequential min/max ring buffer updates prevent SIMD vectorization of the streaming path. The `Calculate(Span)` path can parallelize independent MACD computations but must serialize the double-Stochastic pipeline.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Fast EMA | ~3 | 1 FMA + 1 MUL |
| Slow EMA | ~3 | 1 FMA + 1 MUL |
| MACD subtraction | ~1 | 1 SUB |
| Ring buffer add (MACD) | ~1 | 1 write + index update |
| Min/Max scan (MACD buf) | ~2k | Linear scan of k elements × 2 (min + max) |
| First Stochastic (%K₁) | ~4 | 1 SUB + 1 DIV + 1 MUL + 1 branch |
| First EMA smoothing (%D₁) | ~3 | 1 FMA + 1 MUL |
| Ring buffer add (%D₁) | ~1 | 1 write + index update |
| Min/Max scan (%D₁ buf) | ~2k | Linear scan of k elements × 2 |
| Second Stochastic (%K₂) | ~4 | 1 SUB + 1 DIV + 1 MUL + 1 branch |
| Final smoothing (EMA) | ~3 | 1 FMA + 1 MUL |
| Clamp | ~2 | 2 comparisons |
| **Total (k=10 default)** | **~65** | **O(k) dominated by dual min/max scans** |
| **Total (k=50 worst)** | **~225** | **Linear growth with kPeriod** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | No: recursive EMAs + sequential ring buffer min/max prevent vectorization |
| Bottleneck | Dual min/max scans over ring buffers (2×k comparisons per bar) |
| Parallelism | MACD EMA computation is independent of Stochastic pipeline but still sequential IIR |
| Memory | O(k): two ring buffers of kPeriod doubles + 6 scalar EMA states (~200 bytes at k=10) |
| Throughput | Moderate; faster than HT family (no transcendentals) but slower than pure IIR (min/max scans) |

## Resources

- Schaff, D. — "Schaff Trend Cycle" (currency trading methodology, 1990s)
- PineScript reference: `stc.pine` in indicator directory
- Ehlers, J.F. — *Cybernetic Analysis for Stocks and Futures* (cycle extraction theory)
