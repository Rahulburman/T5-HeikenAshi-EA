//+------------------------------------------------------------------+
//|                                      HeikenAshi_StackingEA.mq5    |
//|                        Heiken Ashi Color-Based Trading Robot       |
//|                                                                    |
//| Buy Entry: Red candle changes to Green                            |
//| Buy Exit: Any Red candle appears                                  |
//| Sell Entry: Green candle changes to Red                           |
//| Sell Exit: Any Green candle appears                               |
//| Features: Position Stacking, Volume Filter, Configurable Inputs   |
//+------------------------------------------------------------------+

#property copyright "Copyright 2025"
#property link      ""
#property version   "1.00"
#property strict
#property description "Heiken Ashi based EA with stacking positions"

#include <Trade\Trade.mqh>

//--- Enumeration for trading modes
enum ENUM_STACKING_MODE {
   STACK_DISABLED = 0,    // No stacking
   STACK_ENABLED = 1      // Enable position stacking
};

//+------------------------------------------------------------------+
//| INPUT PARAMETERS                                                  |
//+------------------------------------------------------------------+

// Trading Parameters
input double            LotSize              = 0.1;           // Initial lot size
input int               MaxStackingPositions = 5;              // Maximum number of stacked positions
input ENUM_STACKING_MODE EnableStacking      = STACK_ENABLED;  // Enable position stacking
input int               MagicNumber          = 123456;         // Magic number for orders
input int               MaxSlippage          = 10;             // Slippage in points

// Filter Parameters
input bool              UseSymbolMinVolume   = true;           // Use symbol minimum volume filter
input double            MinimumVolumeMultiplier = 1.0;         // Volume multiplier for filtering

// Risk Management
input double            StopLoss             = 50;             // Stop loss in points
input double            TakeProfit           = 100;            // Take profit in points

// Session Parameters
input bool              OnlyDayTrading       = false;          // Trade only during day session
input int               DayStartHour         = 9;              // Day session start hour
input int               DayEndHour           = 17;             // Day session end hour

// Display Parameters
input bool              ShowComments         = true;           // Show comments on chart

//+------------------------------------------------------------------+
//| GLOBAL VARIABLES                                                  |
//+------------------------------------------------------------------+

CTrade          Trade;                  // Trade object
int             haHandle = INVALID_HANDLE;  // Heiken Ashi indicator handle
double          haOpen[], haClose[], haHigh[], haLow[];  // HA candle arrays
int             buyOrdersCount = 0;     // Count of current buy orders
int             sellOrdersCount = 0;    // Count of current sell orders

//+------------------------------------------------------------------+
//| EXPERT INITIALIZATION FUNCTION                                    |
//+------------------------------------------------------------------+
int OnInit() {
   // Initialize trade object
   Trade.SetExpertMagicNumber(MagicNumber);
   Trade.SetDeviationInPoints(MaxSlippage);
   
   // Create Heiken Ashi indicator handle (built-in indicator)
   haHandle = iCustom(_Symbol, _Period, "Heiken Ashi");
   
   if(haHandle == INVALID_HANDLE) {
      Print("Error creating Heiken Ashi indicator handle. Error: ", GetLastError());
      return INIT_FAILED;
   }
   
   // Allocate buffers for Heiken Ashi data
   ArraySetAsSeries(haOpen, true);
   ArraySetAsSeries(haClose, true);
   ArraySetAsSeries(haHigh, true);
   ArraySetAsSeries(haLow, true);
   
   Print("Expert Advisor Initialized Successfully");
   Print("Symbol: ", _Symbol, " | Period: ", _Period, " | Stacking: ", EnableStacking);
   
   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| EXPERT DEINITIALIZATION FUNCTION                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason) {
   // Release Heiken Ashi indicator handle
   if(haHandle != INVALID_HANDLE) {
      IndicatorRelease(haHandle);
   }
   
   Print("Expert Advisor Deinitialized");
}

//+------------------------------------------------------------------+
//| EXPERT TICK FUNCTION                                              |
//+------------------------------------------------------------------+
void OnTick() {
   // Check if new bar has formed
   static datetime lastBarTime = 0;
   if(iTime(_Symbol, _Period, 0) == lastBarTime) {
      return;  // Same bar, skip processing
   }
   lastBarTime = iTime(_Symbol, _Period, 0);
   
   // Check session time if day trading only
   if(OnlyDayTrading && !IsTradeTime()) {
      return;
   }
   
   // Fetch Heiken Ashi data
   if(!FetchHeikenAshiData()) {
      return;
   }
   
   // Update position counts
   UpdatePositionCounts();
   
   // Check volume filter
   if(UseSymbolMinVolume && !CheckVolumeFilter()) {
      return;
   }
   
   // Check for trading signals
   CheckSignals();
   
   // Update chart comments
   if(ShowComments) {
      DisplayComments();
   }
}

