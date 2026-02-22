# LUNAR: Lunar Phase Indicator

LUNAR calculates the Moon's illumination fraction using precise orbital mechanics from Jean Meeus' *Astronomical Algorithms*. Output ranges from 0.0 (New Moon) through 0.5 (Quarter) to 1.0 (Full Moon), providing a continuous astronomical cycle for research into potential lunar-correlated market behavior. The indicator is purely time-based, requires no price data, and has zero warmup since the calculation is deterministic from any timestamp.

## Historical Context

Lunar cycles have guided human activity for millennia. The hypothesis that lunar phases influence human behavior—and by extension, financial markets—dates to early technical analysis and remains a subject of academic investigation. Some studies (Dichev & Janes, 2001; Yuan, Zheng & Zhu, 2006) find statistically significant correlations between lunar phases and market returns, while others dismiss such findings as data mining artifacts. Regardless of one's position, rigorous testing requires precise phase calculation. This implementation derives from Meeus' (1991) standard reference for computational positional astronomy, accounting for major orbital perturbations including the Moon's elliptical orbit (eccentricity $e \approx 0.0549$), solar perturbations, and nodal regression, achieving sub-degree accuracy sufficient for financial cycle research.

## Architecture & Physics

### 1. Julian Date Conversion

Convert Unix timestamp to Julian centuries from J2000 epoch:

$$JD = \frac{UnixMs}{86400000} + 2440587.5$$

$$T = \frac{JD - 2451545.0}{36525.0}$$

### 2. Mean Orbital Elements

Polynomial series (Horner's method) compute five fundamental arguments:

$$L' = 218.3164477 + 481267.88123421T - 0.0015786T^2 + \frac{T^3}{538841}$$

$$D = 297.8501921 + 445267.1114034T - 0.0018819T^2 + \frac{T^3}{545868}$$

$$M = 357.5291092 + 35999.0502909T - 0.0001536T^2$$

$$M' = 134.9633964 + 477198.8675055T + 0.0087414T^2$$

$$F = 93.2720950 + 483202.0175233T - 0.0036539T^2$$

where $L'$ = mean lunar longitude, $D$ = mean elongation, $M$ = solar mean anomaly, $M'$ = lunar mean anomaly, $F$ = lunar argument of latitude.

### 3. Perturbation Corrections

Major periodic terms correct the Moon's true longitude:

$$\Sigma = 6288.016 \sin M' + 1274.242 \sin(2D - M') + 658.314 \sin 2D + 214.818 \sin 2M' + 186.986 \sin M + 109.154 \sin 2F$$

$$\lambda_{Moon} = L' + \frac{\Sigma}{10^6}$$

### 4. Phase Angle

The angular separation between Moon and Sun:

$$\psi = \lambda_{Moon} - \lambda_{Sun}$$

### 5. Illumination Fraction

$$k = \frac{1 - \cos(\psi)}{2}$$

This gives 0.0 at New Moon ($\psi = 0°$) and 1.0 at Full Moon ($\psi = 180°$).

### 6. Complexity

$O(1)$ per timestamp. No state required (deterministic from time). Zero warmup. The synodic period is approximately 29.53 days.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

The calculation is entirely determined by the input timestamp.

### Pseudo-code

```
function LUNAR(timestamp):
    // Julian date
    JD ← timestamp_to_unix_ms / 86400000 + 2440587.5
    T ← (JD - 2451545.0) / 36525.0

    // Mean orbital elements (Horner evaluation)
    Lp ← FMA(T, FMA(T, FMA(T, 1/538841, -0.0015786), 481267.88123421), 218.3164477)
    D  ← FMA(T, FMA(T, FMA(T, 1/545868, -0.0018819), 445267.1114034), 297.8501921)
    M  ← FMA(T, FMA(T, -0.0001536, 35999.0502909), 357.5291092)
    Mp ← FMA(T, FMA(T, 0.0087414, 477198.8675055), 134.9633964)
    F  ← FMA(T, FMA(T, -0.0036539, 483202.0175233), 93.2720950)

    // Normalize to [0°, 360°)
    Lp, D, M, Mp, F ← mod(*, 360)

    // Perturbation correction (6 major terms)
    Σ ← 6288016·sin(Mp) + 1274242·sin(2D - Mp) + 658314·sin(2D)
       + 214818·sin(2Mp) + 186986·sin(M) + 109154·sin(2F)
    λ_moon ← Lp + Σ / 1e6

    // Solar longitude (simplified)
    L0 ← 280.46646 + 36000.76983·T
    M_sun ← 357.52911 + 35999.05029·T
    λ_sun ← L0 + 1.9146·sin(M_sun) + 0.02·sin(2·M_sun)

    // Phase angle and illumination
    ψ ← λ_moon - λ_sun
    k ← (1 - cos(ψ)) / 2

    emit k      // 0.0 = New Moon, 1.0 = Full Moon
```

### Output Interpretation

| Value | Phase |
|-------|-------|
| $k \approx 0.0$ | New Moon |
| $k$ rising, $< 0.5$ | Waxing Crescent |
| $k \approx 0.5$ (rising) | First Quarter |
| $k$ rising, $> 0.5$ | Waxing Gibbous |
| $k \approx 1.0$ | Full Moon |
| $k$ falling, $> 0.5$ | Waning Gibbous |
| $k \approx 0.5$ (falling) | Last Quarter |
| $k$ falling, $< 0.5$ | Waning Crescent |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Julian date conversion | ~4 | 1 DIV + 1 ADD + 1 SUB + 1 DIV |
| Horner polynomial (5 elements) | ~25 | 5 FMA chains (3-4 deep each) |
| Modular reduction (5 elements) | ~5 | 5 `mod 360` operations |
| SIN evaluations (perturbations) | ~48 | 6 `Math.Sin` calls (~8 cycles each) |
| Perturbation sum | ~11 | 6 MUL + 5 ADD |
| Solar longitude (Horner + 2 SIN) | ~20 | 2 FMA + 2 `Math.Sin` + 2 FMA |
| Phase angle + COS | ~10 | 1 SUB + 1 `Math.Cos` + 1 SUB + 1 MUL |
| **Total** | **~123** | **O(1) pure arithmetic; no state, no buffers** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Yes: fully stateless; each timestamp independent; `Vector<double>` applicable to Horner chains |
| Bottleneck | 8 transcendental calls (6 SIN + 1 SIN + 1 COS); ~64 cycles total |
| Parallelism | Full: no inter-bar dependencies; ideal for `Vector<double>` batch processing |
| Memory | O(0): zero state; pure function of timestamp |
| Throughput | Very fast; bulk evaluation benefits from SIMD Horner + vectorized sin/cos |

## Resources

- **Meeus, J.** *Astronomical Algorithms*. 2nd ed., Willmann-Bell, 1998.
- **Dichev, I.D. & Janes, T.D.** "Lunar Cycle Effects in Stock Returns." *Journal of Private Equity*, 2001.
- **Yuan, K., Zheng, L. & Zhu, Q.** "Are Investors Moonstruck? Lunar Phases and Stock Returns." *Journal of Empirical Finance*, 2006.
