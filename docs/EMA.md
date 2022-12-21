# EMA: Exponential Moving Average

EMA needs very short history buffer and calculates the EMA value using just the previous EMA value. The weight of the new datapoint (k) is k = 2 / (period-1)

## Calculation

There is an adopted practice to calculate $SMA$ when $n < period$.

$$
EMA_n = \left\{ \begin{array}{cl}
\frac{1}{p}\left( data_{n}-data_{n-p}\right)+SMA_{n-1} & : \ n \leq period \\
{k}\times ({data_{n}} - EMA_{n-1}) + EMA_{n-1} & : \ x > period
\end{array} \right.
$$


## Implementation

``` csharp
EMA_Series mean = new(source: data, period: p, useNaN: false);
```

- `TSeries source` -  List of value tuples (DateTime, double)
- `int period` - Integer representing the period of SMA
- `bool useNaN` - if true, initial values from 1 to period-1 will be replaced with NaN. If false, the initial calculation will return values for SMA(length) instead of SMA(period)

## Comparison & Validation

Validation tests
Performance tests

## Visual analysis

![Alt text](./img/EMA_chart.svg)



## References