//+------------------------------------------------------------------+
//| FETCH HEIKEN ASHI DATA                                            |
//+------------------------------------------------------------------+
bool FetchHeikenAshiData() {
   // Copy Heiken Ashi indicator data (4 buffers: Open, Close, High, Low)
   if(CopyBuffer(haHandle, 0, 0, 2, haOpen) < 0) {
      return false;
   }
   if(CopyBuffer(haHandle, 1, 0, 2, haClose) < 0) {
      return false;
   }
   if(CopyBuffer(haHandle, 2, 0, 2, haHigh) < 0) {
      return false;
   }
   if(CopyBuffer(haHandle, 3, 0, 2, haLow) < 0) {
      return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| GET HEIKEN ASHI CANDLE COLOR                                      |
//+------------------------------------------------------------------+
// Returns: 1 = Green (Bullish), -1 = Red (Bearish), 0 = Doji
int GetCandleColor(int index) {
   if(haClose[index] > haOpen[index]) {
      return 1;  // Green (Bullish)
   } else if(haClose[index] < haOpen[index]) {
      return -1;  // Red (Bearish)
   }
   return 0;  // Doji
}

//+------------------------------------------------------------------+
//| CHECK VOLUME FILTER                                               |
//+------------------------------------------------------------------+
bool CheckVolumeFilter() {
   if(!UseSymbolMinVolume) {
      return true;
   }
   
   // Get tick volume of current candle
   long tickVolume = iVolume(_Symbol, _Period, 0);
   
   // Get symbol minimum volume from market info
   long minVolumeSymbol = SymbolInfoInteger(_Symbol, SYMBOL_VOLUME_MIN);
   long minVolume = (long)(minVolumeSymbol * MinimumVolumeMultiplier);
   
   return tickVolume >= minVolume;
}

//+------------------------------------------------------------------+
//| IS TRADE TIME                                                     |
//+------------------------------------------------------------------+
bool IsTradeTime() {
   MqlDateTime timeStruct;
   TimeToStruct(TimeCurrent(), timeStruct);
   
   return (timeStruct.hour >= DayStartHour && timeStruct.hour < DayEndHour);
}

//+------------------------------------------------------------------+
//| UPDATE POSITION COUNTS                                            |
//+------------------------------------------------------------------+
void UpdatePositionCounts() {
   buyOrdersCount = 0;
   sellOrdersCount = 0;
   
   // Count open positions by type
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      if(PositionSelectByTicket(PositionGetTicket(i))) {
         if(PositionGetString(POSITION_SYMBOL) == _Symbol &&
            PositionGetInteger(POSITION_MAGIC) == MagicNumber) {
            
            if(PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY) {
               buyOrdersCount++;
            } else if(PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_SELL) {
               sellOrdersCount++;
            }
         }
      }
   }
}

//+------------------------------------------------------------------+
//| CHECK TRADING SIGNALS                                             |
//+------------------------------------------------------------------+
void CheckSignals() {
   int currentCandleColor = GetCandleColor(1);  // Current closed candle
   int previousCandleColor = GetCandleColor(2); // Previous closed candle
   
   // BUY SIGNAL: Red to Green transition
   if(previousCandleColor == -1 && currentCandleColor == 1) {
      if(EnableStacking == STACK_ENABLED) {
         if(buyOrdersCount < MaxStackingPositions) {
            OpenBuyOrder();
         }
      } else {
         if(buyOrdersCount == 0) {
            OpenBuyOrder();
         }
      }
   }
   
   // BUY EXIT: Any Red candle appears
   if(currentCandleColor == -1 && buyOrdersCount > 0) {
      CloseBuyOrders();
   }
   
   // SELL SIGNAL: Green to Red transition
   if(previousCandleColor == 1 && currentCandleColor == -1) {
      if(EnableStacking == STACK_ENABLED) {
         if(sellOrdersCount < MaxStackingPositions) {
            OpenSellOrder();
         }
      } else {
         if(sellOrdersCount == 0) {
            OpenSellOrder();
         }
      }
   }
   
   // SELL EXIT: Any Green candle appears
   if(currentCandleColor == 1 && sellOrdersCount > 0) {
      CloseSellOrders();
   }
}

//+------------------------------------------------------------------+
//| OPEN BUY ORDER                                                    |
//+------------------------------------------------------------------+
void OpenBuyOrder() {
   double bid = SymbolInfoDouble(_Symbol, SYMBOL_BID);
   double ask = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
   double point = SymbolInfoDouble(_Symbol, SYMBOL_POINT);
   
   double stopLoss = bid - (StopLoss * point);
   double takeProfit = bid + (TakeProfit * point);
   
   // Get minimum stop level
   long stopsLevel = SymbolInfoInteger(_Symbol, SYMBOL_TRADE_STOPS_LEVEL);
   double minDistance = stopsLevel * point;
   
   // Adjust SL/TP to avoid violating minimum distance
   if((bid - stopLoss) < minDistance) {
      stopLoss = bid - minDistance;
   }
   if((takeProfit - bid) < minDistance) {
      takeProfit = bid + minDistance;
   }
   
   // Normalize lot size
   double normalizedLot = NormalizeLotSize(LotSize, _Symbol);
   
   if(!Trade.Buy(normalizedLot, _Symbol, ask, stopLoss, takeProfit, "HA BUY")) {
      Print("Buy order failed. Error: ", GetLastError());
   } else {
      Print("Buy order placed. Lot: ", normalizedLot, " | Price: ", ask, " | SL: ", stopLoss, " | TP: ", takeProfit);
      buyOrdersCount++;
   }
}

//+------------------------------------------------------------------+
//| OPEN SELL ORDER                                                   |
//+------------------------------------------------------------------+
void OpenSellOrder() {
   double bid = SymbolInfoDouble(_Symbol, SYMBOL_BID);
   double ask = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
   double point = SymbolInfoDouble(_Symbol, SYMBOL_POINT);
   
   double stopLoss = ask + (StopLoss * point);
   double takeProfit = ask - (TakeProfit * point);
   
   // Get minimum stop level
   long stopsLevel = SymbolInfoInteger(_Symbol, SYMBOL_TRADE_STOPS_LEVEL);
   double minDistance = stopsLevel * point;
   
   // Adjust SL/TP to avoid violating minimum distance
   if((stopLoss - ask) < minDistance) {
      stopLoss = ask + minDistance;
   }
   if((ask - takeProfit) < minDistance) {
      takeProfit = ask - minDistance;
   }
   
   // Normalize lot size
   double normalizedLot = NormalizeLotSize(LotSize, _Symbol);
   
   if(!Trade.Sell(normalizedLot, _Symbol, bid, stopLoss, takeProfit, "HA SELL")) {
      Print("Sell order failed. Error: ", GetLastError());
   } else {
      Print("Sell order placed. Lot: ", normalizedLot, " | Price: ", bid, " | SL: ", stopLoss, " | TP: ", takeProfit);
      sellOrdersCount++;
   }
}

//+------------------------------------------------------------------+
//| CLOSE BUY ORDERS                                                  |
//+------------------------------------------------------------------+
void CloseBuyOrders() {
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      if(PositionSelectByTicket(PositionGetTicket(i))) {
         if(PositionGetString(POSITION_SYMBOL) == _Symbol &&
            PositionGetInteger(POSITION_MAGIC) == MagicNumber &&
            PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY) {
            
            if(!Trade.PositionClose(PositionGetTicket(i))) {
               Print("Failed to close buy position. Error: ", GetLastError());
            } else {
               Print("Buy position closed. Ticket: ", PositionGetTicket(i));
            }
         }
      }
   }
   buyOrdersCount = 0;
}

