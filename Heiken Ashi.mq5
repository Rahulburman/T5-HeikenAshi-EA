//+------------------------------------------------------------------+
//|                                        Heiken Ashi.mq5            |
//|                      Custom Heiken Ashi Candle Indicator           |
//|                                                                    |
//| Calculates and displays Heiken Ashi candles on chart               |
//| Formula:                                                           |
//| HA Close = (O+H+L+C)/4                                             |
//| HA Open = (Prev HA Open + Prev HA Close)/2                        |
//| HA High = MAX(H, HA Open, HA Close)                               |
//| HA Low = MIN(L, HA Open, HA Close)                                |
//+------------------------------------------------------------------+

#property copyright "Copyright 2025"
#property link      ""
#property version   "1.00"
#property description "Custom Heiken Ashi Candle Indicator"
#property indicator_chart_window
#property indicator_buffers 4
#property indicator_plots   1
#property indicator_type1   DRAW_CANDLES
#property indicator_color1  clrGreen, clrRed
#property indicator_width1  2

// Indicator buffers
double         haOpen[];
double         haClose[];
double         haHigh[];
double         haLow[];

//+------------------------------------------------------------------+
//| INDICATOR INITIALIZATION FUNCTION                                 |
//+------------------------------------------------------------------+
int OnInit() {
   // Set buffer descriptions
   PlotIndexSetString(0, PLOT_LABEL, "Heiken Ashi");
   
   // Allocate buffers
   SetIndexBuffer(0, haOpen, INDICATOR_DATA);
   SetIndexBuffer(1, haClose, INDICATOR_DATA);
   SetIndexBuffer(2, haHigh, INDICATOR_DATA);
   SetIndexBuffer(3, haLow, INDICATOR_DATA);
   
   // Set buffer arrays as series
   ArraySetAsSeries(haOpen, true);
   ArraySetAsSeries(haClose, true);
   ArraySetAsSeries(haHigh, true);
   ArraySetAsSeries(haLow, true);
   
   Print("Heiken Ashi Indicator Initialized Successfully");
   
   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| INDICATOR DEINITIALIZATION FUNCTION                               |
//+------------------------------------------------------------------+
void OnDeinit(const int reason) {
   Print("Heiken Ashi Indicator Deinitialized");
}

//+------------------------------------------------------------------+
//| INDICATOR CALCULATION FUNCTION                                    |
//+------------------------------------------------------------------+
int OnCalculate(const int rates_total,
                const int prev_calculated,
                const datetime &time[],
                const double &open[],
                const double &high[],
                const double &low[],
                const double &close[],
                const long &tick_volume[],
                const long &volume[],
                const int &spread[]) {
   
   int start = 0;
   
   // If first calculation, start from bar 0
   if(prev_calculated == 0) {
      start = 0;
      // Initialize first bar HA Open = (O+C)/2
      haOpen[rates_total - 1] = (open[rates_total - 1] + close[rates_total - 1]) / 2.0;
   } else {
      start = prev_calculated - 1;
   }
   
   // Calculate Heiken Ashi for each bar
   for(int i = start; i < rates_total; i++) {
      // HA Close = (O + H + L + C) / 4
      haClose[i] = (open[i] + high[i] + low[i] + close[i]) / 4.0;
      
      // HA Open = (Prev HA Open + Prev HA Close) / 2
      if(i < rates_total - 1) {
         haOpen[i] = (haOpen[i + 1] + haClose[i + 1]) / 2.0;
      } else {
         // First bar - use current bar O and C average
         haOpen[i] = (open[i] + close[i]) / 2.0;
      }
      
      // HA High = MAX(H, HA Open, HA Close)
      haHigh[i] = MathMax(high[i], MathMax(haOpen[i], haClose[i]));
      
      // HA Low = MIN(L, HA Open, HA Close)
      haLow[i] = MathMin(low[i], MathMin(haOpen[i], haClose[i]));
   }
   
   return rates_total;
}

//+------------------------------------------------------------------+
