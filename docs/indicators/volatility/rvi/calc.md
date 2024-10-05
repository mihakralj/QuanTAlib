# The Math Behind RVI

## Components of RVI

The **Relative Volatility Index (RVI)** measures the direction of volatility in the market, using components like:

- Standard deviation of price changes
- Simple moving average (SMA) to smooth volatility
- Separation of up and down price movements

### RVI Formula

The RVI is calculated using the following formula:

$$
\text{RVI}_t = 100 \times \frac{\text{SMA}(\sigma_{\text{up}}, N)}{\text{SMA}(\sigma_{\text{up}}, N) + \text{SMA}(\sigma_{\text{down}}, N)}
$$

Where:
- \( \text{RVI}_t \) is the RVI value at time \( t \)
- \( \sigma_{\text{up}} \) is the standard deviation of up moves over the lookback period \( N \)
- \( \sigma_{\text{down}} \) is the standard deviation of down moves over the lookback period \( N \)
- \( \text{SMA} \) represents the simple moving average applied over \( N \) periods

### Up and Down Move Calculation

The standard deviations \( \sigma_{\text{up}} \) and \( \sigma_{\text{down}} \) are calculated based on the price changes:

$$
\Delta \text{Price} = \text{Close}_t - \text{Close}_{t-1}
$$

- If \( \Delta \text{Price} > 0 \), it contributes to \( \sigma_{\text{up}} \)
- If \( \Delta \text{Price} < 0 \), it contributes to \( \sigma_{\text{down}} \)

### Parameter Definitions

RVI uses the following main parameters:

- **Lookback period** (\( N \)): The number of periods used to calculate the standard deviations and SMAs. A typical value is 14.
- **Smoothing with SMA**: The standard deviations of up and down moves are smoothed using a simple moving average (SMA), making the RVI less sensitive to short-term fluctuations.

### Computational Process

For each new data point:
- Calculate the price change (\( \Delta \text{Price} \)) from the previous period.
- Separate the price changes into up moves and down moves.
- Compute the standard deviations (\( \sigma_{\text{up}} \) and \( \sigma_{\text{down}} \)) over the last \( N \) periods.
- Apply the simple moving average (SMA) to both up and down standard deviations.
- Use the RVI formula to produce the final RVI value.
