# BINOMDIST: Binomial Distribution CDF

BINOMDIST computes the cumulative distribution function of the Binomial distribution, mapping a min-max normalized price to a success probability $p$ and evaluating $P(X \leq k)$ for $X \sim \text{Binomial}(n, p)$. The normalized price position within its lookback range determines the probability of success per trial, while the trial count $n$ and threshold $k$ control the shape of the CDF response. The output is a $[0, 1]$ bounded oscillator where values near 0 indicate the price-derived probability makes $k$ or fewer successes very unlikely (bullish pressure), and values near 1 indicate $k$ successes are very likely (established range).

## Historical Context

The Binomial distribution, formalized by Jakob Bernoulli in 1713 and refined by Abraham de Moivre, is the foundational discrete probability distribution for counting successes in independent trials. Its CDF application to financial time series transforms the continuous price position into a discrete probabilistic framework: "given the current price's relative position as a probability, how likely is it that at most $k$ out of $n$ events would succeed?" This reframing provides a nonlinear transformation that is particularly sensitive around the probability values where $k/n$ transitions from unlikely to likely. The log-space summation technique used here avoids factorial overflow for large $n$, leveraging the Lanczos log-gamma approximation for $\ln(n!)$ computation.

## Architecture & Physics

### Two-Stage Pipeline

1. **Min-Max Normalization:** The source is normalized to $p \in [0, 1]$ over the lookback window. This probability represents the "success rate" implied by the price's position within its recent range.

2. **Binomial CDF Summation:** The CDF $P(X \leq k)$ is computed as a direct sum of binomial probabilities from $i = 0$ to $k$. Each term is computed in log-space to avoid overflow: $\ln\binom{n}{i} + i\ln(p) + (n-i)\ln(1-p)$, then exponentiated and accumulated. The log-binomial coefficient uses the Lanczos log-gamma function.

### Edge Cases

- $p \leq 0$: All mass at $X = 0$, so $P(X \leq k) = 1$ for any $k \geq 0$
- $p \geq 1$: All mass at $X = n$, so $P(X \leq k) = 1$ only if $k \geq n$
- Result is clamped to $[0, 1]$ to guard against floating-point accumulation drift

## Mathematical Foundation

**Binomial CDF:**

$$P(X \leq k) = \sum_{i=0}^{k} \binom{n}{i} p^i (1-p)^{n-i}$$

**Log-space computation** (avoids factorial overflow):

$$\ln\binom{n}{i} = \ln\Gamma(n+1) - \ln\Gamma(i+1) - \ln\Gamma(n-i+1)$$

$$P(X \leq k) = \sum_{i=0}^{k} \exp\!\left[\ln\binom{n}{i} + i\ln(p) + (n-i)\ln(1-p)\right]$$

**Lanczos log-gamma** ($g = 7$, 9 coefficients): same as BETADIST.

**Default parameters:** period = 50, trials = 20, threshold = 10 (symmetric: $k = n/2$).

## Resources

- Bernoulli, J. (1713). *Ars Conjectandi*
- Press, W. et al. (2007). *Numerical Recipes*, 3rd ed., §6.2 (Incomplete Beta as alternative)
- PineScript reference: [`binomdist.pine`](binomdist.pine)
