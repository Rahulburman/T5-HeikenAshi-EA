# MT5 Heiken Ashi Stacking EA

A MetaTrader 5 Expert Advisor that uses Heiken Ashi candle color changes for automated trading with position stacking capabilities.

## Features

### Trading Logic
- **Buy Entry**: Red candle changes to Green (bullish transition)
- **Buy Exit**: Any Red candle appears
- **Sell Entry**: Green candle changes to Red (bearish transition)  
- **Sell Exit**: Any Green candle appears

### Position Management
- **Position Stacking**: Add multiple positions after initial entry (default: 5 max positions)
- **Stacking Mode**: Toggle between stacking enabled/disabled
- **Position Tracking**: Real-time count of active buy/sell positions

### Risk Management
- **Stop Loss**: Configurable stop loss in points
- **Take Profit**: Configurable take profit in points
- **Slippage Control**: Maximum slippage setting for order placement
- **Lot Normalization**: Automatic lot size normalization based on symbol requirements

### Filtering
- **Volume Filter**: Boolean toggle for symbol minimum volume filter
- **Volume Multiplier**: Adjustable volume multiplier for filtering
- **Session Filter**: Optional day trading mode with configurable hours

### Visualization
- **Chart Comments**: Real-time EA status display on chart
- **Position Counter**: Shows current positions vs maximum allowed
- **Signal Display**: Current candle color and settings

## Input Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| LotSize | double | 0.1 | Initial lot size per trade |
| MaxStackingPositions | int | 5 | Maximum number of positions to stack |
| EnableStacking | enum | ENABLED | Enable/disable position stacking |
| MagicNumber | int | 123456 | Magic number for order identification |
| MaxSlippage | int | 10 | Maximum slippage in points |
| UseSymbolMinVolume | bool | true | Enable/disable volume filter |
| MinimumVolumeMultiplier | double | 1.0 | Volume multiplier for filter |
| StopLoss | double | 50 | Stop loss in points |
| TakeProfit | double | 100 | Take profit in points |
| OnlyDayTrading | bool | false | Restrict trading to specific hours |
| DayStartHour | int | 9 | Day trading start hour |
| DayEndHour | int | 17 | Day trading end hour |
| ShowComments | bool | true | Display EA info on chart |

## Installation

1. Download `HeikenAshi_StackingEA.mq5`
2. Place in MetaTrader 5 `Experts` folder: `C:\Users\[YourUsername]\AppData\Roaming\MetaQuotes\Terminal\[TerminalNumber]\MQL5\Experts\`
3. Restart MetaTrader 5
4. Open the Expert tab in Navigator
5. Drag the EA onto your desired chart
6. Configure parameters as needed
7. Enable automated trading

## Requirements

- MetaTrader 5 Terminal
- Heiken Ashi indicator available in your platform
- Trade permissions enabled in EA settings

## How It Works

### Signal Detection
- The EA monitors Heiken Ashi candle colors every new bar
- Detects color transitions (Red→Green or Green→Red)
- Triggers buy/sell orders on transitions
- Exits positions when opposite color appears

### Position Stacking
- After initial entry signal, EA can add additional positions
- Limited by `MaxStackingPositions` parameter (default 5)
- Each stacked position has independent SL/TP
- All positions tracked and managed individually

### Volume Filtering
- When enabled, checks if current bar volume meets minimum requirements
- Prevents trading during low-volume periods
- Adjustable via `MinimumVolumeMultiplier`

## Trading Example

```
Bar 1: Red candle → No signal
Bar 2: Green candle (Red→Green) → BUY SIGNAL (Position 1)
Bar 3: Green candle → No signal
Bar 4: Green candle → BUY SIGNAL (Position 2 - Stacking)
Bar 5: Green candle → BUY SIGNAL (Position 3 - Stacking)
Bar 6: Red candle → CLOSE ALL BUY POSITIONS
```

## Disclaimer

This EA is provided for educational and testing purposes. Always test on a demo account before using on a live account. Past performance does not guarantee future results. Trading Forex involves risk.

## License

This code is provided as-is for personal use. Modification and distribution are allowed with proper attribution.

## Support

For issues or questions, please refer to the MetaTrader 5 documentation or MQL5 community forums.
