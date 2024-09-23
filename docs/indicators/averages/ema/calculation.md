## EMA - Calculation Method

The EMA calculation utilizes a weighting multiplier, typically denoted as the smoothing factor ($alpha$). This factor is calculated as:

$alpha = \frac{2}{period + 1}$

where 'period' represents the chosen period for the EMA.

The general formula for EMA required for arithmetic operations:

$EMA_n = (data_{n} \times alpha) + (EMA_{n-1} \times (1 - alpha))$

or in optimized form (requires only three arithmetic operations instead of four):

$EMA_n = {alpha}\times ({data_{n}} - EMA_{n-1}) + EMA_{n-1}$



When calculating the Exponential Moving Average (EMA) and there is not enough data (n < period), several approaches can be considered. Each method has its own pros and cons:

#### 1. Assume all previous values were 0

$EMA_0 = 0$ \
$EMA_n = alpha \times (data_n - EMA_{n-1}) + EMA_{n-1}$

- Will lead to significant underestimation of EMA in early periods

#### 2. Calculate as if all previous values were the same as the first value

$EMA_0 = data_0$ \
$EMA_n = alpha \times (data_n - EMA_{n-1}) + EMA_{n-1}$

- Will overestimate early EMA if initial data point is far from representative

#### 3. Use SMA instead of EMA for the first period

$EMA_n = \left\{ \begin{array}{cl}
\frac{1}{p}\left( data_{n}-data_{n-p}\right)+SMA_{n-1} & : \ n \leq period \\
{alpha}\times ({data_{n}} - EMA_{n-1}) + EMA_{n-1} & : \ n > period
\end{array} \right.$

- Creates a discontinuity when switching from SMA to EMA



### Conclusion

The choice of method depends on the specific requirements of the application:

- Method 1 is suitable for applications where underestimation in early periods is acceptable.
- Method 2 is beneficial when a smooth transition is crucial and the initial data point is representative.
- Method 3 is appropriate when simplicity is preferred and a clear distinction between SMA and EMA is acceptable.
- Method 4 offers a good balance between adaptability and maintaining the EMA concept, but may require additional explanation to users.


