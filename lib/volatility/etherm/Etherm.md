# ETHERM: Elder's Thermometer

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                       |
| **Inputs**       | OHLCV bar (TBar)                 |
| **Parameters**   | `period` (default 22)            |
| **Outputs**      | Temperature + Signal (EMA)       |
| **Output range** | $\geq 0$                        |
| **Warmup**       | `period` bars                    |

### TL;DR

- Elder's Thermometer (ETHERM) measures how far today's price bar protrudes beyond yesterday's range, capturing the maximum outward extension in either direction.
- Parameterized by `period` (default 22) for the EMA signal line.
- Output range: $\geq 0$ (same units as price).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Markets run a fever before they crash. The thermometer tells you when to reach for the aspirin."

Elder's Thermometer (ETHERM) measures bar-to-bar range extension — the maximum outward protrusion of the current bar beyond the previous bar's high or low. Developed by Dr. Alexander Elder, it captures only outward expansions; inward contractions clamp to zero. An EMA signal line with bias compensation provides a smoothed reference for detecting explosive moves (temperature significantly exceeding the signal).

## Historical Context

Dr. Alexander Elder introduced the Market Thermometer in *Come Into My Trading Room* (2002) as part of his Triple Screen trading system refinements. Elder observed that bars extending well beyond the prior bar's range signaled heightened volatility — the market "running a fever." The thermometer provides a simple, bar-level volatility measure that distinguishes between outward breakouts and inward consolidation, making it ideal for stop placement and position sizing decisions.

## Architecture & Physics

ETHERM is a **two-stage pipeline**: a per-bar range-extension measurement followed by an exponential smoother.

**Stage 1 — Temperature:** For each bar, compute how far the high protrudes above the previous high and how far the low protrudes below the previous low. Only outward extensions count; inward contractions clamp to zero. The temperature is the larger of the two protrusions.

**Stage 2 — Signal:** A bias-compensated EMA of the temperature provides a smoothed baseline. The bias compensation ensures accuracy from the first bar by dividing out the geometric decay factor $e_t$, converging to a standard EMA as $e_t \to 0$.

### Transfer Function

The signal line is a standard EMA applied to the temperature series:

$$H(z) = \frac{\alpha}{1 - \beta z^{-1}}, \quad \alpha = \frac{2}{N+1}, \quad \beta = 1 - \alpha$$

### Half-Life

$$t_{1/2} = \frac{-\ln 2}{\ln \beta}$$

For `period = 22`: $\beta \approx 0.913$, $t_{1/2} \approx 7.6$ bars.

### Warmup Period

QuanTAlib uses bias-compensated EMA, which converges after approximately `period` bars. During warmup, outputs are produced but `IsHot` returns false until the compensator $e_t \leq 0.05$.

## Mathematical Foundation

### Step 1: Outward Protrusions

$$\text{highDiff}_t = \max(H_t - H_{t-1},\; 0)$$
$$\text{lowDiff}_t = \max(L_{t-1} - L_t,\; 0)$$

### Step 2: Temperature

$$T_t = \max(\text{highDiff}_t,\; \text{lowDiff}_t)$$

### Step 3: EMA Signal with Bias Compensation

$$\text{ema}_t = \beta \cdot \text{ema}_{t-1} + \alpha \cdot T_t$$

$$e_t = \beta \cdot e_{t-1}, \quad e_0 = 1$$

$$\text{Signal}_t = \begin{cases} \frac{\text{ema}_t}{1 - e_t} & \text{if } e_t > \epsilon \\ \text{ema}_t & \text{otherwise} \end{cases}$$

where $N$ = `period`, $H_t$ = High, $L_t$ = Low, $\epsilon = 10^{-10}$.

## Performance Profile

### Operation Count (per bar)

| Operation       | Count | Notes                              |
| --------------- | ----- | ---------------------------------- |
| Subtract        | 2     | High/low diffs                     |
| Max             | 3     | Clamp to 0 (×2), final max        |
| FMA             | 1     | EMA update                         |
| Multiply        | 2     | $\alpha \cdot T$, $\beta \cdot e$  |
| Division        | 1     | Bias compensation                  |
| Compare/branch  | 2     | Finite check, bias threshold       |
| **Total**       | ~11   | O(1) per bar, no allocations       |

### SIMD Applicability

Not beneficial — the recursive EMA dependency prevents vectorization. Each bar depends on the previous bar's state.

### Memory Layout

| Field          | Type     | Bytes | Purpose                      |
| -------------- | -------- | ----- | ---------------------------- |
| `PrevHigh`     | `double` | 8     | Previous bar's high          |
| `PrevLow`      | `double` | 8     | Previous bar's low           |
| `Ema`          | `double` | 8     | Running EMA of temperature   |
| `E`            | `double` | 8     | Bias compensator             |
| `LastValidHigh`| `double` | 8     | NaN fallback for high        |
| `LastValidLow` | `double` | 8     | NaN fallback for low         |
| `LastValidTemp`| `double` | 8     | NaN fallback for temperature |
| `Count`        | `int`    | 4     | Bar counter                  |
| **Total**      |          | 60    | Single cache line            |

## Validation

| Library  | Match | Notes                                    |
| -------- | ----- | ---------------------------------------- |
| TA-Lib   | —     | No Elder Thermometer function            |
| Skender  | —     | No direct equivalent                     |
| Tulip    | —     | No direct equivalent                     |
| Self     | ✓     | Batch ⟷ streaming ⟷ span consistency     |
| Pine     | ✓     | `etherm.pine` matches C# output          |

## Common Pitfalls

1. **Using close-only data** — ETHERM requires High and Low prices. When fed a single value (TValue), it treats H=L, producing zero temperature. Always use `Update(TBar)`.
2. **Confusing temperature with signal** — The `Value` property returns the raw temperature (current bar only); the `Signal` property returns the smoothed EMA. Use signal for trend comparisons.
3. **Inside bars** — Both protrusions clamp to zero, so inside bars always produce temperature = 0. This is by design, not a bug.
4. **First bar** — No previous bar exists, so temperature = 0. The EMA signal starts building from the second bar.
5. **Explosive threshold** — A common strategy is to flag bars where temperature exceeds `Signal × multiplier` (e.g., 3×) as explosive moves.

## References

- **Elder, Alexander** (2002). *Come Into My Trading Room: A Complete Guide to Trading*, Wiley. p. 162.
- **Elder, Alexander** (1993). *Trading for a Living*, Wiley. (Earlier discussion of volatility-based stops.)