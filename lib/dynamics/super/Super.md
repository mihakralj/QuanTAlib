# SUPER: SuperTrend

> "It's not an indicator; it's a trailing stop with a marketing budget."

SuperTrend is a trend-following overlay that uses ATR-scaled bands around the HL2 midpoint, switching between upper and lower bands based on close price breakouts. A ratchet mechanism prevents the active band from moving against the trend, creating a step-like trailing stop that adapts to volatility. The indicator is a two-state machine (bullish/bearish) with O(1) per-bar updates and zero allocations in the hot path.

## Historical Context

Olivier Seban created SuperTrend, which gained massive popularity in the retail trading community for its visual simplicity: a single line that is green during uptrends and red during downtrends. The construction combines Wilder's ATR volatility measurement (1978) with a breakout/ratchet mechanism. Unlike moving-average crossover systems that produce continuous values, SuperTrend outputs a binary trend state with a concrete stop level, making it directly actionable as a trailing stop-loss. The indicator does not repaint historical values, though the current bar's value can oscillate until the close is finalized.

## Architecture & Physics

### 1. Basic Bands

The raw bands center on the HL2 midpoint, offset by ATR times a multiplier:

$$\text{Upper}_{\text{basic}} = \frac{H_t + L_t}{2} + m \cdot \text{ATR}(N)$$

$$\text{Lower}_{\text{basic}} = \frac{H_t + L_t}{2} - m \cdot \text{ATR}(N)$$

where $m$ is the multiplier (default 3.0) and $N$ is the ATR period (default 10).

### 2. Ratchet Logic

The bands act as a one-way ratchet that prevents regression against the trend:

**Upper band** (bearish stop) can only move down:

$$\text{Upper}_{\text{final}} = \begin{cases} \min(\text{Upper}_{\text{basic}},\ \text{Upper}_{\text{prev}}) & \text{if } C_{t-1} \leq \text{Upper}_{\text{prev}} \\ \text{Upper}_{\text{basic}} & \text{otherwise} \end{cases}$$

**Lower band** (bullish stop) can only move up:

$$\text{Lower}_{\text{final}} = \begin{cases} \max(\text{Lower}_{\text{basic}},\ \text{Lower}_{\text{prev}}) & \text{if } C_{t-1} \geq \text{Lower}_{\text{prev}} \\ \text{Lower}_{\text{basic}} & \text{otherwise} \end{cases}$$

### 3. Trend State Machine

$$\text{Trend}_t = \begin{cases} \text{Bullish} & \text{if } C_t > \text{Upper}_{\text{final}} \\ \text{Bearish} & \text{if } C_t < \text{Lower}_{\text{final}} \\ \text{Trend}_{t-1} & \text{otherwise (hysteresis)} \end{cases}$$

$$\text{SuperTrend} = \begin{cases} \text{Lower}_{\text{final}} & \text{if Bullish} \\ \text{Upper}_{\text{final}} & \text{if Bearish} \end{cases}$$

The output is the active stop level. Crossing the active band flips the state.

### 4. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar |
| Space | O(1) (ATR state + 2 band values + 1 trend boolean) |
| Allocations | Zero in hot path |
| Warmup | N bars (ATR stabilization) |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| atrPeriod | int | 10 | > 0 | ATR lookback period |
| multiplier | double | 3.0 | > 0 | ATR multiplier for band width |

### Pseudo-code

```
SUPERTREND(bar, atrPeriod=10, multiplier=3.0):

  // ATR update (Wilder's smoothing or SMA)
  atr = ATR.Update(bar, atrPeriod)

  // Basic bands
  hl2 = (bar.High + bar.Low) / 2
  upper_basic = hl2 + multiplier * atr
  lower_basic = hl2 - multiplier * atr

  // Ratchet: upper can only decrease, lower can only increase
  if prev_close <= prev_upper_final:
    upper_final = min(upper_basic, prev_upper_final)
  else:
    upper_final = upper_basic

  if prev_close >= prev_lower_final:
    lower_final = max(lower_basic, prev_lower_final)
  else:
    lower_final = lower_basic

  // State machine transition
  if bar.Close > upper_final:
    is_bullish = true
  else if bar.Close < lower_final:
    is_bullish = false
  // else: retain previous state (hysteresis)

  // Output active stop level
  if is_bullish:
    supertrend = lower_final
  else:
    supertrend = upper_final

  return supertrend
```

### Band Behavior by State

| State | Active Band | Ratchet Direction | Flip Condition |
|:------|:------------|:------------------|:---------------|
| Bullish | Lower (support) | Can only rise | Close < Lower |
| Bearish | Upper (resistance) | Can only fall | Close > Upper |

The step-like output results from the ratchet constraint: the band remains flat until a new extremum pushes it in the trend direction. Whipsaws occur in ranging markets where close repeatedly crosses both bands.

## Performance Profile

### Operation Count (Streaming Mode)

Supertrend uses ATR-based bands with a state-machine ratchet: upper/lower bands only move in their respective directions, and the trend flips when price crosses the active band.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| TR computation (SUB×3, ABS×2, MAX×2) | 7 | 1 | 7 |
| FMA (RMA ATR update) | 1 | 4 | 4 |
| MUL (ATR × multiplier) | 1 | 3 | 3 |
| ADD + SUB (upper/lower basic bands) | 2 | 1 | 2 |
| MAX/MIN (ratchet: clamp to prev band) | 2 | 1 | 2 |
| CMP × 2 (trend flip conditions) | 2 | 1 | 2 |
| CMP × 2 (final band selection) | 2 | 1 | 2 |
| **Total** | **19** | — | **~22 cycles** |

The ratchet logic adds branch overhead (~3 cycles average from the CMPs), but overall ~22 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR + ATR (RMA) | **No** | Recursive RMA — sequential |
| Band arithmetic | Yes | VADDPD + VSUBPD + VMULPD after ATR pass |
| Ratchet clamp | **No** | State-dependent MAX/MIN — depends on prior band value |
| Trend flip state machine | **No** | Branch-heavy flip logic depends on prior trend state |

The ratchet and trend-flip logic create strong sequential dependencies. The ATR and band arithmetic sub-steps are vectorizable as intermediate passes.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | FMA ATR; ratchet logic exact |
| **Timeliness** | 7/10 | ATR period warmup; ratchet responds immediately to band crosses |
| **Smoothness** | 9/10 | One-way ratchet eliminates oscillation; clean directional band |
| **Noise Rejection** | 8/10 | ATR-scaled bands self-adjust to volatility regime |

## Resources

- Seban, O. SuperTrend indicator documentation.
- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
