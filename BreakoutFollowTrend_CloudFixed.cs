using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.IndiaStandardTime, AccessRights = AccessRights.None, AddIndicators = true)]
    public class BreakoutFollowTrend : Robot
    {
        // ==========================================
        // Parameters
        // ==========================================

        #region Risk Management
        [Parameter("Risk % per Trade", Group = "Risk Management", DefaultValue = 1.8, MinValue = 0.1, Step = 0.1)]
        public double RiskPct { get; set; }

        [Parameter("Risk:Reward Ratio", Group = "Risk Management", DefaultValue = 0.2, MinValue = 0.1, Step = 0.1)]
        public double RR { get; set; }

        [Parameter("ATR Multiplier (SL)", Group = "Risk Management", DefaultValue = 1.2, MinValue = 0.1, Step = 0.1)]
        public double ATRMult { get; set; }

        [Parameter("Max Concurrent Trades", Group = "Risk Management", DefaultValue = 4, MinValue = 1)]
        public int MaxTrades { get; set; }

        [Parameter("Use Compounding Risk", Group = "Risk Management", DefaultValue = true)]
        public bool Compound { get; set; }

        [Parameter("Fixed Balance (when compounding off)", Group = "Risk Management", DefaultValue = 5669.0, MinValue = 0.0)]
        public double FixedBal { get; set; }

        [Parameter("Daily Loss Limit %", Group = "Risk Management", DefaultValue = 2.5, MinValue = 0.0, Step = 0.1)]
        public double DailyLoss { get; set; }
        #endregion

        #region Trailing Stop
        [Parameter("Use ATR Trailing Stop", Group = "Trailing Stop", DefaultValue = true)]
        public bool UseAtrTrailing { get; set; }
        #endregion

        #region Net PNL
        [Parameter("Use Net PNL", Group = "Net PNL", DefaultValue = false)]
        public bool UseNetPnLParam { get; set; }

        [Parameter("Net Profit Target", Group = "Net PNL", DefaultValue = 50000.0)]
        public double NetProfitTarget { get; set; }

        [Parameter("Net Loss Limit", Group = "Net PNL", DefaultValue = 100.0)]
        public double NetLossLimit { get; set; }
        #endregion

        #region Daily PNL
        [Parameter("Use Daily PNL", Group = "Daily PNL", DefaultValue = false)]
        public bool UseDailyPnLParam { get; set; }

        [Parameter("Daily Profit Target", Group = "Daily PNL", DefaultValue = 2000.0)]
        public double DailyProfitTarget { get; set; }

        [Parameter("Daily Loss Limit", Group = "Daily PNL", DefaultValue = 50.0)]
        public double DailyLossLimit { get; set; }
        #endregion

        #region Session
        [Parameter("Use Session", Group = "Session", DefaultValue = false)]
        public bool UseSession { get; set; }

        [Parameter("Session Open (HH:MM)", Group = "Session", DefaultValue = "01:00")]
        public string SessionOpen { get; set; }

        [Parameter("Session Close (HH:MM)", Group = "Session", DefaultValue = "23:00")]
        public string SessionClose { get; set; }
        #endregion

        #region Indicators
        [Parameter("Use EMA Trend Filter", Group = "Indicators", DefaultValue = true)]
        public bool UseEMA { get; set; }

        [Parameter("EMA Period", Group = "Indicators", DefaultValue = 49, MinValue = 1)]
        public int EMAPeriod { get; set; }

        [Parameter("Bollinger Bands Period", Group = "Indicators", DefaultValue = 13, MinValue = 1)]
        public int BBPeriod { get; set; }

        [Parameter("Bollinger Bands Deviation", Group = "Indicators", DefaultValue = 0.5, MinValue = 0.1, Step = 0.1)]
        public double BBDev { get; set; }

        [Parameter("ATR Period", Group = "Indicators", DefaultValue = 19, MinValue = 1)]
        public int ATRPeriod { get; set; }
        #endregion

        #region Volume Filter
        [Parameter("Use Volume Filter", Group = "Volume Filter", DefaultValue = false)]
        public bool UseVol { get; set; }

        [Parameter("Volume MA Period", Group = "Volume Filter", DefaultValue = 15, MinValue = 1)]
        public int VolPeriod { get; set; }
        #endregion

        #region Spread Filter
        [Parameter("Use Max Spread", Group = "Spread Filter", DefaultValue = false)]
        public bool UseMaxSpread { get; set; }

        [Parameter("Max Spread (Pips)", Group = "Spread Filter", DefaultValue = 2000.0)]
        public double MaxSpread { get; set; }
        #endregion

        #region Time Filters (System Filters)
        [Parameter("Weekend Close (Friday)", Group = "System Time Filters", DefaultValue = true)]
        public bool WeekendCl { get; set; }

        [Parameter("Friday Close Time (HH:MM)", Group = "System Time Filters", DefaultValue = "23:45")]
        public string FridayTime { get; set; }
        #endregion

        #region Dashboard
        [Parameter("Show Dashboard", Group = "Dashboard", DefaultValue = true)]
        public bool ShowDashboard { get; set; }
        #endregion

        // ==========================================
        // Internal Variables
        // ==========================================
        private ExponentialMovingAverage _ema;
        private BollingerBands _bb;
        private AverageTrueRange _atr;
        private SimpleMovingAverage _volSma;
        private const string BotLabel = "BFT_Strategy";
        private int _requiredBars;

        // State tracking
        private DateTime _currentDay;
        private bool _dailyLossPctHitPrinted = false;
        private bool _weekendLogPrinted = false;
        private bool _netUsdLimitHit = false;
        private bool _dailyUsdLimitHit = false;
        private DateTime _lastProcessedBarTime;

        // ==========================================
        // OnStart
        // ==========================================
        protected override void OnStart()
        {
            Print("=======================================");
            Print("BreakoutFollowTrend Bot Started");
            Print("=======================================");

            _requiredBars = EMAPeriod + 50;

            _atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.WilderSmoothing);
            _ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EMAPeriod);
            _bb = Indicators.BollingerBands(Bars.ClosePrices, BBPeriod, BBDev, MovingAverageType.Simple);
            _volSma = Indicators.SimpleMovingAverage(Bars.TickVolumes, VolPeriod);

            _currentDay = Server.Time.Date;

            if (WeekendCl && !IsValidTimeString(FridayTime))
                Print("WARNING: FridayTime '{0}' is not in HH:MM format. Weekend close will be skipped.", FridayTime);

            if (UseSession)
            {
                if (!IsValidTimeString(SessionOpen))
                    Print("WARNING: SessionOpen '{0}' is not in HH:MM format. Session filter will be ignored.", SessionOpen);
                if (!IsValidTimeString(SessionClose))
                    Print("WARNING: SessionClose '{0}' is not in HH:MM format. Session filter will be ignored.", SessionClose);
            }

            Print("Initialization complete. Compounding: {0}, Max Trades: {1}, Risk: {2}%", Compound, MaxTrades, RiskPct);

            Print("=======================================");
            Print("Symbol: {0}", SymbolName);
            Print("Min Stop Distance: {0} pips", Symbol.MinStopLossDistance);
            Print("Bid: {0} | Ask: {1} | Spread: {2} pips", Symbol.Bid, Symbol.Ask, Math.Round(Symbol.Spread / Symbol.PipSize, 2));
            Print("=======================================");
            UpdateDashboard();
        }

        // ==========================================
        // OnTick - CLOUD COMPATIBLE EXECUTION POINT
        // ==========================================
        protected override void OnTick()
        {
            // Check if we need minimum bars (Gator-style compatibility)
            if (Bars.Count < _requiredBars)
            {
                Print("[INFO] Waiting for bars: {0}/{1}", Bars.Count, _requiredBars);
                return;
            }

            // Day-change reset
            if (Server.Time.Date != _currentDay)
            {
                _currentDay = Server.Time.Date;
                _dailyUsdLimitHit = false;
                _dailyLossPctHitPrinted = false;
                _weekendLogPrinted = false;
                Print("New trading day started. Daily limits and logs have been reset.");
            }

            CheckUsdLimits();

            // Close positions immediately upon entering the weekend block
            if (WeekendCl && IsWeekendBlock())
            {
                var openPositions = Positions.FindAll(BotLabel);
                if (openPositions.Length > 0)
                {
                    Print("Weekend block reached! Closing {0} open position(s).", openPositions.Length);
                    foreach (var pos in openPositions)
                        ClosePosition(pos);
                }
            }

            // ===============================================
            // PROCESS NEW BAR - SAME LOGIC AS GATOR ROBOT
            // ===============================================
            DateTime currentBarTime = Bars.OpenTimes.LastValue;

            if (currentBarTime != _lastProcessedBarTime)
            {
                _lastProcessedBarTime = currentBarTime;
                ProcessNewBar();
            }

            UpdateDashboard();
        }

        // ==========================================
        // ProcessNewBar - GATOR-STYLE SYNCHRONOUS EXECUTION
        // ==========================================
        private void ProcessNewBar()
        {
            // Stop processing if any hard limit is hit
            if (_netUsdLimitHit || _dailyUsdLimitHit) return;

            // Use the last COMPLETED (closed) bar
            int index = Bars.Count - 2;

            double close = Bars.ClosePrices[index];
            double volume = Bars.TickVolumes[index];
            double emaVal = _ema.Result[index];
            double upperBB = _bb.Top[index];
            double lowerBB = _bb.Bottom[index];
            double atrVal = _atr.Result[index];
            double volMAVal = _volSma.Result[index];

            // Skip if indicator values are not yet ready
            if (double.IsNaN(atrVal) || double.IsNaN(emaVal) ||
                double.IsNaN(upperBB) || double.IsNaN(lowerBB))
                return;

            // ATR Trailing Stop update
            if (UseAtrTrailing)
                UpdateAtrTrailingStop(close, atrVal);

            // --- Filter Checks ---
            if (IsDailyLossPctHit())
            {
                if (!_dailyLossPctHitPrinted)
                {
                    Print("Trading paused: Daily Loss % Limit ({0}%) has been reached.", DailyLoss);
                    _dailyLossPctHitPrinted = true;
                }
                return;
            }

            if (WeekendCl && IsWeekendBlock())
            {
                if (!_weekendLogPrinted)
                {
                    Print("Trading paused: Currently inside the Weekend Block window.");
                    _weekendLogPrinted = true;
                }
                return;
            }
            else
            {
                _weekendLogPrinted = false;
            }

            if (!IsInSessionWindow()) return;

            // --- Signal Conditions ---
            bool volCond;
            if (!UseVol)
            {
                volCond = true;
            }
            else if (double.IsNaN(volMAVal) || volMAVal <= 0)
            {
                volCond = true;
            }
            else
            {
                volCond = volume > volMAVal;
            }

            double currentSpreadPips = Symbol.Spread / Symbol.PipSize;
            bool spreadCond = !UseMaxSpread || (currentSpreadPips <= MaxSpread);

            bool longCondition = (!UseEMA || close > emaVal) && close > upperBB && volCond && spreadCond;
            bool shortCondition = (!UseEMA || close < emaVal) && close < lowerBB && volCond && spreadCond;

            // Heartbeat log
            Print("Bar Closed [{0}] | Close: {1:F5} | UpperBB: {2:F5} | LowerBB: {3:F5} | EMA: {4:F5}",
                  Bars.OpenTimes[index], close, upperBB, lowerBB, emaVal);

            // --- Execution (SYNCHRONOUS - Gator Style) ---
            var openPosCount = Positions.Count(p => p.Label == BotLabel);

            if (longCondition && openPosCount < MaxTrades)
            {
                Print("Long Signal! Close={0} > UpperBB={1}. Executing...",
                      Math.Round(close, 5), Math.Round(upperBB, 5));
                ExecuteTrade(TradeType.Buy, close, atrVal);
            }
            else if (shortCondition && openPosCount < MaxTrades)
            {
                Print("Short Signal! Close={0} < LowerBB={1}. Executing...",
                      Math.Round(close, 5), Math.Round(lowerBB, 5));
                ExecuteTrade(TradeType.Sell, close, atrVal);
            }
        }

        // ==========================================
        // ExecuteTrade (SYNCHRONOUS - CLOUD COMPATIBLE)
        // ==========================================
        private void ExecuteTrade(TradeType tradeType, double signalClose, double atrVal)
        {
            double baseBal = Compound ? Account.Equity : FixedBal;
            if (baseBal <= 0)
            {
                Print("ExecuteTrade aborted: baseBal is {0}.", baseBal);
                return;
            }

            double slDist = atrVal * ATRMult;
            if (slDist <= 0)
            {
                Print("ExecuteTrade aborted: slDist is {0} (ATR={1}, Mult={2}).", slDist, atrVal, ATRMult);
                return;
            }

            double riskAmount = baseBal * (RiskPct / 100.0);
            double valuePerUnit = Symbol.TickValue / Symbol.TickSize;
            if (valuePerUnit <= 0)
            {
                Print("ExecuteTrade aborted: valuePerUnit is {0}.", valuePerUnit);
                return;
            }

            double exactVolume = riskAmount / (slDist * valuePerUnit);
            double normalizedVolume = Symbol.NormalizeVolumeInUnits(exactVolume, RoundingMode.Down);

            if (normalizedVolume < Symbol.VolumeInUnitsMin)
            {
                Print("ExecuteTrade aborted: normalizedVolume {0} < min {1}.", normalizedVolume, Symbol.VolumeInUnitsMin);
                return;
            }

            double slPips = slDist / Symbol.PipSize;

            // Validate minimum distance
            double brokerMinDistancePips = Symbol.MinStopLossDistance;
            if (slPips < brokerMinDistancePips)
            {
                slPips = brokerMinDistancePips + 1.0;
                Print("[SL SAFETY] Adjusted SL to broker minimum: {0} pips", slPips);
            }

            double tpPips = (slDist * RR) / Symbol.PipSize;

            Print("=======================================");
            Print("[EXECUTION]");
            Print("Type: {0}", tradeType);
            Print("Volume: {0}", normalizedVolume);
            Print("SL Pips: {0}", Math.Round(slPips, 1));
            Print("TP Pips: {0}", Math.Round(tpPips, 1));
            Print("Bid: {0}", Symbol.Bid);
            Print("Ask: {0}", Symbol.Ask);
            Print("=======================================");

            // SYNCHRONOUS EXECUTION (Gator-style)
            var result = ExecuteMarketOrder(tradeType, SymbolName, normalizedVolume, BotLabel, slPips, tpPips);

            if (!result.IsSuccessful)
            {
                Print("[EXECUTION FAILED]");
                Print("Error: {0}", result.Error);
            }
            else
            {
                Print("[SUCCESS] Position Opened");
                Print("Position ID: {0}", result.Position.Id);
            }
        }

        // ==========================================
        // UpdateAtrTrailingStop
        // ==========================================
        private void UpdateAtrTrailingStop(double closePrice, double atrVal)
        {
            var openPositions = Positions.FindAll(BotLabel);
            double slDistance = atrVal * ATRMult;

            foreach (var pos in openPositions)
            {
                double minDistance = Symbol.MinStopLossDistance * Symbol.PipSize;

                if (pos.TradeType == TradeType.Buy)
                {
                    double newSl = closePrice - slDistance;
                    if (!pos.StopLoss.HasValue || newSl > pos.StopLoss.Value)
                    {
                        double maxAllowedSL = Symbol.Bid - minDistance;
                        if (newSl >= maxAllowedSL)
                        {
                            ModifyPosition(pos, newSl, pos.TakeProfit);
                            Print("ATR Trailing: LONG SL moved to {0}", Math.Round(newSl, 5));
                        }
                    }
                }
                else if (pos.TradeType == TradeType.Sell)
                {
                    double newSl = closePrice + slDistance;
                    if (!pos.StopLoss.HasValue || newSl < pos.StopLoss.Value)
                    {
                        double minAllowedSL = Symbol.Ask + minDistance;
                        if (newSl <= minAllowedSL)
                        {
                            ModifyPosition(pos, newSl, pos.TakeProfit);
                            Print("ATR Trailing: SHORT SL moved to {0}", Math.Round(newSl, 5));
                        }
                    }
                }
            }
        }

        // ==========================================
        // CheckUsdLimits
        // ==========================================
        private void CheckUsdLimits()
        {
            if (_netUsdLimitHit) return;
            if (!UseNetPnLParam && !UseDailyPnLParam) return;

            double netPnL = 0;
            double dailyPnL = 0;
            double floatPnL = 0;
            DateTime today = Server.Time.Date;

            foreach (var hist in History.Where(x => x.Label == BotLabel))
            {
                netPnL += hist.NetProfit;
                if (hist.ClosingTime.Date == today)
                    dailyPnL += hist.NetProfit;
            }

            foreach (var pos in Positions.FindAll(BotLabel))
                floatPnL += pos.NetProfit;

            double totalNet = netPnL + floatPnL;
            double totalDaily = dailyPnL + floatPnL;

            if (UseNetPnLParam && !_netUsdLimitHit)
            {
                if (totalNet >= NetProfitTarget)
                {
                    Print("NET Profit Target ({0} USD) reached! Total={1:F2}. Closing all.", NetProfitTarget, totalNet);
                    CloseAllPositions();
                    _netUsdLimitHit = true;
                    return;
                }
                if (totalNet <= -Math.Abs(NetLossLimit))
                {
                    Print("NET Loss Limit (-{0} USD) reached! Total={1:F2}. Closing all.", NetLossLimit, totalNet);
                    CloseAllPositions();
                    _netUsdLimitHit = true;
                    return;
                }
            }

            if (UseDailyPnLParam && !_dailyUsdLimitHit && !_netUsdLimitHit)
            {
                if (totalDaily >= DailyProfitTarget)
                {
                    Print("DAILY Profit Target ({0} USD) reached! Daily={1:F2}. Closing all.", DailyProfitTarget, totalDaily);
                    CloseAllPositions();
                    _dailyUsdLimitHit = true;
                }
                else if (totalDaily <= -Math.Abs(DailyLossLimit))
                {
                    Print("DAILY Loss Limit (-{0} USD) reached! Daily={1:F2}. Closing all.", DailyLossLimit, totalDaily);
                    CloseAllPositions();
                    _dailyUsdLimitHit = true;
                }
            }
        }

        // ==========================================
        // IsInSessionWindow
        // ==========================================
        private bool IsInSessionWindow()
        {
            if (!UseSession) return true;

            if (!TryParseTime(SessionOpen, out int openTotalMinutes)) return true;
            if (!TryParseTime(SessionClose, out int closeTotalMinutes)) return true;

            int nowTotalMinutes = Server.Time.Hour * 60 + Server.Time.Minute;

            if (openTotalMinutes < closeTotalMinutes)
                return nowTotalMinutes >= openTotalMinutes && nowTotalMinutes < closeTotalMinutes;
            else
                return nowTotalMinutes >= openTotalMinutes || nowTotalMinutes < closeTotalMinutes;
        }

        // ==========================================
        // IsWeekendBlock
        // ==========================================
        private bool IsWeekendBlock()
        {
            if (!TryParseTime(FridayTime, out int fridayTotalMinutes)) return false;

            DateTime now = Server.Time;
            int nowTotalMin = now.Hour * 60 + now.Minute;

            int openHour = 0;
            if (UseSession && TryParseTime(SessionOpen, out int sessionOpenMin))
                openHour = sessionOpenMin / 60;

            bool isFridayPast = now.DayOfWeek == DayOfWeek.Friday && nowTotalMin >= fridayTotalMinutes;
            bool isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
            bool isMondayEarly = now.DayOfWeek == DayOfWeek.Monday && now.Hour < openHour;

            return isFridayPast || isWeekend || isMondayEarly;
        }

        // ==========================================
        // IsDailyLossPctHit
        // ==========================================
        private bool IsDailyLossPctHit()
        {
            if (DailyLoss <= 0) return false;

            double baseBal = Compound ? Account.Equity : FixedBal;
            double dailyLossMax = baseBal * (DailyLoss / 100.0);
            DateTime today = Server.Time.Date;
            double dailyPnL = 0;

            foreach (var hist in History.Where(x => x.Label == BotLabel && x.ClosingTime.Date == today))
                dailyPnL += hist.NetProfit;

            return dailyPnL <= -dailyLossMax;
        }

        // ==========================================
        // CloseAllPositions
        // ==========================================
        private void CloseAllPositions()
        {
            foreach (var pos in Positions.FindAll(BotLabel))
                ClosePosition(pos);
        }

        // ==========================================
        // UpdateDashboard
        // ==========================================
        private void UpdateDashboard()
        {
            if (Chart == null) return;

            if (!ShowDashboard)
            {
                Chart.RemoveObject("BFT_Dashboard");
                return;
            }

            double netPnL = 0;
            double dailyPnL = 0;
            double floatPnL = 0;
            DateTime today = Server.Time.Date;

            foreach (var hist in History.Where(x => x.Label == BotLabel))
            {
                netPnL += hist.NetProfit;
                if (hist.ClosingTime.Date == today)
                    dailyPnL += hist.NetProfit;
            }

            foreach (var pos in Positions.FindAll(BotLabel))
                floatPnL += pos.NetProfit;

            string sign(double v) => v >= 0 ? "+" : "";

            string txt = "BFT STRATEGY DASHBOARD\n";
            txt += "-----------------------------------\n";
            txt += string.Format("Net PnL   (Closed) : {0}{1:F2}\n", sign(netPnL), Math.Round(netPnL, 2));
            txt += string.Format("Daily PnL (Closed) : {0}{1:F2}\n", sign(dailyPnL), Math.Round(dailyPnL, 2));
            txt += string.Format("Float PnL (Open)   : {0}{1:F2}\n", sign(floatPnL), Math.Round(floatPnL, 2));
            txt += string.Format("Open Positions     : {0}\n", Positions.FindAll(BotLabel).Length);
            txt += string.Format("Net Limit Hit      : {0}\n", _netUsdLimitHit ? "YES" : "No");
            txt += string.Format("Daily Limit Hit    : {0}\n", _dailyUsdLimitHit ? "YES" : "No");

            if (UseNetPnLParam || UseDailyPnLParam)
            {
                txt += "\nTARGETS & LIMITS\n";
                txt += "-----------------------------------\n";
                if (UseNetPnLParam)
                {
                    txt += string.Format("Net Target  : +{0} USD\n", NetProfitTarget);
                    txt += string.Format("Net Limit   : -{0} USD {1}\n", NetLossLimit, _netUsdLimitHit ? "(HIT!)" : "");
                }
                if (UseDailyPnLParam)
                {
                    txt += string.Format("Daily Target: +{0} USD\n", DailyProfitTarget);
                    txt += string.Format("Daily Limit : -{0} USD {1}\n", DailyLossLimit, _dailyUsdLimitHit ? "(HIT!)" : "");
                }
            }

            Chart.DrawStaticText("BFT_Dashboard", txt, VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightGray);
        }

        // ==========================================
        // Helpers
        // ==========================================

        private bool TryParseTime(string timeStr, out int totalMinutes)
        {
            totalMinutes = 0;

            if (string.IsNullOrEmpty(timeStr)) return false;

            if (timeStr.Contains(":"))
            {
                string[] parts = timeStr.Split(':');
                if (parts.Length != 2) return false;
                if (!int.TryParse(parts[0], out int h)) return false;
                if (!int.TryParse(parts[1], out int m)) return false;
                if (h < 0 || h > 23 || m < 0 || m > 59) return false;
                totalMinutes = h * 60 + m;
                return true;
            }
            else if (timeStr.Length == 4)
            {
                if (!int.TryParse(timeStr.Substring(0, 2), out int h)) return false;
                if (!int.TryParse(timeStr.Substring(2, 2), out int m)) return false;
                if (h < 0 || h > 23 || m < 0 || m > 59) return false;
                totalMinutes = h * 60 + m;
                return true;
            }

            return false;
        }

        private bool IsValidTimeString(string timeStr)
        {
            return TryParseTime(timeStr, out _);
        }
    }
}
