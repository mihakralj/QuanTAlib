![Alt text](./img/SMA_chart.svg)
# SMA: Simple Moving Average
SMA is one of the most basic trend-following indicators used in Technical Analysis. It is calculated as the *unweighted mean* of the previous $p$ (period) data-points.


## Calculation

SMA is a rolling calculation looking backwards from the position ${n}$ and is denoted as ${SMA}_{p}{(data)}$ where $p$ represents the period and $data$ represents the list of data points:
$$
SMA_p{(data)} = \frac{1}{p}\sum_{i=n-p+1}^{n} data_i
$$
When calculating the value of next $SMA_{p,next}$ while knowing all previous SMA values, SMA calculation can be reduced to:
$$
SMA_{p,next} = SMA_{p,prev}+\frac{1}{p}\left( data_{n+1}-data_{n+1-p}\right)
$$

## Implementation

``` csharp
SMA_Series mean = new(source: data, period: p, useNaN: false);
```

- `TSeries source` -  List of value tuples (DateTime, double)
- `int period` - Integer representing the period of SMA
- `bool useNaN` - if true, initial values from 1 to period-1 will be replaced with NaN. If false, the initial calculation will return values for SMA(length) instead of SMA(period)

## Comparison & Validation

Validation tests
Performance tests

## Visual analysis





## References
   - https://www.tradingtechnologies.com/help/x-study/technical-indicator-definitions/simple-moving-average-sma/