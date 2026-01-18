# Errors

> "All models are wrong. Error metrics tell you how wrong." — Adapted from George Box

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

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [HUBER](lib/errors/huber/Huber.md) | Huber Loss | ✅ | Combines MSE and MAE. Configurable outlier threshold δ. |
| [LOGCOSH](lib/errors/logcosh/LogCosh.md) | Log-Cosh Loss | ✅ | Smooth approximation to MAE. Twice-differentiable. |
| [MAE](lib/errors/mae/Mae.md) | Mean Absolute Error | ✅ | Average of absolute differences. Robust baseline. |
| [MAAPE](lib/errors/maape/Maape.md) | Mean Arctangent APE | ✅ | Bounded percentage error using arctangent. Range: 0 to π/2. |
| [MAPD](lib/errors/mapd/Mapd.md) | Mean Absolute % Deviation | ✅ | Percentage error relative to mean of actual and predicted. |
| [MAPE](lib/errors/mape/Mape.md) | Mean Absolute % Error | ✅ | Percentage error relative to actual. Unbounded when actual≈0. |
| [MASE](lib/errors/mase/Mase.md) | Mean Absolute Scaled Error | ✅ | Scale-free. Uses naive forecast as baseline. |
| [MDAE](lib/errors/mdae/Mdae.md) | Median Absolute Error | ✅ | Median of absolute differences. Outlier-robust. O(n log n). |
| [MDAPE](lib/errors/mdape/Mdape.md) | Median Absolute % Error | ✅ | Median percentage error. Outlier-robust. O(n log n). |
| [ME](lib/errors/me/Me.md) | Mean Error | ✅ | Signed average. Detects systematic bias. |
| [MPE](lib/errors/mpe/Mpe.md) | Mean Percentage Error | ✅ | Signed percentage. Shows directional bias. |
| [MRAE](lib/errors/mrae/Mrae.md) | Mean Relative Absolute Error | ✅ | Error relative to naive forecast. |
| [MSE](lib/errors/mse/Mse.md) | Mean Squared Error | ✅ | Squared differences. Penalizes large errors heavily. |
| [MSLE](lib/errors/msle/Msle.md) | Mean Squared Log Error | ✅ | MSE on log-transformed values. For multiplicative errors. |
| [PSEUDOHUBER](lib/errors/pseudohuber/PseudoHuber.md) | Pseudo-Huber Loss | ✅ | Smooth Huber approximation. Fully differentiable. |
| [QUANTILE](lib/errors/quantile/QuantileLoss.md) | Quantile Loss | ✅ | Asymmetric loss for quantile regression. Pinball loss. |
| [RAE](lib/errors/rae/Rae.md) | Relative Absolute Error | ✅ | Absolute error relative to mean predictor. |
| [RMSE](lib/errors/rmse/Rmse.md) | Root Mean Squared Error | ✅ | √MSE. Same units as input. Penalizes outliers. |
| [RMSLE](lib/errors/rmsle/Rmsle.md) | Root Mean Squared Log Error | ✅ | √MSLE. For multiplicative error structures. |
| [RSE](lib/errors/rse/Rse.md) | Relative Squared Error | ✅ | Squared error relative to mean predictor. |
| [RSQUARED](lib/errors/rsquared/Rsquared.md) | R² (Coefficient of Determination) | ✅ | Variance explained. 1 = perfect. Can be negative. |
| [SMAPE](lib/errors/smape/Smape.md) | Symmetric MAPE | ✅ | Bounded 0-200%. Symmetric around zero. |
| [THEILU](lib/errors/theilu/TheilU.md) | Theil's U Statistic | ✅ | Forecast vs naive. <1 beats naive. >1 worse than naive. |
| [TUKEY](lib/errors/tukey/TukeyBiweight.md) | Tukey Biweight Loss | ✅ | Hard-rejects outliers beyond threshold. Redescending. |
| [WMAPE](lib/errors/wmape/Wmape.md) | Weighted MAPE | ✅ | Volume-weighted percentage error. For heterogeneous data. |
| [WRMSE](lib/errors/wrmse/Wrmse.md) | Weighted RMSE | ✅ | Weighted root mean squared error. Custom observation weighting. |

**Status Key:** ✅ Implemented | 📋 Planned

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