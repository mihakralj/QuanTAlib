# Forecasts

> "Prediction is very difficult, especially about the future."  Niels Bohr

Forecasting and predictive models. Unlike reactive indicators that smooth past data, forecasts attempt to project future values. Extrapolation is inherently uncertain. Use with appropriate skepticism and position sizing.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [AFIRMA](lib/forecasts/afirma/Afirma.md) | Adaptive FIR Moving Average |  | Windowed sinc coefficients. Optimal frequency response. Can extrapolate. |
| CFO | Chande Forecast Oscillator | =Ë | Percentage difference between price and linear regression forecast. |
| MLP | Multilayer Perceptron | =Ë | Neural network regressor. Nonlinear pattern learning. |
| TSF | Time Series Forecast | =Ë | Linear regression projected forward. Standard extrapolation. |

**Status Key:**  Implemented | =Ë Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Smooth extrapolation | AFIRMA | FIR with extrapolation coefficients. Configurable lookahead. |
| Linear trend projection | TSF | Simple, interpretable. Works when trend is linear. |
| Forecast deviation | CFO | Shows when price diverges from linear forecast. |
| Nonlinear patterns | MLP | Neural network learns complex relationships. Requires training. |

## Forecasting Principles

| Aspect | Reality | Implication |
| :--- | :--- | :--- |
| Extrapolation risk | Markets are non-stationary | Short horizons more reliable |
| Model uncertainty | All models are wrong | Use ensemble or confidence intervals |
| Regime changes | Past patterns may not repeat | Monitor forecast errors |
| Overfitting | Complex models fit noise | Prefer simple models when possible |

Forecasting is not prediction. It is disciplined extrapolation of patterns that may or may not persist.