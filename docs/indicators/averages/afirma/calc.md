# The Math Behind AFIRMA

## Components of AFIRMA

AFIRMA is a hybrid beast, combining two main components:

- Autoregressive Moving Average (ARMA)
- Finite Impulse Response (FIR) filter

### ARMA Component

$ X_t = c + \epsilon_t + \sum_{i=1}^p \phi_i X_{t-i} + \sum_{j=1}^q \theta_j \epsilon_{t-j} $

Where:
- $X_t$ is the time series value at time $t$<br>
- $c$ is a constant<br>
- $\phi_i$ are the parameters of the autoregressive term<br>
- $\theta_j$ are the parameters of the moving average term<br>
- $\epsilon_t$ is white noise<br>

### FIR Component

$ y[n] = \sum_{i=0}^{N-1} b_i \cdot x[n-i] $

Where:
- $y[n]$ is the output signal
- $x[n]$ is the input signal
- $b_i$ are the filter coefficients
- $N$ is the filter order

### AFIRMA: Putting It All Together

AFIRMA combines these components and adds cubic spline fitting to the mix. The general form can be expressed as:

$ AFIRMA_t = ARMA_t + FIR_t + CS_t $

Where:
- $ARMA_t$ is the ARMA component at time $t$
- $FIR_t$ is the FIR component at time $t$
- $CS_t$ is the cubic spline fitting component at time $t$

### Digital Filtering Process

- The price data is passed through the digital filter to smooth out fluctuations.
- The filter coefficients are optimized based on the specified parameters (Periods, Taps, Window).

### Cubic Spline Fitting

For the most recent bars:

- A cubic spline is fitted to the data points using the least squares method.
- This ensures a smooth transition between the filtered data and the most recent price movements.

### Parameter Definitions

The AFIRMA indicator allows for the adjustment of three main parameters:

- **Periods**: Affects the overall smoothness of the indicator.
- *Taps*: Influences the complexity of the digital filter.
- *Window*: Determines the number of recent bars to which the cubic spline fitting is applied.

### Computational Process

- Apply the ARMA model to the price data.
- Pass the result through the FIR filter.
- Apply cubic spline fitting to the most recent data points.
- Combine the results to produce the final AFIRMA value.
