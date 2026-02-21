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

Three cascaded EMAs compute $\text{TEMA}_1 = 3 \cdot C_1 - 3 \cdot C_2 + C_3$, where $C_i$ is the warmup-compensated output of EMA stage $i$.

### 2. Outer TEMA (Stages 4-6)

Three more EMAs receive $\text{TEMA}_1$ as input and compute $\text{TEMA}_2 = 3 \cdot C_4 - 3 \cdot C_5 + C_6$.

### 3. DEMA Combination

$$
\text{MCNMA} = 2 \cdot \text{TEMA}_1 - \text{TEMA}_2
$$

### 4. Shared Warmup Compensator

All six stages share a single decay tracker $e = \beta^n$, with compensation factor $c = 1/(1-e)$.

## Mathematical Foundation

With $\alpha = 2/(N+1)$, $\beta = 1 - \alpha$, and warmup compensator $c = 1/(1-\beta^n)$:

**Inner TEMA:**

$$
C_1 = c \cdot E_1, \quad C_2 = c \cdot E_2, \quad C_3 = c \cdot E_3
$$

$$
\text{TEMA}_1 = 3C_1 - 3C_2 + C_3
$$

where $E_1 = \alpha(x - E_1) + E_1$, $E_2 = \alpha(C_1 - E_2) + E_2$, $E_3 = \alpha(C_2 - E_3) + E_3$.

**Outer TEMA:**

$$
C_4 = c \cdot E_4, \quad C_5 = c \cdot E_5, \quad C_6 = c \cdot E_6
$$

$$
\text{TEMA}_2 = 3C_4 - 3C_5 + C_6
$$

where $E_4 = \alpha(\text{TEMA}_1 - E_4) + E_4$, etc.

**Output:**

$$
\text{MCNMA} = 2 \cdot \text{TEMA}_1 - \text{TEMA}_2
$$

**Effective lag:** Near zero for polynomial trends up to degree 3. The six-stage cascade provides approximately $5\times$ less lag than a single EMA of the same period.

**Overshoot risk:** High. The $2\text{TEMA} - \text{TEMA}(\text{TEMA})$ formula amplifies the TEMA's already aggressive lag compensation.

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = 2/(period+1); beta = 1-alpha
e_decay *= beta; comp = 1/(1-e_decay)

// Inner TEMA: 3 cascaded EMAs
e1 += alpha*(src - e1);  c1 = e1*comp
e2 += alpha*(c1 - e2);   c2 = e2*comp
e3 += alpha*(c2 - e3);   c3 = e3*comp
tema1 = 3*c1 - 3*c2 + c3

// Outer TEMA: 3 cascaded EMAs of tema1
e4 += alpha*(tema1 - e4); c4 = e4*comp
e5 += alpha*(c4 - e5);    c5 = e5*comp
e6 += alpha*(c5 - e6);    c6 = e6*comp
tema2 = 3*c4 - 3*c5 + c6

return 2*tema1 - tema2
```

## Resources

- McNicholl, D. (1998). "Better Bollinger Bands." *Futures Magazine*, October 1998.
- Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1), 11-19. (DEMA and TEMA originals.)
- Mulloy, P.G. (1994). "Smoothing Data with Less Lag." *Technical Analysis of Stocks & Commodities*, 12(2). (TEMA continuation.)
