graph TD
    A[QuanTAlib] --> B[lib]
    B --> C[core]
    B --> D[averages]
    B --> E[momentum]
    B --> F[oscillators]
    B --> G[errors]
    B --> H[feeds]
    B --> I[patterns]
    B --> J[statistics]
    B --> K[volatility]
    B --> L[volume]

    C --> C1[AbstractBase]
    C --> C2[TBarSeries]
    C --> C3[TSeries]
    C --> C4[CircularBuffer]

    D --> D1[EMA]
    D --> D2[SMA]
    D --> D3[WMA]

    E --> E1[MACD]
    E --> E2[ROC]
    E --> E3[ADX]

    F --> F1[RSI]
    F --> F2[Stoch]
    F --> F3[CCI]
