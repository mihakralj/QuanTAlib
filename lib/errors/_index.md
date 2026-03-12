# Errors

Error metrics and loss functions for model/strategy evaluation. All error indicators accept two input series (actual and predicted values) and compute rolling error metrics over a configurable period.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [HUBER](huber/Huber.md) | Huber Loss | Combines MSE and MAE. Configurable outlier threshold δ. |
| [LOGCOSH](logcosh/Logcosh.md) | Log-Cosh Loss | Smooth approximation to MAE. Twice-differentiable. |
| [MAAPE](maape/Maape.md) | Mean Arctangent APE | Bounded percentage error using arctangent. Range: 0 to π/2. |
| [MAE](mae/Mae.md) | Mean Absolute Error | Average of absolute differences. Robust baseline. |
| [MAPD](mapd/Mapd.md) | Mean Absolute % Deviation | Percentage error relative to mean of actual and predicted. |
| [MAPE](mape/Mape.md) | Mean Absolute % Error | Percentage error relative to actual. Unbounded when actual≈0. |
| [MASE](mase/Mase.md) | Mean Absolute Scaled Error | Scale-free. Uses naive forecast as baseline. |
| [MDAE](mdae/Mdae.md) | Median Absolute Error | Median of absolute differences. Outlier-robust. O(n log n). |
| [MDAPE](mdape/Mdape.md) | Median Absolute % Error | Median percentage error. Outlier-robust. O(n log n). |
| [ME](me/Me.md) | Mean Error | Signed average. Detects systematic bias. |
| [MPE](mpe/Mpe.md) | Mean Percentage Error | Signed percentage. Shows directional bias. |
| [MRAE](mrae/Mrae.md) | Mean Relative Absolute Error | Error relative to naive forecast. |
| [MSE](mse/Mse.md) | Mean Squared Error | Squared differences. Penalizes large errors heavily. |
| [MSLE](msle/Msle.md) | Mean Squared Log Error | MSE on log-transformed values. For multiplicative errors. |
| [PSEUDOHUBER](pseudohuber/Pseudohuber.md) | Pseudo-Huber Loss | Smooth Huber approximation. Fully differentiable. |
| [QUANTILELOSS](quantileloss/Quantileloss.md) | Quantile Loss | Asymmetric loss for quantile regression. Pinball loss. |
| [RAE](rae/Rae.md) | Relative Absolute Error | Absolute error relative to mean predictor. |
| [RMSE](rmse/Rmse.md) | Root Mean Squared Error | √MSE. Same units as input. Penalizes outliers. |
| [RMSLE](rmsle/Rmsle.md) | Root Mean Squared Log Error | √MSLE. For multiplicative error structures. |
| [RSE](rse/Rse.md) | Relative Squared Error | Squared error relative to mean predictor. |
| [RSQUARED](rsquared/Rsquared.md) | R² (Coefficient of Determination) | Variance explained. 1 = perfect. Can be negative. |
| [SMAPE](smape/Smape.md) | Symmetric MAPE | Bounded 0-200%. Symmetric around zero. |
| [THEILU](theilu/Theilu.md) | Theil's U Statistic | Forecast vs naive. <1 beats naive. >1 worse than naive. |
| [TUKEYBIWEIGHT](tukeybiweight/Tukeybiweight.md) | Tukey Biweight Loss | Hard-rejects outliers beyond threshold. Redescending. |
| [WMAPE](wmape/Wmape.md) | Weighted MAPE | Volume-weighted percentage error. For heterogeneous data. |
| [WRMSE](wrmse/Wrmse.md) | Weighted RMSE | Weighted root mean squared error. Custom observation weighting. |
