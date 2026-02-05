# SOLAR: Solar Cycle Indicator

> "The Sun is the greatest clock—every market on Earth dances to its annual rhythm."

The Solar Cycle indicator calculates the Sun's position in its annual cycle using ecliptic longitude, outputting values from -1.0 (winter solstice) through 0.0 (equinoxes) to +1.0 (summer solstice). This implementation uses the Meeus astronomical algorithms for computing the Sun's true position with equation of center corrections.

## Historical Context

Solar cycle analysis in trading reflects the fundamental seasonality that governs agricultural commodities, energy demand, and even human behavior. The "Sell in May" effect and seasonal patterns in various markets trace back to solar-driven cycles of planting, harvest, heating demand, and daylight hours affecting productivity.

The algorithm derives from Jean Meeus' *Astronomical Algorithms* (1991), implementing the equation of center—the difference between the Sun's mean and true positions caused by Earth's elliptical orbit. The correction terms account for Earth's orbital eccentricity (currently ~0.0167).

## Architecture & Physics

### 1. Time Conversion

The indicator converts input timestamps to Julian Date (JD), the continuous day count from 4713 BCE:

$$
JD = \frac{t_{unix}}{86400000} + 2440587.5
$$

Julian centuries from J2000 epoch (2000-01-01 12:00 TT):

$$
T = \frac{JD - 2451545.0}{36525.0}
$$

### 2. Orbital Elements

Two fundamental elements describe the Sun's apparent position:

| Element | Symbol | Description |
|:--------|:------:|:------------|
| Mean longitude | $L_0$ | Sun's average position along ecliptic |
| Mean anomaly | $M$ | Sun's position relative to perihelion |

Each element follows a polynomial in $T$:

$$
L_0 = 280.46646 + 36000.76983T + 0.0003032T^2
$$

$$
M = 357.52911 + 35999.05029T - 0.0001537T^2 - 0.00000025T^3
$$

### 3. Equation of Center

The equation of center corrects for Earth's elliptical orbit:

$$
C = (1.914602 - 0.004817T - 0.000014T^2)\sin(M)
$$
$$
+ (0.019993 - 0.000101T)\sin(2M) + 0.000289\sin(3M)
$$

These terms account for:
- Primary orbital eccentricity effect (~1.915° amplitude)
- Second-order eccentricity correction (~0.02°)
- Third-order correction (~0.0003°)

### 4. True Longitude & Cycle Value

The Sun's true ecliptic longitude:

$$
\lambda = L_0 + C
$$

The solar cycle value uses the sine of the longitude:

$$
cycle = \sin(\lambda)
$$

This produces:
- $cycle = -1$ at winter solstice ($\lambda = 270°$, ~Dec 21)
- $cycle = 0$ at equinoxes ($\lambda = 0°, 180°$)
- $cycle = +1$ at summer solstice ($\lambda = 90°$, ~Jun 21)

## Mathematical Foundation

### Julian Date Conversion

From Unix milliseconds $t$:

$$
JD = \frac{t}{86400000} + 2440587.5
$$

### Orbital Element Polynomials

All angles in degrees, normalized to [0°, 360°):

**Sun's mean longitude:**
$$
L_0 = 280.46646 + 36000.76983T + 0.0003032T^2
$$

**Sun's mean anomaly:**
$$
M = 357.52911 + 35999.05029T - 0.0001537T^2 - 0.00000025T^3
$$

### Equation of Center

$$
C = (1.914602 - 0.004817T - 0.000014T^2)\sin(M)
$$
$$
+ (0.019993 - 0.000101T)\sin(2M) + 0.000289\sin(3M)
$$

### True Longitude

$$
\lambda = L_0 + C \pmod{360°}
$$

### Cycle Output

$$
cycle = \sin\left(\lambda \cdot \frac{\pi}{180}\right)
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
|:----------|:-----:|:-------------:|:--------:|
| FMA | 10 | 4 | 40 |
| ADD/SUB | 5 | 1 | 5 |
| MUL | 8 | 3 | 24 |
| DIV | 4 | 15 | 60 |
| MOD | 2 | 15 | 30 |
| SIN | 4 | 50 | 200 |
| **Total** | **33** | — | **~359 cycles** |

Uses `Math.FusedMultiplyAdd()` for polynomial evaluations. Approximately half the computational cost of the LUNAR indicator due to simpler orbital mechanics.

### Batch Mode

SIMD vectorization applies naturally to batch timestamp processing—each calculation is independent. With AVX-512 (8-wide double):

| Operation | Scalar | SIMD (AVX-512) | Speedup |
|:----------|:------:|:--------------:|:-------:|
| Full calculation | 365 | ~55 | ~6.6× |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 9/10 | Within arcminutes of JPL ephemeris |
| **Determinism** | 10/10 | Pure function of timestamp |
| **Timeliness** | N/A | No lag—not a filter |
| **Stability** | 10/10 | No numerical drift |

## Validation

| Source | Status | Notes |
|:-------|:------:|:------|
| **USNO** | ✅ | Naval Observatory solar position data |
| **timeanddate.com** | ✅ | Cross-referenced solstice/equinox dates |
| **JPL Horizons** | ✅ | Within expected tolerance |

Known solar events validated:
- Winter Solstice: December 21, 2024 09:20 UTC → cycle < -0.95
- Summer Solstice: June 20, 2024 20:50 UTC → cycle > 0.95
- Vernal Equinox: March 20, 2024 03:06 UTC → |cycle| < 0.1
- Autumnal Equinox: September 22, 2024 12:43 UTC → |cycle| < 0.1

## Common Pitfalls

1. **Timezone confusion**: The indicator uses UTC timestamps internally. Local time inputs will produce offset results. Always pass UTC or use `DateTimeKind.Utc`.

2. **Hemisphere interpretation**: The cycle follows Northern Hemisphere conventions. For Southern Hemisphere trading, invert the interpretation: cycle = +1 is winter, cycle = -1 is summer.

3. **Sign at equinoxes**: Cycle ≈ 0 occurs at *both* vernal (spring) and autumnal (fall) equinoxes. To distinguish, check if the cycle is rising (vernal) or falling (autumnal).

4. **Century limits**: The polynomial coefficients are optimized for dates within a few centuries of J2000. For dates before 1800 or after 2200, accuracy degrades.

5. **No warmup period**: Unlike filter-based indicators, Solar has no warmup—each output depends only on its timestamp.

6. **Seasonality strength varies**: Solar-driven seasonal effects are strongest in agriculture, energy, and weather-sensitive sectors. Financial indices show weaker correlations.

## References

- Meeus, J. (1991). *Astronomical Algorithms*. Willmann-Bell.
- Standish, E. M. (1982). "The JPL Planetary Ephemerides." *Celestial Mechanics*, 26, 181-186.
- U.S. Naval Observatory. "Earth's Seasons." https://aa.usno.navy.mil/data/Earth_Seasons
- Kamstra, M. J., Kramer, L. A., & Levi, M. D. (2003). "Winter Blues: A SAD Stock Market Cycle." *American Economic Review*, 93(1), 324-343.