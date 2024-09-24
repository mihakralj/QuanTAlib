## DEMA: Double Exponential Moving Average

### Concept

DEMA is an enhanced version of the Exponential Moving Average (EMA) designed to reduce lag while maintaining smoothness. It achieves this by calculating an EMA of an EMA and then using a formula to reduce the inherent lag - at the expense of overshooting the signal line.

### Origin

DEMA was developed by Patrick Mulloy and first introduced in the February 1994 issue of *Technical Analysis of Stocks & Commodities magazine*. It was created to address the lag issue in traditional moving averages, particularly in trend identification and signal generation.

### Key Features

1. **Double Smoothing**: Uses two EMAs in its calculation, providing a smoother output than a single EMA.
2. **Lag Reduction**: Employs a formula to reduce the lag typically associated with moving averages.
3. **Responsiveness**: More responsive to price changes than a standard EMA of the same period, at the expense of overshooting.
4. **Trend Sensitivity**: Better at capturing trends and reacting to reversals than traditional moving averages.

### Usage

1. **Trend Identification**: DEMA can identify trends more quickly than traditional moving averages due to its reduced lag.
2. **Signal Generation**: Crossovers between DEMA and price, or between different DEMA settings, can generate trading signals.
3. **Support and Resistance**: DEMA can act as dynamic support and resistance levels.
4. **Smoothing Price Action**: Useful for smoothing noisy price data while preserving important trend information.

### Advantages

- Reduces lag compared to simple and exponential moving averages.
- More responsive to price changes than traditional EMAs.
- Maintains smoothness despite increased responsiveness.
- Can be more effective in capturing short to medium-term price movements.
- Simple to understand conceptually, building on the familiar EMA.

### Considerations

- **Period**: As with other moving averages, determines how many price bars are included in the calculation. The period affects both EMAs used in the DEMA calculation.

- **Calculation**: The formula for DEMA is:
  DEMA = 2 * EMA(price) - EMA(EMA(price))<br>
  This formula effectively doubles the percentage of EMA weight applied to the most recent price.

- **Sensitivity**:
  - DEMA is more sensitive to price changes than a standard EMA of the same period.
  - This increased sensitivity can lead to earlier signals but may also result in more false signals in choppy markets.

- **Balancing Responsiveness and Stability**:
  - Shorter periods increase responsiveness but may lead to more false signals in volatile markets.
  - Longer periods increase smoothness but may introduce more lag.

- **Comparison to Other MAs**:
  - DEMA typically responds faster than EMA, SMA, or triangular MA of the same period.
  - It may be less smooth than a triple exponential moving average (TEMA) but with less lag.

- **Whipsaws**: Due to its responsiveness, DEMA may be prone to whipsaws in ranging or choppy markets.

- **Multiple Time Frame Analysis**: Using DEMAs on different time frames can provide a more comprehensive view of trends and potential reversals.

- **Computational Complexity**: Slightly more complex to calculate than simple or exponential moving averages, which may be a minor consideration in high-frequency trading systems.

- **Interpretation**: While more responsive than traditional EMAs, traders should still be aware that DEMA is a lagging indicator by nature, and should be used in conjunction with other technical analysis tools for confirmation.