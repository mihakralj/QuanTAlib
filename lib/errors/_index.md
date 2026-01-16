# Errors

Error metrics and loss functions for model/strategy evaluation. All error indicators accept two input series (actual and predicted values) and compute rolling error metrics over a configurable period.

## Two-Input Pattern

All error indicators in this category follow a consistent dual-input API:

```csharp
// Streaming mode
var mae = new Mae(period: 14);
var result = mae.Update(actualValue, predictedValue);

// Batch mode
var maeSeries = Mae.Calculate(actualSeries, predictedSeries, period: 14);

// Span mode (zero-allocation)
Mae.Batch(actualSpan, predictedSpan, outputSpan, period: 14);
```

## Indicator Reference

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [HUBER](lib/errors/huber/Huber.md) | Huber Loss | Combines MSE and MAE; configurable outlier threshold |
| [LOGCOSH](lib/errors/logcosh/LogCosh.md) | Log-Cosh Loss | Smooth approximation to MAE; twice-differentiable |
| [MAE](lib/errors/mae/Mae.md) | Mean Absolute Error | Average of absolute differences |
| [MAAPE](lib/errors/maape/Maape.md) | Mean Arctangent Absolute Percentage Error | Bounded percentage error using arctangent |
| [MAPD](lib/errors/mapd/Mapd.md) | Mean Absolute Percentage Deviation | Percentage error relative to mean of actual and predicted |
| [MAPE](lib/errors/mape/Mape.md) | Mean Absolute Percentage Error | Percentage error relative to actual values |
| [MASE](lib/errors/mase/Mase.md) | Mean Absolute Scaled Error | Scale-free error using naive forecast as baseline |
| [MDAE](lib/errors/mdae/Mdae.md) | Median Absolute Error | Median of absolute differences; outlier-robust |
| [MDAPE](lib/errors/mdape/Mdape.md) | Median Absolute Percentage Error | Median percentage error; outlier-robust |
| [ME](lib/errors/me/Me.md) | Mean Error | Average of signed differences (bias detector) |
| [MPE](lib/errors/mpe/Mpe.md) | Mean Percentage Error | Signed percentage error (directional bias) |
| [MRAE](lib/errors/mrae/Mrae.md) | Mean Relative Absolute Error | Error relative to naive forecast |
| [MSE](lib/errors/mse/Mse.md) | Mean Squared Error | Average of squared differences |
| [MSLE](lib/errors/msle/Msle.md) | Mean Squared Logarithmic Error | MSE on log-transformed values |
| [PSEUDOHUBER](lib/errors/pseudohuber/PseudoHuber.md) | Pseudo-Huber Loss | Smooth approximation to Huber; fully differentiable |
| [QUANTILE](lib/errors/quantile/QuantileLoss.md) | Quantile Loss | Asymmetric loss for quantile regression |
| [RAE](lib/errors/rae/Rae.md) | Relative Absolute Error | Absolute error relative to mean predictor |
| [RMSE](lib/errors/rmse/Rmse.md) | Root Mean Squared Error | Square root of MSE; same units as input |
| [RMSLE](lib/errors/rmsle/Rmsle.md) | Root Mean Squared Logarithmic Error | RMSE on log-transformed values |
| [RSE](lib/errors/rse/Rse.md) | Relative Squared Error | Squared error relative to mean predictor |
| [RSQUARED](lib/errors/rsquared/Rsquared.md) | Coefficient of Determination | Proportion of variance explained (1 - RSE) |
| [SMAPE](lib/errors/smape/Smape.md) | Symmetric Mean Absolute Percentage Error | Bounded percentage error (0-200%) |
| [THEILU](lib/errors/theilu/TheilU.md) | Theil's U Statistic | Forecast accuracy relative to naive model |
| [TUKEY](lib/errors/tukey/TukeyBiweight.md) | Tukey Biweight Loss | Hard-rejection of outliers beyond threshold |
| [WMAPE](lib/errors/wmape/Wmape.md) | Weighted Mean Absolute Percentage Error | Volume-weighted percentage error |

## Choosing an Error Metric

### By Use Case

| Use Case | Recommended Metrics |
| :--- | :--- |
| General accuracy | MAE, RMSE |
| Outlier-robust | MAE, Huber, MASE, MDAE, Tukey |
| Percentage interpretation | MAPE, SMAPE, MAPD, MAAPE |
| Bias detection | ME, MPE |
| Scale-free comparison | MASE, RAE, RSE, MRAE, TheilU |
| Model quality score | R², RSE |
| Log-scale data | MSLE, RMSLE |
| Gradient optimization | LogCosh, PseudoHuber |
| Quantile forecasting | Quantile Loss |
| Volume-weighted | WMAPE |

### By Properties

| Metric | Scale | Outlier Sensitivity | Complexity |
| :--- | :--- | :--- | :--- |
| MAE | Original units | Low | O(1) |
| MSE | Squared units | High | O(1) |
| RMSE | Original units | High | O(1) |
| MAPE | Percentage | Medium | O(1) |
| SMAPE | 0-200% | Medium | O(1) |
| Huber | Original units | Low (configurable) | O(1) |
| R² | 0-1 (for good models) | High | O(1) |
| MDAE | Original units | Very Low | O(n log n) |
| MDAPE | Percentage | Very Low | O(n log n) |
| LogCosh | Original units | Low | O(1) |
| PseudoHuber | Original units | Low | O(1) |
| Tukey | Original units | Very Low | O(1) |