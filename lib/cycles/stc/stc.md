# STC: Schaff Trend Cycle

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

### Pseudo-code

```
Initialize:
  ema_fast = ema_slow = first price
  α_f = 2 / (fastLength + 1)
  α_s = 2 / (slowLength + 1)
  α_d = 2 / (dPeriod + 1)
  macd_buf = RingBuffer(kPeriod)
  d1_buf = RingBuffer(kPeriod)
  %D₁ = 0
  bar_count = 0

On each bar (price, isNew):
  if !isNew: restore previous state

  // Step 1: MACD
  ema_fast = FMA(ema_fast, 1 - α_f, α_f × price)
  ema_slow = FMA(ema_slow, 1 - α_s, α_s × price)
  macd = ema_fast - ema_slow

  // Step 2: First Stochastic
  macd_buf.Add(macd)
  macd_max = Max(macd_buf)
  macd_min = Min(macd_buf)
  range1 = macd_max - macd_min
  %K₁ = range1 > 0 ? 100 × (macd - macd_min) / range1 : prev_%K₁

  // Step 3: First Smoothing
  %D₁ = FMA(%D₁, 1 - α_d, α_d × %K₁)

  // Step 4: Second Stochastic
  d1_buf.Add(%D₁)
  d1_max = Max(d1_buf)
  d1_min = Min(d1_buf)
  range2 = d1_max - d1_min
  %K₂ = range2 > 0 ? 100 × (%D₁ - d1_min) / range2 : prev_%K₂

  // Step 5: Final Smoothing
  switch smoothing:
    None:    STC = %K₂
    EMA:     STC = FMA(prev_STC, 1 - α_d, α_d × %K₂)
    Sigmoid: STC = 100 / (1 + exp(-0.1 × (%K₂ - 50)))
    Digital: STC = %K₂ ≥ 50 ? 100 : 0

  output = Clamp(STC, 0, 100)
```

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

## Resources

- Schaff, D. — "Schaff Trend Cycle" (currency trading methodology, 1990s)
- PineScript reference: `stc.pine` in indicator directory
- Ehlers, J.F. — *Cybernetic Analysis for Stocks and Futures* (cycle extraction theory)