//+------------------------------------------------------------------+
//| CLOSE SELL ORDERS                                                 |
//+------------------------------------------------------------------+
void CloseSellOrders() {
   for(int i = PositionsTotal() - 1; i >= 0; i--) {
      if(PositionSelectByTicket(PositionGetTicket(i))) {
         if(PositionGetString(POSITION_SYMBOL) == _Symbol &&
            PositionGetInteger(POSITION_MAGIC) == MagicNumber &&
            PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_SELL) {
            
            if(!Trade.PositionClose(PositionGetTicket(i))) {
               Print("Failed to close sell position. Error: ", GetLastError());
            } else {
               Print("Sell position closed. Ticket: ", PositionGetTicket(i));
            }
         }
      }
   }
   sellOrdersCount = 0;
}

//+------------------------------------------------------------------+
//| NORMALIZE LOT SIZE                                                |
//+------------------------------------------------------------------+
double NormalizeLotSize(double lot, string symbol) {
   double minLot = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   double maxLot = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MAX);
   double stepLot = SymbolInfoDouble(symbol, SYMBOL_VOLUME_STEP);
   
   // Ensure lot is within limits
   lot = MathMax(lot, minLot);
   lot = MathMin(lot, maxLot);
   
   // Round to step
   if(stepLot > 0) {
      lot = MathRound(lot / stepLot) * stepLot;
   }
   
   return lot;
}

//+------------------------------------------------------------------+
//| DISPLAY COMMENTS                                                  |
//+------------------------------------------------------------------+
void DisplayComments() {
   int currentCandleColor = GetCandleColor(1);
   string candleColorStr = (currentCandleColor == 1) ? "GREEN" : (currentCandleColor == -1) ? "RED" : "DOJI";
   
   string comment = "";
   comment += "=== Heiken Ashi Stacking EA ===\n";
   comment += "Symbol: " + _Symbol + " | Period: " + IntegerToString(Period()) + "\n";
   comment += "Current Candle: " + candleColorStr + "\n";
   comment += "Buy Orders: " + IntegerToString(buyOrdersCount) + "/" + IntegerToString(MaxStackingPositions) + "\n";
   comment += "Sell Orders: " + IntegerToString(sellOrdersCount) + "/" + IntegerToString(MaxStackingPositions) + "\n";
   comment += "Stacking: " + (EnableStacking == STACK_ENABLED ? "ENABLED" : "DISABLED") + "\n";
   comment += "Volume Filter: " + (UseSymbolMinVolume ? "ON" : "OFF") + "\n";
   
   Comment(comment);
}

//+------------------------------------------------------------------+
