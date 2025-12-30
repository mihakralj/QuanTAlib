# Errors

Error metrics and performance indicators for model/strategy evaluation. All error indicators accept two input series (actual and predicted values) and compute rolling error metrics over a configurable period.

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
|:----------|:----------|:------------|
| [HUBER](huber/Huber.md) | Huber Loss | Combines MSE and MAE; less sensitive to outliers |
| [MAE](mae/Mae.md) | Mean Absolute Error | Average of absolute differences |
| [MAPD](mapd/Mapd.md) | Mean Absolute Percentage Deviation | Percentage error relative to mean of actual and predicted |
| [MAPE](mape/Mape.md) | Mean Absolute Percentage Error | Percentage error relative to actual values |
| [MASE](mase/Mase.md) | Mean Absolute Scaled Error | Scale-free error using naive forecast as baseline |
| [ME](me/Me.md) | Mean Error | Average of signed differences (bias detector) |
| [MPE](mpe/Mpe.md) | Mean Percentage Error | Signed percentage error (directional bias) |
| [MSE](mse/Mse.md) | Mean Squared Error | Average of squared differences |
| [MSLE](msle/Msle.md) | Mean Squared Logarithmic Error | MSE on log-transformed values |
| [RAE](rae/Rae.md) | Relative Absolute Error | Absolute error relative to mean predictor |
| [RMSE](rmse/Rmse.md) | Root Mean Squared Error | Square root of MSE; same units as input |
| [RMSLE](rmsle/Rmsle.md) | Root Mean Squared Logarithmic Error | RMSE on log-transformed values |
| [RSE](rse/Rse.md) | Relative Squared Error | Squared error relative to mean predictor |
| [RSQUARED](rsquared/Rsquared.md) | Coefficient of Determination | Proportion of variance explained (1 - RSE) |
| [SMAPE](smape/Smape.md) | Symmetric Mean Absolute Percentage Error | Bounded percentage error (0-200%) |

## Choosing an Error Metric

### By Use Case

| Use Case | Recommended Metrics |
|:---------|:--------------------|
| General accuracy | MAE, RMSE |
| Outlier-robust | MAE, Huber, MASE |
| Percentage interpretation | MAPE, SMAPE, MAPD |
| Bias detection | ME, MPE |
| Scale-free comparison | MASE, RAE, RSE |
| Model quality score | R², RSE |
| Log-scale data | MSLE, RMSLE |

### By Properties

| Metric | Scale | Outlier Sensitivity | Interpretability |
|:-------|:------|:--------------------|:-----------------|
| MAE | Original units | Low | High |
| MSE | Squared units | High | Medium |
| RMSE | Original units | High | High |
| MAPE | Percentage | Medium | High |
| SMAPE | 0-200% | Medium | High |
| Huber | Original units | Low (configurable) | Medium |
| R² | 0-1 (for good models) | High | Very High |
