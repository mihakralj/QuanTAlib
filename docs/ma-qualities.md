# Four Core Qualities of Superior Moving Averages

> "Any fool can make something complicated. It takes a genius to make it simple."  Woody Guthrie (probably not talking about price smoothing, but the point stands)

Every moving average makes promises. Most break them. Understanding which promises matter requires looking at four measurable qualities that separate market-ready smoothers from academic curiosities.

## Accuracy: Preserving Large-Scale Structure

Accuracy measures how faithfully the smoothed output represents the true underlying price trajectory. Not the noise. Not the random walk component. The actual signal.

**What it means in practice:**

A price series contains signal (trend, cycles, mean-reversion) and noise (microstructure, bid-ask bounce, random fluctuations). An accurate moving average preserves the signal while suppressing the noise. Measuring accuracy requires comparing against a known "true" signal, which in real markets does not exist.

**How QuanTAlib measures accuracy:**

Synthetic price generation using Geometric Brownian Motion with known drift and volatility parameters. The "true" price is the deterministic component. Accuracy score compares smoother output against this ground truth.

| Accuracy Score | Interpretation |
| :------------- | :------------- |
| 9-10/10 | Preserves 95%+ of true trend direction |
| 7-8/10 | Minor smoothing artifacts at reversals |
| 5-6/10 | Loses some structural detail |
| 3-4/10 | Significant trend distortion |
| 1-2/10 | Output bears little relation to input |

## Timeliness: Minimal Lag

Lag is the silent killer of trading systems. The moving average says "buy" and the optimal entry was 6 bars ago. That is not timeliness. That is archaeology.

**The physics of lag:**

Every smoother introduces phase delay. Simple Moving Average with period $n$ lags by $(n-1)/2$ bars. Exponential Moving Average with decay $\alpha$ lags by $(1-\alpha)/\alpha$ bars. Zero-lag claims should trigger immediate skepticism. Physics does not negotiate.

**Timeliness vs. responsiveness:**

Timeliness measures delay to genuine trend changes. Responsiveness measures reaction to any price change, including noise. A smoother that reacts instantly to every tick has perfect responsiveness and terrible utility.

| Timeliness Score | Interpretation |
| :--------------- | :------------- |
| 9-10/10 | Lag < 20% of SMA equivalent |
| 7-8/10 | Lag 20-40% of SMA equivalent |
| 5-6/10 | Lag 40-60% of SMA equivalent |
| 3-4/10 | Lag 60-80% of SMA equivalent |
| 1-2/10 | Lag e SMA equivalent |

## Minimal Overshoot: Staying Within Bounds

Overshoot occurs when a smoother extends beyond the actual price extremes. The price reaches 100, the moving average hits 103. That is not smoothing. That is hallucination.

**Why overshoot happens:**

Aggressive lag compensation creates overshoot. DEMA, TEMA, and HMA all use extrapolation techniques that can push the smoothed value past the price it supposedly represents. During sharp reversals, this creates phantom levels that never existed in the actual market.

**Real-world impact:**

A threshold-based system triggers when the smoothed price crosses a level. If the smoother overshoots, the system triggers on fictional prices. In backtesting, this creates phantom profits that vanish in live trading.

| Overshoot Score | Interpretation |
| :-------------- | :------------- |
| 9-10/10 | Never exceeds price extremes |
| 7-8/10 | Occasional overshoot < 0.5% |
| 5-6/10 | Regular overshoot 0.5-1% |
| 3-4/10 | Frequent overshoot 1-3% |
| 1-2/10 | Severe overshoot > 3% |

## Smoothness: Reduced Noise

A quality smoother removes the jitter without removing the information. The output should look like what the market "meant" to do, not what it actually did in tick-by-tick chaos.

**Measuring smoothness:**

Smoothness correlates inversely with the second derivative of the output. High curvature changes indicate rough output. The ratio of output variance to input variance provides a smoothing factor. A smoother that passes everything through unchanged has a smoothing factor of 1.0 and is useless.

**The smoothness-timeliness tradeoff:**

More smoothing equals more lag. This is not a bug. This is physics. The only escape from this tradeoff is adaptive mechanisms that detect regime changes and adjust parameters in real-time.

| Smoothness Score | Interpretation |
| :--------------- | :------------- |
| 9-10/10 | Output variance < 10% of input |
| 7-8/10 | Output variance 10-25% of input |
| 5-6/10 | Output variance 25-50% of input |
| 3-4/10 | Output variance 50-75% of input |
| 1-2/10 | Output variance e 75% of input |

## The Impossible Quadrant

No moving average scores 10/10 on all four qualities. Pick two, sacrifice one, compromise on the fourth. That is the architecture of smoothing.

| Moving Average | Accuracy | Timeliness | Overshoot | Smoothness |
| :------------- | :------: | :--------: | :-------: | :--------: |
| SMA | 7 | 4 | 10 | 8 |
| EMA | 7 | 6 | 9 | 7 |
| DEMA | 6 | 8 | 5 | 6 |
| TEMA | 5 | 9 | 3 | 5 |
| HMA | 6 | 8 | 4 | 6 |
| JMA | 8 | 8 | 8 | 8 |
| ALMA | 8 | 6 | 9 | 8 |
| KAMA | 7 | 7 | 8 | 7 |

Notice the pattern: traditional smoothers (SMA, EMA) never overshoot but lag. Aggressive smoothers (DEMA, TEMA, HMA) reduce lag but overshoot. Adaptive smoothers (JMA, KAMA) attempt to balance all four but require more computational cycles per bar.

## Adaptive Moving Averages: Breaking the Tradeoff

The Dynamic Adaptive Moving Average (DAMA) represents an attempt to escape the impossible quadrant through real-time parameter adjustment.

### Architecture

Three-stage filtering pipeline:

**Stage 1: Volatility Assessment**

The ratio between short-term True Range and longer-term ATR measures relative volatility. This ratio drives parameter adjustment through calibrated sigmoid functions. High volatility increases responsiveness. Low volatility increases smoothing.

**Stage 2: Adaptive EMA**

Self-adjusting decay parameter based on volatility assessment. During trending markets with low noise, the decay approaches pure EMA behavior. During choppy markets with high noise, the decay approaches SMA-like smoothing.

**Stage 3: Kalman-Style Filtering**

Innovation-based correction provides optimal smoothing under Gaussian noise assumptions. The Kalman gain adjusts automatically based on the difference between predicted and actual prices.

**Stage 4: Final Adaptive Pass**

A second adaptive filter balances the output of stage 3, reducing any remaining artifacts from the Kalman correction while preserving responsiveness to genuine trend changes.

### Performance Profile

| Quality | DAMA Score | Notes |
| :------ | :--------: | :---- |
| Accuracy | 8/10 | Preserves trend structure well |
| Timeliness | 7/10 | Adapts to regime changes |
| Overshoot | 8/10 | Bounded by volatility-aware limits |
| Smoothness | 8/10 | Three-stage filtering removes jitter |

**Computational cost:** Approximately 45 cycles per bar in streaming mode. The three-stage pipeline adds overhead compared to simple EMA (8 cycles) but delivers measurably better quality metrics.

## References

- Ehlers, J. (2001). "Rocket Science for Traders." *Wiley*.
- Kaufman, P. (1995). "Smarter Trading." *McGraw-Hill*.
- Jurik, M. (1998). "Jurik Moving Average." *Jurik Research*.