# MCNMA: McNicholl EMA (Zero-Lag TEMA)

> "Dennis McNicholl applied TEMA to itself and subtracted the result, producing six cascaded EMA stages that cancel lag through three layers of triple-smoothing. When single TEMA is not enough, double it."

MCNMA computes $2 \times \text{TEMA}(x, N) - \text{TEMA}(\text{TEMA}(x, N), N)$, applying the DEMA lag-cancellation technique to TEMA itself. This requires six cascaded EMA stages: three for the inner TEMA and three for the outer TEMA of the inner TEMA's output. The result is an extremely responsive moving average that tracks fast trends with minimal lag, at the cost of significant overshoot on reversals. Published by Dennis McNicholl in "Better Bollinger Bands" (*Futures Magazine*, October 1998) as a component for improved volatility band construction.

## Historical Context

Dennis McNicholl published MCNMA as part of his "Better Bollinger Bands" article in *Futures Magazine* (October 1998), where he argued that standard Bollinger Bands use SMA as the center line, introducing unnecessary lag. His solution was to use a zero-lag moving average derived from nested triple exponential smoothing.

MCNMA is the logical extension of Mulloy's lag-cancellation hierarchy:
- **DEMA** (1994): $2\text{EMA}_1 - \text{EMA}_2$ (2 stages, cancels first-order lag)
- **TEMA** (1994): $3\text{EMA}_1 - 3\text{EMA}_2 + \text{EMA}_3$ (3 stages, cancels first and second-order lag)
- **MCNMA** (1998): $2\text{TEMA}_1 - \text{TEMA}_2$ where $\text{TEMA}_2 = \text{TEMA}(\text{TEMA}_1)$ (6 stages, cancels through third order)

Each additional stage of nesting removes another order of lag, but also amplifies noise and overshoot. MCNMA represents the practical limit of this approach; further nesting produces filters that oscillate around price rather than tracking it.

## Architecture & Physics

### 1. Inner TEMA (Stages 1-3)

Three cascaded EMAs compute $\text{TEMA}_1 = 3 \cdot E_1 - 3 \cdot E_2 + E_3$, where $E_i$ is the raw EMA output of stage $i$.

### 2. Outer TEMA (Stages 4-6)

Three more EMAs receive $\text{TEMA}_1$ as input and compute $\text{TEMA}_2 = 3 \cdot E_4 - 3 \cdot E_5 + E_6$.

### 3. DEMA Combination

$$
\text{MCNMA} = 2 \cdot \text{TEMA}_1 - \text{TEMA}_2
$$

### 4. First-Value Seeding

All six EMA stages are initialized to the first source value. This eliminates warmup bias without a compensator and produces output from bar 1. On the first bar, all stages equal the source, so $\text{TEMA}_1 = \text{TEMA}_2 = \text{source}$ and $\text{MCNMA} = \text{source}$.

## Mathematical Foundation

With $\alpha = 2/(N+1)$ and $\beta = 1 - \alpha$:

**Inner TEMA:**

$$
E_1 = \alpha \cdot x + \beta \cdot E_1, \quad E_2 = \alpha \cdot E_1 + \beta \cdot E_2, \quad E_3 = \alpha \cdot E_2 + \beta \cdot E_3
$$

$$
\text{TEMA}_1 = 3E_1 - 3E_2 + E_3
$$

**Outer TEMA:**

$$
E_4 = \alpha \cdot \text{TEMA}_1 + \beta \cdot E_4, \quad E_5 = \alpha \cdot E_4 + \beta \cdot E_5, \quad E_6 = \alpha \cdot E_5 + \beta \cdot E_6
$$

$$
\text{TEMA}_2 = 3E_4 - 3E_5 + E_6
$$

**Output:**

$$
\text{MCNMA} = 2 \cdot \text{TEMA}_1 - \text{TEMA}_2
$$

**Initialization:** $E_1 = E_2 = E_3 = E_4 = E_5 = E_6 = x_0$ (first source value).

**Effective lag:** Near zero for polynomial trends up to degree 3. The six-stage cascade provides approximately $5\times$ less lag than a single EMA of the same period.

**Overshoot risk:** High. The $2\text{TEMA} - \text{TEMA}(\text{TEMA})$ formula amplifies the TEMA's already aggressive lag compensation.

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = 2/(period+1); beta = 1-alpha

// First bar: seed all 6 stages to source
if not initialized:
    e1 = e2 = e3 = e4 = e5 = e6 = src
    return src

// Inner TEMA: 3 cascaded EMAs
e1 = alpha*src + beta*e1
e2 = alpha*e1  + beta*e2
e3 = alpha*e2  + beta*e3
tema1 = 3*e1 - 3*e2 + e3

// Outer TEMA: 3 cascaded EMAs of tema1
e4 = alpha*tema1 + beta*e4
e5 = alpha*e4    + beta*e5
e6 = alpha*e5    + beta*e6
tema2 = 3*e4 - 3*e5 + e6

return 2*tema1 - tema2
```

## Resources

- McNicholl, D. (1998). "Better Bollinger Bands." *Futures Magazine*, October 1998.
- Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1), 11-19. (DEMA and TEMA originals.)
- Mulloy, P.G. (1994). "Smoothing Data with Less Lag." *Technical Analysis of Stocks & Commodities*, 12(2). (TEMA continuation.)
