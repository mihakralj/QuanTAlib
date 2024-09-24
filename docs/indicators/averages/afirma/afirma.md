## AFIRMA: Adaptive Filtering Integrated Recursive Moving Average

### Concept

AFIRMA combines elements of simple moving averages (`SMA`) with adaptive filtering techniques from signal processing. It aims to provide a more responsive indicator that can quickly adjust to market changes while maintaining stability.

### Origin

AFIRMA (Adaptive Filtering Integrated Recursive Moving Average) was developed by Clive Bowsher and Roland Meeks in their 2008 paper titled "*The Dynamics of Economic Functions: Modeling and Forecasting the Yield Curve.*" It was created as an improvement over traditional moving averages, designed to adapt more quickly to changes in financial time series data.

### Key Features

1. **Adaptive Nature**: AFIRMA adjusts its behavior based on recent price movements, allowing it to respond more quickly to significant changes.
2. **Error-based Adaptation**: It uses the error between its last output and the current input to determine how much to adapt.
3. **Customizable Sensitivity**: The alpha parameter allows fine-tuning the indicator's responsiveness.

### Usage

1. **Trend Identification**: AFIRMA can help identify the start or end of trends more quickly than traditional moving averages.
2. **Signal Generation**: Traders may use crossovers of AFIRMA with price or other indicators to generate buy/sell signals.
3. **Dynamic Support/Resistance**: The AFIRMA line can act as a dynamic support or resistance level.
4. **Volatility Analysis**: The adaptive nature of AFIRMA can provide insights into market volatility.

### Advantages

- More responsive to market changes compared to traditional moving averages.
- Reduces lag typically associated with moving averages.
- Customizable through its `alpha` parameter to suit different market conditions or trading styles.

### Considerations

- **Adaptive Factor**: Alpha serves as the base adaptive factor. It has a range between 0.0 and 1.0 that determines how quickly the AFIRMA responds to changes in the input data.
- **Error Sensitivity**: Alpha is used in calculating the adaptive factor, which is based on the error between the current input and the last AFIRMA value.
- **Balancing Stability and Responsiveness**: A smaller alpha (closer to 0.0) makes the AFIRMA more stable but less responsive to recent changes.
A larger alpha (closer to 1) makes the AFIRMA more responsive to recent changes but potentially more volatile.
- **Fine-tuning the Indicator**:
    - In trending markets, a higher alpha might be preferred to capture price movements more quickly.
    - In ranging markets, a lower alpha might be better to reduce false signals from price noise.
- **Adaptive Nature**: The use of alpha allows AFIRMA to adapt its behavior based on recent price movements. When there are significant changes (large errors), the adaptive factor increases, allowing AFIRMA to adjust more quickly.