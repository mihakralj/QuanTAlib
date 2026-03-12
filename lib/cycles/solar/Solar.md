# SOLAR: Solar Cycle Indicator

> *Solar cycles encode the Sun's rhythmic activity into a tradeable signal, bridging astrophysics and price action.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (SOLAR)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `0` bars                          |
| **PineScript**   | [solar.pine](solar.pine)                       |

- SOLAR models Earth's seasonal position relative to the Sun using astronomical ephemeris calculations.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `0` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

SOLAR models Earth's seasonal position relative to the Sun using astronomical ephemeris calculations. Output oscillates continuously from $-1.0$ (Winter Solstice) through $0.0$ (Equinoxes) to $+1.0$ (Summer Solstice), providing a smooth, mathematically precise seasonal phase for econometric modeling. Like LUNAR, the indicator is purely time-based, requires no price data, and has zero warmup since the calculation is deterministic from any timestamp.

## Historical Context

Seasonal adjustments are fundamental to econometric analysis. Agricultural commodities, retail sales, energy consumption, and tourism all exhibit strong annual patterns. Traditional approaches use monthly dummy variables or calendar-based lookup tables, creating discontinuities at month boundaries. Astronomical seasonality offers a continuous, smooth alternative: the Sun's ecliptic longitude provides an exact phase position within the annual cycle at any time resolution. The implementation derives from Jean Meeus' *Astronomical Algorithms* (1998), computing the Sun's geometric mean longitude, mean anomaly, and equation of center with sufficient precision ($\pm 0.01°$) for financial applications. Unlike lunar cycles, the tropical year's length varies by only seconds over centuries, making solar seasonality highly predictable.

## Architecture & Physics

### 1. Julian Date Conversion

$$JD = \frac{UnixMs}{86400000} + 2440587.5$$

$$T = \frac{JD - 2451545.0}{36525.0}$$

where $T$ is Julian centuries from the J2000.0 epoch.

### 2. Geometric Mean Longitude

The Sun's mean position in its apparent orbit:

$$L_0 = 280.46646 + 36000.76983T + 0.0003032T^2$$

### 3. Mean Anomaly

Angular distance from perihelion:

$$M = 357.52911 + 35999.05029T - 0.0001537T^2$$

### 4. Equation of Center

Correction for orbital eccentricity ($e \approx 0.0167$):

$$C = (1.914602 - 0.004817T - 0.000014T^2) \sin M + (0.019993 - 0.000101T) \sin 2M + 0.000289 \sin 3M$$

### 5. True Ecliptic Longitude

$$\lambda_{Sun} = L_0 + C$$

### 6. Seasonal Index

$$Solar = \sin(\lambda_{Sun})$$

This maps: Vernal Equinox ($\lambda = 0°$) $\to 0$, Summer Solstice ($\lambda = 90°$) $\to +1$, Autumnal Equinox ($\lambda = 180°$) $\to 0$, Winter Solstice ($\lambda = 270°$) $\to -1$.

### 7. Complexity

$O(1)$ per timestamp. No state required. Zero warmup. The tropical year is approximately 365.242 days.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

The calculation is entirely determined by the input timestamp.

### Seasonal Correspondence (Northern Hemisphere)

| Date (approx.) | $\lambda_{Sun}$ | Solar Value | Season |
|-----------------|-----------------|-------------|--------|
| March 20 | $0°$ | $0.0$ | Vernal Equinox |
| June 21 | $90°$ | $+1.0$ | Summer Solstice |
| September 22 | $180°$ | $0.0$ | Autumnal Equinox |
| December 21 | $270°$ | $-1.0$ | Winter Solstice |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $Solar \approx +1$ | Peak summer (Northern Hemisphere) |
| $Solar \approx -1$ | Peak winter (Northern Hemisphere) |
| $Solar = 0$ (rising) | Spring equinox crossing |
| $Solar = 0$ (falling) | Autumn equinox crossing |
| Southern Hemisphere | Negate the output |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Julian date conversion | ~4 | 1 DIV + 1 ADD + 1 SUB + 1 DIV |
| Horner polynomial (L0) | ~5 | 2 FMA + 1 mod |
| Horner polynomial (M) | ~5 | 2 FMA + 1 mod |
| SIN evaluations (equation of center) | ~24 | 3 `Math.Sin` calls (~8 cycles each) |
| Equation of center arithmetic | ~8 | 3 FMA chains + 2 ADD |
| True longitude addition | ~1 | 1 ADD |
| Final SIN (seasonal index) | ~10 | 1 degree-to-radian MUL + 1 `Math.Sin` |
| **Total** | **~57** | **O(1) pure arithmetic; simpler than LUNAR** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: fully stateless; each timestamp independent; `Vector<double>` applicable |
| Bottleneck | 4 transcendental calls (3 SIN for equation of center + 1 final SIN); ~32 cycles |
| Parallelism | Full: no inter-bar dependencies; ideal for `Vector<double>` batch processing |
| Memory | O(0): zero state; pure function of timestamp |
| Throughput | Fastest cycle indicator; ~2× faster than LUNAR (fewer perturbation terms) |

## Resources

- **Meeus, J.** *Astronomical Algorithms*. 2nd ed., Willmann-Bell, 1998.
- **USNO** *Astronomical Almanac*. U.S. Government Publishing Office (annual reference for solstice/equinox verification).
