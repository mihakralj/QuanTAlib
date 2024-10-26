# The Math Behind ALMA

## Components of ALMA

ALMA is a single-formula moving average that incorporates elements of several advanced techniques:

- Gaussian distribution
- Weighted moving average
- Offset parameter

### ALMA Formula

$ ALMA_t = \sum_{i=0}^{n-1} w_i \cdot P_{t-i} $

Where:
- $ALMA_t$ is the ALMA value at time $t$
- $n$ is the window size (number of periods)
- $P_{t-i}$ is the price at time $t-i$
- $w_i$ are the weights

### Weight Calculation

The weights $w_i$ are calculated using a Gaussian distribution function with an offset:

$ w_i = \exp\left(-\frac{(i - m)^2}{2s^2}\right) $

Where:
- $i$ is the position of the price in the window (0 to $n-1$)
- $m$ is the offset of the Gaussian distribution, calculated as $m = \text{floor}(offset \cdot (n - 1))$
- $s$ is the standard deviation of the Gaussian distribution, calculated as $s = \frac{n}{sigma}$

### Parameter Definitions

ALMA uses three main parameters:

- **Window size** ($n$): Affects the overall reactivity of the indicator.
- **Offset**: Influences the lag of the moving average. Lower values reduce lag but may increase noise.
- **Sigma**: Controls the smoothness of the indicator. Higher values increase smoothness but may increase lag.

### Computational Process

For each new data point:
- Calculate the weights for the entire window.
- Apply these weights to the most recent $n$ prices.
- Sum the weighted prices to produce the final ALMA value.
