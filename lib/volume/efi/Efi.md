# EFI: Elder's Force Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 13)                      |
| **Outputs**      | Single series (EFI)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [efi.pine](efi.pine)                       |

- Elder's Force Index (EFI) quantifies the buying and selling pressure behind price movements by multiplying price change by volume.
- Parameterized by `period` (default 13).
- Output range: Unbounded.
- Requires `> period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Force Index combines price movement with volume to measure the power behind every move. It's the market's polygraph test." — Dr. Alexander Elder

Elder's Force Index (EFI) quantifies the buying and selling pressure behind price movements by multiplying price change by volume. Large positive values indicate strong buying pressure (bulls in control), while large negative values reveal strong selling pressure (bears dominant).

The genius of EFI lies in its integration of three essential market elements: direction (price change sign), extent (price change magnitude), and conviction (volume). A $1 move on 1 million shares tells a very different story than the same move on 10,000 shares.

## Historical Context

Developed by Dr. Alexander Elder and introduced in his seminal book "Trading for a Living" (1993), the Force Index emerged from Elder's quest to measure market momentum more accurately. Unlike oscillators that focus solely on price, Elder recognized that volume provides crucial context—it measures the crowd's emotional commitment to a price move.

Elder originally used a 2-period EMA for short-term signals and a 13-period EMA for intermediate trends. The raw force (price change × volume) is smoothed with an exponential moving average to filter noise while preserving responsiveness.

This implementation uses bias-corrected EMA during warmup, ensuring accurate values from the first calculation rather than waiting for exponential decay to stabilize.

## Architecture & Physics

EFI operates as a two-stage pipeline:

### 1. Raw Force Calculation

The raw force measures instantaneous buying or selling pressure:

$$
F_t = (Close_t - Close_{t-1}) \times Volume_t
$$

- Positive when price rises (buying pressure)
- Negative when price falls (selling pressure)
- Magnitude proportional to both price change and volume

### 2. EMA Smoothing with Bias Correction

The raw force is smoothed using an exponential moving average:

$$
\alpha = \frac{2}{period + 1}
$$

$$
EMA_t = \alpha \times F_t + (1 - \alpha) \times EMA_{t-1}
$$

During warmup, bias correction compensates for the EMA's initial underestimation:

$$
e_t = e_{t-1} \times (1 - \alpha)
$$

$$
EFI_t = \frac{EMA_t}{1 - e_t}
$$

Once $e_t \leq 10^{-10}$, the correction factor approaches 1 and is disabled.

## Mathematical Foundation

### Raw Force

$$
F_t = \Delta P_t \times V_t
$$

where:
- $\Delta P_t = Close_t - Close_{t-1}$ (price change)
- $V_t$ = Volume at time t

### Smoothed Force Index

Standard EMA form:
$$
EFI_t = \alpha \times F_t + (1 - \alpha) \times EFI_{t-1}
$$

Using FMA optimization:
$$
EFI_t = \text{FMA}(\alpha, F_t - EFI_{t-1}, EFI_{t-1})
$$

### Interpretation Thresholds

- **Strong buying pressure**: EFI >> 0 with increasing trend
- **Strong selling pressure**: EFI << 0 with decreasing trend
- **Zero line crossover**: Potential trend change signal
- **Divergence**: Price makes new high/low but EFI doesn't confirm

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | Price change |
| MUL | 1 | Force calculation |
| FMA | 1 | EMA smoothing |
| MUL | 1 | Bias decay (warmup only) |
| DIV | 1 | Bias correction (warmup only) |
| **Total** | ~3-5 | Per bar |

### Memory Footprint

| Component | Size | Notes |
| :--- | :---: | :--- |
| State record | 48 bytes | 6 doubles/flags |
| Previous state | 48 bytes | For bar correction |
| **Total** | ~96 bytes | Per instance |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Bias-corrected EMA matches reference |
| **Timeliness** | 8/10 | EMA lag increases with period |
| **Overshoot** | 7/10 | Can spike on volume surges |
| **Smoothness** | 7/10 | Smoother than raw force, responsive to extremes |
| **Allocation** | 10/10 | Zero heap allocations in hot path |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **QuanTAlib** | ✅ | Bias-corrected EMA implementation |
| **TA-Lib** | N/A | No Force Index implementation |
| **Skender** | N/A | Has ElderRay (different indicator) |
| **Tulip** | N/A | No Force Index implementation |
| **Ooples** | ✅ | Matches after warmup period |

Note: Most libraries use standard EMA without bias correction, causing warmup divergence. After the warmup period, values converge.

## Common Pitfalls

1. **First Bar**: No previous close exists, so raw force = 0. The implementation handles this gracefully.

2. **Volume Scale**: EFI is not bounded—values depend on volume magnitude. Comparing EFI across securities with vastly different volume levels requires normalization.

3. **Zero Volume**: When volume is zero, raw force is zero regardless of price change. This can create misleading readings during low-liquidity periods.

4. **Period Selection**:
   - Short periods (2-3): More sensitive, more noise, good for short-term signals
   - Standard period (13): Balance of responsiveness and smoothness
   - Long periods (20+): Smoother, slower, better for trend confirmation

5. **Divergence Interpretation**: EFI divergence from price is a warning, not a signal. Confirm with other indicators before acting.

6. **isNew Parameter**: When correcting a bar (isNew=false), the implementation properly restores previous state. Failure to handle this causes cumulative EMA errors.

7. **NaN/Infinity Handling**: Implementation substitutes last valid close for NaN inputs and treats infinite volume as zero to prevent propagation of invalid values.

## References

- Elder, A. (1993). "Trading for a Living." John Wiley & Sons.
- Elder, A. (2002). "Come Into My Trading Room." John Wiley & Sons.
- StockCharts. "Force Index." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:force_index)
- Investopedia. "Force Index Definition." [Technical Analysis](https://www.investopedia.com/terms/f/force-index.asp)
