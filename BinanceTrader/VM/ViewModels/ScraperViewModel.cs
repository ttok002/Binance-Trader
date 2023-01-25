/*
*MIT License
*
*Copyright (c) 2022 S Christison
*
*Permission is hereby granted, free of charge, to any person obtaining a copy
*of this software and associated documentation files (the "Software"), to deal
*in the Software without restriction, including without limitation the rights
*to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
*copies of the Software, and to permit persons to whom the Software is
*furnished to do so, subject to the following conditions:
*
*The above copyright notice and this permission notice shall be included in all
*copies or substantial portions of the Software.
*
*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
*SOFTWARE.
*/

using BinanceAPI;
using BinanceAPI.Enums;
using BinanceAPI.Objects;
using BinanceAPI.Objects.Spot.SpotData;
using BTNET.BV.Abstract;
using BTNET.BV.Base;
using BTNET.BV.Enum;
using BTNET.BVVM;
using BTNET.BVVM.BT;
using BTNET.BVVM.BT.Args;
using BTNET.BVVM.BT.Orders;
using BTNET.BVVM.Helpers;
using BTNET.BVVM.Log;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BTNET.VM.ViewModels
{
    public partial class ScraperViewModel : Core
    {
        public volatile EventHandler<string> ScraperStopped;
        public volatile EventHandler<OrderPair> SellOrderEvent;
        public volatile EventHandler<OrderPair> BuyOrderEvent;
        public volatile EventHandler<OrderViewModel> FailedFoKOrderTask;

        protected private volatile SemaphoreSlim SlimSell = new SemaphoreSlim(ONE, ONE);
        protected private volatile SemaphoreSlim SlimBuy = new SemaphoreSlim(ONE, ONE);
        protected private volatile SemaphoreSlim GuardSlimWait = new SemaphoreSlim(ONE, ONE);
        protected private volatile SemaphoreSlim GuardSlimWaitGuess = new SemaphoreSlim(ONE, ONE);
        protected private volatile SemaphoreSlim GuardSlimWatchGuess = new SemaphoreSlim(ONE, ONE);

        protected private volatile OrderViewModel? WaitingBuy;
        protected private volatile OrderViewModel? WaitingSell;

        protected private volatile CompareRandom CompareRandom = new CompareRandom();

        private SolidColorBrush statusColor = Static.White;
        private SolidColorBrush runningTotalColor = Static.White;

        private Combo selectedItem = new();
        private ComboBoxItem biasDirection = new();

        private int up = ZERO;
        private int down = ZERO;
        private int countDisplay;
        private int waitTimeCount = 7;

        private string status = WAITING_TWO;
        private string logText = EMPTY;

        private decimal stepSize = ONE;
        private decimal percent = 0.035m;
        private decimal reverseDownPercent = 0.042m;
        private decimal waitTime = 200;
        private decimal quantity;
        private decimal priceBias = 300;

        private decimal nextPriceUp;
        private decimal nextPriceDown;
        private decimal downDecimal;
        private decimal percentDecimal;
        private decimal waitPrice;
        private decimal buyPrice;
        private decimal runningTotal = ZERO;
        private decimal win;
        private decimal lose;

        private bool started = false;
        private bool isStartEnabled = true;
        private bool isStopEnabled = false;
        private bool isChangeBiasEnabled = true;
        private bool isSettingsEnabled = true;
        private bool isAddEnabled = false;
        private bool isCloseCurrentEnabled = false;
        private bool isSwitchEnabled = false;
        private bool switchAutoIsChecked = false;
        private bool clearStatsIsChecked = false;
        private bool useLimitAdd = true;
        private bool useLimitClose = true;
        private bool useLimitSell = true;
        private bool useLimitBuy = true;
        private bool useDontGuessBuy = false;
        private bool useDontGuessSell = false;
        private decimal guesserReverseBias = -500;
        private bool buyReverseIsChecked;

        public string Symbol => Static.SelectedSymbolViewModel.SymbolView.Symbol;

        public TradingMode Mode => Static.CurrentTradingMode;

        public ICommand UseLimitBuyCommand { get; set; }

        public ICommand UseLimitSellCommand { get; set; }

        public ICommand UseLimitCloseCommand { get; set; }

        public ICommand UseLimitAddCommand { get; set; }

        public ICommand UseSwitchAutoCommand { get; set; }

        public ICommand SwitchCommand { get; set; }

        public ICommand StopCommand { get; set; }

        public ICommand StartCommand { get; set; }

        public ICommand AddNewCommand { get; set; }

        public ICommand IncreaseBiasCommand { get; set; }

        public ICommand DecreaseBiasCommand { get; set; }

        public ICommand IncreaseStepCommand { get; set; }

        public ICommand DecreaseStepCommand { get; set; }

        public ICommand IncreasePercentCommand { get; set; }

        public ICommand DecreasePercentCommand { get; set; }

        public ICommand IncreaseWaitTimeCommand { get; set; }

        public ICommand DecreaseWaitTimeCommand { get; set; }

        public ICommand IncreaseWaitCountCommand { get; set; }

        public ICommand DecreaseWaitCountCommand { get; set; }

        public ICommand IncreaseDownReverseCommand { get; set; }

        public ICommand DecreaseDownReverseCommand { get; set; }

        public ICommand CloseCurrentCommand { get; set; }

        public ICommand TriggerClearStatsCommand { get; set; }

        public ICommand DontGuessBuyCommand { get; set; }

        public ICommand DontGuessSellCommand { get; set; }

        public ICommand BuyReverseCommand { get; set; }

        protected private Ticker? SymbolTicker { get; set; } = null;

        public Bias DirectionBias { get; set; } = Bias.None;

        private int Loops { get; set; } = ZERO;

        public decimal GuesserLastPriceTicker { get; set; } = ZERO;

        protected bool WatchingBlocked { get; set; } = true;

        private bool WatchingGuesserBlocked { get; set; } = true;

        private bool WaitingGuesserBlocked { get; set; } = true;

        private bool SideTaskStarted { get; set; } = false;

        private Conductor Conductor { get; set; }

        public ScraperCounter ScraperCounter { get; set; } = new();

        public ScraperViewModel()
        {
            StopCommand = new DelegateCommand(Stop);
            StartCommand = new DelegateCommand(Start);
            AddNewCommand = new DelegateCommand(UserAdd);
            CloseCurrentCommand = new DelegateCommand(CloseCurrent);
            SwitchCommand = new DelegateCommand(Switch);

            IncreaseBiasCommand = new DelegateCommand(IncreaseBias);
            DecreaseBiasCommand = new DelegateCommand(DecreaseBias);
            IncreaseStepCommand = new DelegateCommand(IncreaseStep);
            DecreaseStepCommand = new DelegateCommand(DecreaseStep);
            IncreasePercentCommand = new DelegateCommand(IncreaseScrapePercent);
            DecreasePercentCommand = new DelegateCommand(DecreaseScrapePercent);
            IncreaseWaitTimeCommand = new DelegateCommand(IncreaseWaitTime);
            DecreaseWaitTimeCommand = new DelegateCommand(DecreaseWaitTime);

            IncreaseWaitCountCommand = new DelegateCommand(IncreaseWaitCount);
            DecreaseWaitCountCommand = new DelegateCommand(DecreaseWaitCount);
            IncreaseDownReverseCommand = new DelegateCommand(IncreaseDownReverse);
            DecreaseDownReverseCommand = new DelegateCommand(DecreaseDownReverse);

            TriggerClearStatsCommand = new DelegateCommand(ClearStats);

            UseSwitchAutoCommand = new DelegateCommand(ToggleSwitchAuto);
            UseLimitBuyCommand = new DelegateCommand(ToggleUseLimitBuy);
            UseLimitSellCommand = new DelegateCommand(ToggleUseLimitSell);
            UseLimitCloseCommand = new DelegateCommand(ToggleUseLimitClose);
            UseLimitAddCommand = new DelegateCommand(ToggleUseLimitAdd);
            DontGuessBuyCommand = new DelegateCommand(ToggleDontGuessBuy);
            DontGuessSellCommand = new DelegateCommand(ToggleDontGuessSell);
            BuyReverseCommand = new DelegateCommand(ToggleReverseBuy);

            ScraperStopped += Stop;
            FailedFoKOrderTask += FailedFokOrderEvent;
            SellOrderEvent += SellOrderTask;
            BuyOrderEvent += BuyOrderTask;

            Conductor = new Conductor();
            SetupTimers();
        }

        public bool BuyReverseIsChecked
        {
            get => buyReverseIsChecked;
            set
            {
                buyReverseIsChecked = value;
                PropChanged();
            }
        }

        public void ToggleReverseBuy(object o)
        {
            BuyReverseIsChecked = !BuyReverseIsChecked;
        }

        public SolidColorBrush RunningTotalColor
        {
            get => runningTotalColor;
            set
            {
                runningTotalColor = value;
                PropChanged();
            }
        }

        public SolidColorBrush StatusColor
        {
            get => statusColor;
            set
            {
                statusColor = value;
                PropChanged();
            }
        }

        public Combo SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                PropChanged();
            }
        }

        public ComboBoxItem BiasDirectionComboBox
        {
            get => biasDirection;
            set
            {
                biasDirection = value;
                DirectionBias = GetBias(value);

                PropChanged();
            }
        }

        public bool DontGuessBuyIsChecked
        {
            get => useDontGuessBuy;
            set
            {
                useDontGuessBuy = value;
                PropChanged();
            }
        }

        public bool DontGuessSellIsChecked
        {
            get => useDontGuessSell;
            set
            {
                useDontGuessSell = value;
                PropChanged();
            }
        }

        public decimal RunningTotal
        {
            get => runningTotal;
            set
            {
                runningTotal = value;
                PropChanged();
            }
        }

        public decimal WinCount
        {
            get => win;
            set
            {
                win = value;
                PropChanged();
            }
        }

        public decimal LoseCount
        {
            get => lose;
            set
            {
                lose = value;
                PropChanged();
            }
        }

        public bool ClearStatsIsChecked
        {
            get => clearStatsIsChecked;
            set
            {
                clearStatsIsChecked = value;
                PropChanged();
            }
        }

        public bool SwitchAutoIsChecked
        {
            get => switchAutoIsChecked;
            set
            {
                switchAutoIsChecked = value;
                PropChanged();
            }
        }

        public string Status
        {
            get => status;
            set
            {
                status = value;
                PropChanged();
            }
        }

        public bool Started
        {
            get => started;
            set
            {
                started = value;
                PropChanged();
            }
        }

        public decimal BuyPrice
        {
            get => buyPrice;
            set
            {
                buyPrice = value;
                PropChanged();
            }
        }

        public decimal WaitPrice
        {
            get => waitPrice;
            set
            {
                waitPrice = value;
                PropChanged();
            }
        }

        public decimal Quantity
        {
            get => quantity;
            set
            {
                quantity = value;
                PropChanged();
            }
        }

        public decimal DownDecimal
        {
            get => downDecimal;
            set
            {
                downDecimal = value;
                PropChanged();
            }
        }

        public decimal PercentDecimal
        {
            get => percentDecimal;
            set
            {
                percentDecimal = value;
                PropChanged();
            }
        }

        public decimal SellPercent
        {
            get => percent;
            set
            {
                percent = value;
                PropChanged();
            }
        }

        public decimal PriceBias
        {
            get => priceBias;
            set
            {
                priceBias = value;
                PropChanged();
                UpdatePriceBias(ScraperVM.BuyPrice == ZERO ? ScraperVM.WaitPrice : ScraperVM.BuyPrice, out _);
            }
        }

        public decimal NextPriceUp
        {
            get => nextPriceUp;
            set
            {
                nextPriceUp = value;
                PropChanged();
            }
        }

        public decimal NextPriceDown
        {
            get => nextPriceDown;
            set
            {
                nextPriceDown = value;
                PropChanged();
            }
        }

        public bool IsStartEnabled
        {
            get => isStartEnabled;
            set
            {
                isStartEnabled = value;
                PropChanged();
            }
        }

        public bool IsStopEnabled
        {
            get => isStopEnabled;
            set
            {
                isStopEnabled = value;
                PropChanged();
            }
        }

        public bool IsSettingsEnabled
        {
            get => isSettingsEnabled;
            set
            {
                isSettingsEnabled = value;
                PropChanged();
            }
        }

        public bool IsChangeBiasEnabled
        {
            get => isChangeBiasEnabled;
            set
            {
                isChangeBiasEnabled = value;
                PropChanged();
            }
        }

        public bool IsAddEnabled
        {
            get => isAddEnabled;
            set
            {
                isAddEnabled = value;
                PropChanged();
            }
        }

        public bool IsCloseCurrentEnabled
        {
            get => isCloseCurrentEnabled;
            set
            {
                isCloseCurrentEnabled = value;
                PropChanged();
            }
        }

        public bool IsSwitchEnabled
        {
            get => isSwitchEnabled;
            set
            {
                isSwitchEnabled = value;
                PropChanged();
            }
        }

        public decimal StepSize
        {
            get => stepSize;
            set
            {
                stepSize = value;
                PropChanged();
            }
        }

        public int Down
        {
            get => down;
            set
            {
                down = value;
                PropChanged();
            }
        }

        public int Up
        {
            get => up;
            set
            {
                up = value;
                PropChanged();
            }
        }

        public decimal WaitTime
        {
            get => waitTime;
            set
            {
                waitTime = value;
                PropChanged();
            }
        }

        public int WaitTimeCount
        {
            get => waitTimeCount;
            set
            {
                waitTimeCount = value;
                PropChanged();
            }
        }

        public int CountDisplay
        {
            get => countDisplay;
            set
            {
                countDisplay = value;
                PropChanged();
            }
        }

        public decimal ReverseDownPercent
        {
            get => reverseDownPercent;
            set
            {
                reverseDownPercent = value;
                PropChanged();
            }
        }

        public string LogText
        {
            get => logText;
            set
            {
                logText = logText + value + NEW_LINE;
                PropChanged();
            }
        }

        public bool UseLimitAdd
        {
            get => useLimitAdd;
            set
            {
                useLimitAdd = value;
                PropChanged();
            }
        }

        public bool UseLimitClose
        {
            get => useLimitClose;
            set
            {
                useLimitClose = value;
                PropChanged();
            }
        }

        public bool UseLimitSell
        {
            get => useLimitSell;
            set
            {
                useLimitSell = value;
                PropChanged();
            }
        }

        public bool UseLimitBuy
        {
            get => useLimitBuy;
            set
            {
                useLimitBuy = value;
                PropChanged();
            }
        }

        public decimal GuesserReverseBias
        {
            get => guesserReverseBias;
            set
            {
                guesserReverseBias = value;
                PropChanged();
            }
        }

        protected private void SetupTimers()
        {
            Conductor.WaitingTimer.SetInterval(() =>
            {
                if (GuardSlimWait.Wait(ZERO))
                {
                    if (WaitingSell != null && !WaitingSell.IsOrderCompleted)
                    {
                        if (Break())
                        {
                            ScraperStopped.Invoke(false, EMPTY);
                            GuardSlimWait.Release();
                            return;
                        }

                        if (CalculateReverse(WaitingSell, BuyReverseIsChecked))
                        {
                            Conductor.InteruptWaiting();

                            if (!DontGuessBuyIsChecked)
                            {
                                GuesserWaitingModeEvent(WaitingSell); // -> Waiting Guesser
                            }
                            else
                            {
                                DontGuessWaiting(WaitingSell);
                            }

                            GuardSlimWait.Release();
                            return;
                        }

                        decimal wait = ScraperVM.WaitPrice;
                        if (wait != ZERO)
                        {
                            UpdatePriceBias(wait, out decimal pb);
                            var price = decimal.Round(MarketVM.AverageOneSecond, (int)QuoteVM.PriceTickSizeScale);
                            bool up = NextBias(price, NextPriceDown, NextPriceUp, pb, DirectionBias, out bool nextBias);
                            if (nextBias)
                            {
                                if (!up)
                                {
                                    Conductor.InteruptWaiting();
                                    if (!BuySwitchPrice(WaitingSell, NEXT_D + PRICE_BIAS + price + SLASH + pb, false, NextPriceDown)) // -> Success Watching Mode // <- Fail Waiting Mode
                                    {
                                        ResetWaitingLoop();
                                    }

                                    GuardSlimWait.Release();
                                    return;
                                }
                                else
                                {
                                    Conductor.InteruptWaiting();
                                    if (!BuySwitchPrice(WaitingSell, NEXT_U + PRICE_BIAS + price + SLASH + pb, false, NextPriceUp)) // -> Success Watching Mode // <- Fail Waiting Mode 
                                    {
                                        ResetWaitingLoop();
                                    }

                                    GuardSlimWait.Release();
                                    return;
                                }
                            }
                        }

                        if (RealTimeVM.AskPrice > WaitingSell.Price)
                        {
                            InvokeUI.CheckAccess(() =>
                            {
                                Up++;
                            });
                        }
                        else
                        {
                            InvokeUI.CheckAccess(() =>
                            {
                                Down++;
                            });
                        }

                        if (Down >= (WaitTime * FIVE_HUNDRED))
                        {
                            Conductor.InteruptWaiting();
                            if (!BuySwitchAskPrice(WaitingSell, TIME_ELAPSED, false)) // -> Success Watching Mode // <- Fail Waiting Mode
                            {
                                ResetWaitingLoop();
                            }

                            GuardSlimWait.Release();
                            return;
                        }

                        if (TimePrice())
                        {
                            Conductor.InteruptWaiting();
                            if (!BuySwitchAskPrice(WaitingSell, WAIT_COUNT_ELAPSED, false)) // -> Success Watching Mode // <- Fail Waiting Mode
                            {
                                ResetWaitingLoop();
                            }

                            GuardSlimWait.Release();
                            return;
                        }

                        Loops++;
                    }
                    else
                    {
                        ScraperStopped.Invoke(true, STOPPED_DIED); // This shouldn't happen
                    }

                    GuardSlimWait.Release();
                }
            }, ONE, false, resolution: ONE);

            Conductor.WaitingGuesserTimer.SetInterval(() =>
            {
                if (GuardSlimWaitGuess.Wait(ZERO))
                {
                    if (!WaitingGuesserBlocked && WaitingSell != null && !WaitingSell.IsOrderCompleted)
                    {
                        var counter = ScraperVM.ScraperCounter;
                        var elapsed = counter.GuesserStopwatch.ElapsedMilliseconds;
                        if (elapsed > GUESSER_START_MIN_MS)
                        {
                            if (!CalculateReverse(WaitingSell))
                            {
                                WaitingGuesserBlocked = true;
                                WaitingLoopStartEvent(WaitingSell); // -> Fail Waiting Mode
                                GuardSlimWaitGuess.Release();
                                return;
                            }

                            if (counter.GuesserDiv <= GuesserReverseBias)
                            {
                                SettleWaitingGuesser(WaitingSell);
                                GuardSlimWaitGuess.Release();
                                return;
                            }

                            if (CompareRandom.Compare(WaitingSell.Pnl))
                            {
                                //WriteLog.Test("Price:" + RealTimeVM.AskPrice + " GuesserDiv:" + counter.GuesserDiv + " ComparePnl:" + CompareRandom.a + " CompareB:" + CompareRandom.b + " CompareC:" + CompareRandom.c + " CompareD:" + CompareRandom.d);
                                ScraperCounter.ResetCounter();
                                CompareRandom.Clear();
                            }
                        }

                        if (MarketVM.Insights.Ready || MarketVM.Insights.Ready15Minutes)
                        {
                            bool? chl = CountLowHigh(counter);
                            if (chl != null)
                            {
                                if (chl.Value)
                                {
                                    SettleWaitingGuesser(WaitingSell);
                                    AddMessage(NEW_LOW + counter.GuessNewLowCount + BAR + counter.GuessNewLowCountTwo);
                                    GuardSlimWaitGuess.Release();
                                    return;
                                }
                                else
                                {
                                    SettleWaitingGuesser(WaitingSell);
                                    AddMessage(NEW_LOW + counter.GuessNewLowCount + BAR + counter.GuessNewLowCountTwo);
                                    GuardSlimWaitGuess.Release();
                                    return;
                                }
                            }
                        }
                    }

                    GuardSlimWaitGuess.Release();
                }
            }, ONE, false, resolution: ONE);

            Conductor.WatchingGuesserTimer.SetInterval(() =>
            {
                if (GuardSlimWatchGuess.Wait(ZERO))
                {
                    if (!WatchingGuesserBlocked && WaitingBuy != null && !WaitingBuy.IsOrderCompleted)
                    {
                        var counter = ScraperVM.ScraperCounter;
                        var elapsed = counter.GuesserStopwatch.ElapsedMilliseconds;
                        if (elapsed > GUESSER_START_MIN_MS)
                        {
                            if (UpdateCurrentPnlPercent(WaitingBuy) < SellPercent)
                            {
                                WatchingGuesserBlocked = true;
                                WatchingLoopStartEvent(true, WaitingBuy); // <- Fail Watching Mode        
                                GuardSlimWatchGuess.Release();
                                return;
                            }

                            if (counter.GuesserDiv <= GuesserReverseBias)
                            {
                                SettleWatchingGuesser(WaitingBuy); // -> Success Waiting Mode // <- Fail Watching Mode
                                GuardSlimWatchGuess.Release();
                                return;
                            }

                            if (CompareRandom.Compare(WaitingBuy.Pnl))
                            {
                                //WriteLog.Test("Price:" + RealTimeVM.BidPrice + " GuesserDiv:" + counter.GuesserDiv + " ComparePnl:" + CompareRandom.a + " CompareB:" + CompareRandom.b + " CompareC:" + CompareRandom.c + " CompareD:" + CompareRandom.d);
                                ScraperCounter.ResetCounter();
                                CompareRandom.Clear();
                            }
                        }

                        if (MarketVM.Insights.Ready || MarketVM.Insights.Ready15Minutes)
                        {
                            if (counter.GuesserBias < GUESSER_LOW_HIGH_BIAS)
                            {
                                if (counter.GuessNewHighCount > GUESSER_LOW_COUNT_MAX || counter.GuessNewHightCountTwo > GUESSER_LOW_COUNT_MAX)
                                {
                                    SettleWatchingGuesser(WaitingBuy); // -> Success Waiting Mode // <- Fail Watching Mode
                                    AddMessage(NEW_HIGH + counter.GuessNewHighCount + BAR + counter.GuessNewHightCountTwo);
                                    GuardSlimWatchGuess.Release();
                                    return;
                                }
                            }
                            else
                            {
                                if (counter.GuessNewHighCount > GUESSER_HIGH_COUNT_MAX || counter.GuessNewHightCountTwo > GUESSER_HIGH_COUNT_MAX)
                                {
                                    SettleWatchingGuesser(WaitingBuy); // -> Success Waiting Mode // <- Fail Watching Mode
                                    AddMessage(NEW_HIGH + counter.GuessNewHighCount + BAR + counter.GuessNewHightCountTwo);
                                    GuardSlimWatchGuess.Release();
                                    return;
                                }
                            }
                        }
                    }

                    GuardSlimWatchGuess.Release();
                }
            }, ONE, false, resolution: ONE);
        }

        protected private void SideTask()
        {
            _ = Task.Run((() =>
            {
                while (SideTaskStarted)
                {
                    try
                    {
                        if (Orders.Current.Count > ZERO)
                        {
                            var order = Orders.Current[ZERO];
                            var quote = order.CumulativeQuoteQuantityFilled;
                            var quantity = order.QuantityFilled;
                            var price = order.Price;

                            if (order.Side == OrderSide.Sell)
                            {
                                PercentDecimal = ZERO;
                                var downDecimal = decimal.Round(price - (quote / quantity) * ReverseDownPercent / ONE_HUNDRED_PERCENT, (int)QuoteVM.PriceTickSizeScale);

                                InvokeUI.CheckAccess(() =>
                                {
                                    DownDecimal = downDecimal;
                                });
                            }
                            else
                            {
                                var percentDecimal = decimal.Round(price + (quote / quantity) * SellPercent / ONE_HUNDRED_PERCENT, (int)QuoteVM.PriceTickSizeScale);
                                var downDecimal = decimal.Round(PercentDecimal - (quote / quantity) * ReverseDownPercent / ONE_HUNDRED_PERCENT, (int)QuoteVM.PriceTickSizeScale);

                                InvokeUI.CheckAccess(() =>
                                {
                                    PercentDecimal = percentDecimal;
                                    DownDecimal = downDecimal;
                                });
                            }
                        }

                        Thread.Sleep(ONE_HUNDRED);
                    }
                    catch
                    {

                    }
                }
            })).ConfigureAwait(false);
        }

        protected private bool Main(OrderViewModel workingBuy)
        {
            try
            {
                if (!WatchingBlocked)
                {
                    var current = UpdateCurrentPnlPercent(workingBuy);
                    if (current > ZERO)
                    {
                        if (current >= SellPercent)
                        {
                            WatchingBlocked = true;
                            Conductor.InteruptWaiting();

                            if (!DontGuessSellIsChecked)
                            {
                                GuesserWatchingModeEvent(workingBuy); // -> Watching Guesser
                            }
                            else
                            {
                                DontGuessWatching(workingBuy);
                            }

                            return false;
                        }
                    }

                    if (Break())
                    {
                        HardBlock();
                        return false;
                    }

                    var buy = ScraperVM.BuyPrice;
                    if (buy != ZERO)
                    {
                        UpdatePriceBias(buy, out decimal pb);

                        var price = decimal.Round(MarketVM.AverageOneSecond, (int)QuoteVM.PriceTickSizeScale);
                        var nextUp = NextPriceUp;
                        var nextDown = NextPriceDown;
                        bool up = NextBias(price, nextDown, nextUp, pb, DirectionBias, out bool nextBias);
                        if (nextBias)
                        {
                            WatchingBlocked = true;
                            if (!up)
                            {
                                BuySwitchPrice(workingBuy, NEXT_D + PRICE_BIAS + price, true, nextDown);  // -> Success Watching Mode // <- Fail Watching Mode
                                return false;
                            }
                            else
                            {
                                BuySwitchPrice(workingBuy, NEXT_U + PRICE_BIAS + price, true, nextUp);  // -> Success Watching Mode // <- Fail Watching Mode
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog.Error(ex);
                ScraperStopped.Invoke(false, EXCEPTION);
            }

            return true;
        }

        private void WatchingLoopTask(OrderViewModel startOrder)
        {
            if (BuyPrice > ZERO && Quantity > ZERO)
            {
                WatchingBlocked = false;
                while (Started)
                {
                    UpdateStatus(WATCHING, Static.Green);
                    Thread.Sleep(ONE);                      
                  
                    if (Break())
                    {
                        break;
                    }

                    if (!WatchingBlocked)
                    {
                        if (!Main(startOrder))
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                return;
            }
            else
            {
                ScraperStopped.Invoke(false, FAILED_START_PRELAUNCH);
            }

            UpdateStatus(STOPPED, Static.Red);
            ScraperStopped.Invoke(false, FAILED_START);
        }

        protected private void WatchingLoopStartEvent(object sender, OrderViewModel startOrder)
        {
            Conductor.ChangeStatus(ConductorStatus.Watching);
            startOrder.ScraperStatus = true;

            ResetGuesserWatchingLoop();

            if (RealTimeVM.BidPrice == ZERO || RealTimeVM.AskPrice == ZERO)
            {
                ScraperStopped.Invoke(false, TICKER_FAIL);
                return;
            }

            ToggleStart(true);
            ResetPriceQuantity(startOrder, true);
            
            if (!(bool)sender && startOrder.Side != OrderSide.Buy)
            {
                ScraperCounter.CheckStart(OrderSide.Buy);
                _ = Task.Factory.StartNew(() =>
                {
                    WaitingLoopStartEvent(startOrder); // -> Waiting Mode
                }, TaskCreationOptions.DenyChildAttach).ConfigureAwait(false);
            }
            else
            {
                ScraperCounter.CheckStart(OrderSide.Sell);
                _ = Task.Factory.StartNew(() =>
                {
                    WatchingLoopTask(startOrder);  // -> Watching Mode
                }, TaskCreationOptions.DenyChildAttach).ConfigureAwait(false);
            }
        }

        protected private void WaitingLoopStartEvent(OrderViewModel sell)
        {
            ResetGuesserWaitingLoop();
            ResetPriceQuantity(sell, false);
            Conductor.InteruptWaiting();
            ResetWaitingLoop();

            UpdateStatus(WAITING, Static.Green);
            WaitingSell = sell;
            IsSwitchEnabled = true;
            IsAddEnabled = true;
            IsCloseCurrentEnabled = false;

            Conductor.ChangeStatus(ConductorStatus.Waiting);
        }

        protected private void GuesserWatchingModeEvent(OrderViewModel buyOrder)
        {
            IsAddEnabled = false;
            IsCloseCurrentEnabled = false;

            ScraperCounter.ChangeSide(OrderSide.Sell);
            ScraperCounter.ResetGuesserStopwatch();
            CompareRandom.Clear();

            UpdateStatus(GUESS_SELL, Static.Gold);
            WaitingBuy = buyOrder;
            WatchingGuesserBlocked = false;

            Conductor.ChangeStatus(ConductorStatus.GuessingWatch);
        }

        protected private void GuesserWaitingModeEvent(OrderViewModel sellOrder)
        {
            ResetWaitingLoop();

            IsAddEnabled = false;
            IsSwitchEnabled = false;

            ScraperCounter.ChangeSide(OrderSide.Buy);
            ScraperCounter.ResetGuesserStopwatch();
            CompareRandom.Clear();

            UpdateStatus(GUESS_BUY, Static.Gold);
            WaitingSell = sellOrder;
            WaitingGuesserBlocked = false;

            Conductor.ChangeStatus(ConductorStatus.GuessingWait);
        }

        protected private void ResetGuesserWatchingLoop()
        {
            WatchingGuesserBlocked = true;
            ScraperCounter.ResetGuesserStopwatch();
        }

        protected private void ResetGuesserWaitingLoop()
        {
            WaitingGuesserBlocked = true;
            ScraperCounter.ResetGuesserStopwatch();
        }

        private void DontGuessWaiting(OrderViewModel sellOrder)
        {
            ScraperCounter.ResetGuesserStopwatch();
            CompareRandom.Clear();
            IsSwitchEnabled = false;
            SettleWaitingGuesserDontGuess(sellOrder);
        }

        private void DontGuessWatching(OrderViewModel workingBuy)
        {
            ScraperCounter.ResetGuesserStopwatch();
            CompareRandom.Clear();
            IsAddEnabled = false;
            SettleWatchingGuesser(workingBuy);
        }

        protected private void SettleWaitingGuesser(OrderViewModel sellOrder)
        {
            WaitingGuesserBlocked = true;

            if (UpdateReversePnLPercent(sellOrder, decimal.Round(OrderHelper.PnLAsk(sellOrder, RealTimeVM.AskPrice), App.DEFAULT_ROUNDING_PLACES)) >= ReverseDownPercent)
            {
                if (BuySwitchPrice(sellOrder, BUY_PROCESSED, false, DownDecimal)) // -> Success Watching Mode // <- Fail Waiting Mode
                {
                    ResetGuesserWaitingLoop();
                }
            }
            else
            {
                WaitingLoopStartEvent(sellOrder); // -> Fail Waiting Mode
            }
        }

        protected private void SettleWaitingGuesserDontGuess(OrderViewModel sellOrder)
        {
            WaitingGuesserBlocked = true;

            if (BuySwitchPrice(sellOrder, BUY_PROCESSED, false, RealTimeVM.AskPrice + QuoteVM.PriceTickSize)) // -> Success Watching Mode // <- Fail Waiting Mode
            {
                ResetGuesserWaitingLoop();
            }
        }

        protected private void SettleWatchingGuesser(OrderViewModel buyOrder)
        {
            WatchingGuesserBlocked = true;

            bool canEnter = SlimSell.Wait(ZERO);
            if (canEnter)
            {
                if (UpdatePnlPercent(buyOrder, decimal.Round(OrderHelper.PnLBid(buyOrder, RealTimeVM.BidPrice), App.DEFAULT_ROUNDING_PLACES)) >= SellPercent)
                {
                    if (SellSwitch(buyOrder, PercentDecimal)) // -> Success Waiting Mode // <- Failed Watching Mode
                    {
                        ResetGuesserWatchingLoop();
                    }
                }
                else
                {
                    WatchingLoopStartEvent(true, buyOrder); // <- Failed Watching Mode
                }

                SlimSell.Release();
            }
            else
            {
                AddMessage(BLOCKED_SELL);
                ResetGuesserWatchingLoop();
            }
        }

        protected private void UpdatePriceBias(decimal currentPrice, out decimal priceBias)
        {
            priceBias = ScraperVM.PriceBias;
            ScraperVM.NextPriceUp = currentPrice + priceBias;
            ScraperVM.NextPriceDown = currentPrice - priceBias;
        }

        protected private bool TimePrice()
        {
            if (Loops >= (WaitTime * FIVE_HUNDRED))
            {
                if (CountDisplay == WaitTimeCount)
                {
                    return true;
                }

                InvokeUI.CheckAccess(() =>
                {
                    CountDisplay++;
                });

                ResetWaitingLoop();

                UpdateStatus(LEFT_BRACKET + CountDisplay + SLASH + WaitTimeCount + RIGHT_BRACKET, Static.Green);
                NotifyVM.Notification(WAITING_TO_BUY, Static.Green);
            }

            return false;
        }

        protected private decimal UpdateCurrentPnlPercent(OrderViewModel workingBuy)
        {
            return UpdateCurrentPnlPercentInternal(workingBuy, out decimal pnl);
        }

        protected private bool CalculateReverse(OrderViewModel sell, bool reverse = false)
        {
            return CalculateReverseInternal(sell, ReverseDownPercent, reverse);
        }

        public bool SellSwitch(OrderViewModel oldBuyOrder, decimal price)
        {
            if (UseLimitSell)
            {
                return ProcessNextSellOrderLimit(oldBuyOrder, price);
            }
            else
            {
                return ProcessNextSellOrderMarket(oldBuyOrder);
            }
        }

        public bool SellSwitchClose(OrderViewModel oldBuyOrder, decimal price)
        {
            if (UseLimitClose)
            {
                return ProcessNextSellOrderLimit(oldBuyOrder, price);
            }
            else
            {
                return ProcessNextSellOrderMarket(oldBuyOrder);
            }
        }

        protected private bool ProcessNextSellOrderLimit(OrderViewModel oldBuyOrder, decimal price)
        {
            OrderViewModel? nextSwitchBuyOrder = NextSwitchBuyOrder();
            WebCallResult<BinancePlacedOrder> sellResult = Trade.PlaceOrderLimitFoKAsync(Symbol, Quantity, Mode, false, OrderSide.Sell, price).Result;
            if (sellResult.Success)
            {
                OrderViewModel newSellOrder = OrderBase.NewScraperOrder(sellResult.Data, Mode);

                if (newSellOrder.Status == OrderStatus.Filled)
                {
                    AfterSell(new OrderPair(oldBuyOrder, newSellOrder), nextSwitchBuyOrder); // -> Success Waiting Mode
                    return false;
                }
                else
                {
                    FailedFoKOrderTask.Invoke(null, newSellOrder);
                }
            }

            WatchingLoopStartEvent(true, oldBuyOrder); // -> Failed Watching Mode
            AddMessage(FAILED_FOK_SELL_WATCH);
            return true;
        }

        protected private bool ProcessNextSellOrderMarket(OrderViewModel oldBuyOrder)
        {
            OrderViewModel? nextSwitchBuyOrder = NextSwitchBuyOrder();
            WebCallResult<BinancePlacedOrder> sellResult = Trade.PlaceOrderMarketAsync(Symbol, Quantity, Mode, false, OrderSide.Sell).Result;

            if (sellResult.Success && sellResult.Data.Status == OrderStatus.Filled)
            {
                AfterSell(new OrderPair(oldBuyOrder, OrderBase.NewScraperOrder(sellResult.Data, Mode)), nextSwitchBuyOrder); // -> Success Waiting Mode
                return false;
            }

            WatchingLoopStartEvent(true, oldBuyOrder); // -> Failed Watching Mode
            AddMessage(FAILED_MARKET_SELL_WATCH);
            return true;
        }

        public bool BuySwitchAdd(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail, decimal price)
        {
            if (UseLimitAdd)
            {
                return ProcessNextBuyOrderPriceLimit(oldOrder, buyReason, watchingModeOnFail, price);
            }
            else
            {
                return ProcessNextBuyOrderPriceMarket(oldOrder, buyReason, watchingModeOnFail);
            }
        }

        public bool BuySwitchPrice(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail, decimal price)
        {
            bool canEnter = SlimBuy.Wait(ZERO);
            if (canEnter)
            {
                if (UseLimitBuy)
                {
                    bool b = ProcessNextBuyOrderPriceLimit(oldOrder, buyReason, watchingModeOnFail, price);
                    SlimBuy.Release();
                    return b;
                }
                else
                {
                    bool b = ProcessNextBuyOrderPriceMarket(oldOrder, buyReason, watchingModeOnFail);
                    SlimBuy.Release();
                    return b;
                }
            }
            else
            {
                AddMessage(BLOCKED_BUY);
            }

            return true;
        }

        public bool BuySwitchAskPrice(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail)
        {
            bool canEnter = SlimBuy.Wait(ZERO);
            if (canEnter)
            {
                IsAddEnabled = false;
                if (UseLimitBuy)
                {
                    bool b = ProcesNextBuyOrderAskPriceLimit(oldOrder, buyReason, watchingModeOnFail);
                    SlimBuy.Release();
                    return b;
                }
                else
                {
                    bool b = ProcessNextBuyOrderPriceMarket(oldOrder, buyReason, watchingModeOnFail);
                    SlimBuy.Release();
                    return b;
                }
            }
            else
            {
                AddMessage(BLOCKED_BUY);
            }

            return true;
        }

        protected private bool ProcesNextBuyOrderAskPriceLimit(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail)
        {
            if (!ProcessNextBuyOrderInternalLimit(buyReason, oldOrder, RealTimeVM.AskPrice, watchingModeOnFail))
            {
                return false;
            }

            return true;
        }

        protected private bool ProcessNextBuyOrderPriceLimit(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail, decimal price)
        {
            if (!ProcessNextBuyOrderInternalLimit(buyReason, oldOrder, price, watchingModeOnFail))
            {
                return false;
            }

            return true;
        }

        protected private bool ProcessNextBuyOrderPriceMarket(OrderViewModel oldOrder, string buyReason, bool watchingModeOnFail)
        {
            if (!ProcessNextBuyOrderInternalMarket(buyReason, oldOrder, watchingModeOnFail))
            {
                return false;
            }

            return true;
        }

        protected private bool ProcessNextBuyOrderInternalLimit(string buyReason, OrderViewModel oldOrder, decimal price, bool watchingModeOnFail = false)
        {
            WebCallResult<BinancePlacedOrder> buyResult = Trade.PlaceOrderLimitFoKAsync(Symbol, Quantity, Mode, false, OrderSide.Buy, price).Result;

            if (buyResult.Success)
            {
                OrderViewModel newBuyOrder = OrderBase.NewScraperOrder(buyResult.Data, Mode);
                newBuyOrder = Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(newBuyOrder);

                if (buyResult.Data.Status == OrderStatus.Filled)
                {
                    AfterBuy(buyReason, new OrderPair(newBuyOrder, oldOrder));
                    return true;
                }
                else
                {
                    FailedFoKOrderTask.Invoke(null, newBuyOrder);
                }
            }

            if (watchingModeOnFail)
            {
                AddMessage(FAILED_FOK_BUY_WATCH);
                WatchingLoopStartEvent(true, oldOrder); // -> Fail Watching Mode
            }
            else
            {
                AddMessage(FAILED_FOK_BUY_WAIT);
                WaitingLoopStartEvent(oldOrder); // -> Fail Waiting Mode
            }

            return false;
        }

        protected private bool ProcessNextBuyOrderInternalMarket(string buyReason, OrderViewModel oldOrder, bool watchingModeOnFail = false)
        {
            WebCallResult<BinancePlacedOrder> buyResult = Trade.PlaceOrderMarketAsync(Symbol, Quantity, Mode, false, OrderSide.Buy).Result;

            if (buyResult.Success && buyResult.Data.Status == OrderStatus.Filled)
            {
                AfterBuy(buyReason, new OrderPair(Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(OrderBase.NewScraperOrder(buyResult.Data, Mode)), oldOrder));
                return true;
            }

            if (watchingModeOnFail)
            {
                AddMessage(FAILED_MARKET_BUY_WATCH);
                WatchingLoopStartEvent(true, oldOrder); // -> Fail Watching Mode
            }
            else
            {
                AddMessage(FAILED_MARKET_BUY_WAIT);
                WaitingLoopStartEvent(oldOrder); // -> Fail Waiting Mode
            }

            return false;
        }

        protected private void SetRunningTotal(decimal delta)
        {
            RunningTotal += delta;
            if (RunningTotal < ZERO)
            {
                RunningTotalColor = Static.Red;
            }
            else
            {
                RunningTotalColor = Static.Green;
            }
        }

        protected private void ResetPriceQuantity(OrderViewModel order, bool buy)
        {
            if (buy)
            {
                BuyPrice = order.Price;
                WaitPrice = ZERO;
            }
            else
            {
                WaitPrice = order.Price;
                BuyPrice = ZERO;
            }

            Quantity = order.Quantity;
        }

        protected private void ToggleStart(bool start)
        {
            Started = start;
            IsSwitchEnabled = false;
            IsSettingsEnabled = !start;
            IsStartEnabled = !start;
            IsStopEnabled = start;
            IsChangeBiasEnabled = start;
            IsAddEnabled = start;
            IsCloseCurrentEnabled = start;
        }

        protected private void BlockButtons()
        {
            InvokeUI.CheckAccess(() =>
            {
                IsSwitchEnabled = false;
                IsSettingsEnabled = false;
                IsStartEnabled = false;
                IsStopEnabled = false;
                IsChangeBiasEnabled = false;
                IsAddEnabled = false;
                IsCloseCurrentEnabled = false;
            });
        }

        protected private void ResetNext()
        {
            InvokeUI.CheckAccess(() =>
            {
                WaitPrice = ZERO;
                NextPriceDown = ZERO;
                PercentDecimal = ZERO;
                DownDecimal = ZERO;
                NextPriceUp = ZERO;
                CountDisplay = ZERO;
            });
        }

        protected private void ResetWaitingLoop()
        {
            InvokeUI.CheckAccess(() =>
            {
                Loops = ZERO;
                Up = ZERO;
                Down = ZERO;
            });
        }

        protected private void Reset()
        {
            HardBlock();
            ResetNext();
        }

        protected private void HardBlock()
        {
            WatchingBlocked = true;
            Conductor.InteruptWaiting();
            ResetWaitingLoop();
            Conductor.WatchingGuesserTimer.StopSilent();
            ResetGuesserWatchingLoop();
            Conductor.WaitingGuesserTimer.StopSilent();
            ResetGuesserWaitingLoop();
        }

        protected private bool Break()
        {
            if (!Core.MainVM.IsSymbolSelected || Started == false)
            {
                return true;
            }

            return false;
        }

        protected private void AddMessage(string message)
        {
            LogText = message;
        }

        protected private Bias GetBias(ComboBoxItem co)
        {
            if (co != null)
            {
                if (co.Content != null)
                {
                    return co.Content.ToString() == NONE ? Bias.None : co.Content.ToString() == BEARISH ? Bias.Bearish : Bias.Bullish;
                }
            }

            return Bias.None;
        }

        protected private void UpdateStatus(string message, SolidColorBrush c)
        {
            if (Status != message)
            {
                InvokeUI.CheckAccess(() =>
                {
                    Status = message;
                    StatusColor = c;
                });
            }
        }

        public OrderViewModel? NextSwitchBuyOrder()
        {
            if (SwitchAutoIsChecked)
            {
                lock (MainOrders.OrderUpdateLock)
                {
                    var count = Orders.Current.Count;
                    if (count >= TWO)
                    {
                        var order = Orders.Current[ONE];
                        if (order.Side == OrderSide.Buy && !order.ScraperStatus)
                        {
                            return order;
                        }
                    }
                }
            }

            Orders.RemoveHiddenOrders();
            return null;
        }

        public void AfterSell(OrderPair pair, OrderViewModel? switchOrder)
        {
            pair.Sell.ScraperStatus = true;
            pair.Sell = Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(pair.Sell);

            pair.Buy.IsOrderHidden = true;
            pair.Buy.ScraperStatus = false;
            pair.Buy.IsOrderCompleted = true;
            pair.Buy = Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(pair.Buy);

            Orders.RemoveHiddenOrders();

            SellOrderEvent.Invoke(null, pair);

            if (SwitchAutoIsChecked)
            {
                if (switchOrder != null)
                {
                    SwitchToNextSell(pair.Sell, switchOrder);
                    return;
                }
                else
                {
                    AddMessage(SWITCHING_NO_ORDER);
                    SwitchAutoIsChecked = false;
                }
            }

            WaitingLoopStartEvent(pair.Sell); // -> Success Waiting Mode
        }

        public void SellOrderTask(object o, OrderPair pair)
        {
            var totalS = pair.Sell.Total;
            if (totalS == 0) // todo: Is this a bug or does the server sometimes not send QuantityFilled for Filled FOK orders? Makes sense to me since its a waste of compute
            {
                totalS = pair.Sell.Price * pair.Sell.Quantity; // All orders placed by the scraper are FOK or Market
                WriteLog.Info("Abstracting CQF for sell order: " + pair.Sell.OrderId);
            }

            var pnl = (totalS - pair.Buy.Total).Normalize();
            var positive = pnl > ZERO;

            SetRunningTotal(pnl);
            string pnlr = (positive ? PLUS : EMPTY) + pnl;

            if (positive)
            {
                WinCount++;
            }
            else
            {
                LoseCount++;
            }

            NotifyVM.Notification(SOLD + pnlr + RIGHT_BRACKET + WAITING_BUY, positive ? Static.Green : Static.Red);
            AddMessage(SELL_PROCESSED + pnlr);

            WriteLog.Info(AFTER_SELL + pair.Sell.OrderId + PRICE + pair.Sell.Price + QUANTITY + pair.Sell.Quantity + QUANTITY_FILLED + pair.Sell.QuantityFilled + CQCF + pair.Sell.CumulativeQuoteQuantityFilled + TYPE + pair.Sell.Type + STATUS + pair.Sell.Status + OTHER_ORDER + pair.Buy.OrderId);
        }

        public void AfterBuy(object o, OrderPair pair)
        {
            pair.Sell.ScraperStatus = false; // Sell Could be OldOrder

            if (pair.Sell.Side == OrderSide.Sell) // Add Buy Orders are not Completed or Hidden
            {
                pair.Sell.IsOrderCompleted = true;
                pair.Sell.IsOrderHidden = true;
            }

            pair.Sell = Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(pair.Sell);

            Orders.RemoveHiddenOrders();

            BuyOrderEvent.Invoke(o, pair);

            WatchingLoopStartEvent(true, Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(pair.Buy)); // -> Success Watching Mode
        }

        public void BuyOrderTask(object o, OrderPair pair)
        {
            NotifyVM.Notification(ORDER_PLACED + pair.Buy.Symbol + QUANTITY + pair.Buy.Quantity, Static.Gold);
            AddMessage(o.ToString());

            WriteLog.Info(AFTER_BUY + pair.Buy.OrderId + PRICE + pair.Buy.Price + QUANTITY + pair.Buy.Quantity + QUANTITY_FILLED + pair.Buy.QuantityFilled + CQCF + pair.Buy.CumulativeQuoteQuantityFilled + TYPE + pair.Buy.Type + STATUS + pair.Buy.Status + OTHER_ORDER + pair.Sell.OrderId);
        }

        public void FailedFokOrderEvent(object o, OrderViewModel sellOrder)
        {
            sellOrder.IsOrderHidden = true;
            Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(sellOrder);
            Orders.RemoveHiddenOrders();
        }

        protected private bool SwitchToNext(OrderViewModel one, OrderViewModel two)
        {
            var sell = one.Side == OrderSide.Sell ? one : two;
            var buy = two.Side == OrderSide.Buy ? two : one;

            if (!NotLimitOrFilled(sell) || !NotLimitOrFilled(buy))
            {
                ScraperStopped.Invoke(false, NO_LIMIT_SWITCH);
                return false;
            }

            if (sell.Side == OrderSide.Sell && buy.Side == OrderSide.Buy)
            {
                Reset();

                sell.ScraperStatus = false;
                sell.IsOrderHidden = true;
                Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(sell);

                Orders.RemoveHiddenOrders();

                AddMessage(SWITCHING);

                WatchingLoopStartEvent(true, buy); // -> Watching Mode            
                return true;
            }

            NotifyVM.Notification(SWITCH_ERROR);
            IsSwitchEnabled = true;
            return false;
        }

        protected private bool SwitchToNextSell(OrderViewModel one, OrderViewModel two)
        {
            var sell = one.Side == OrderSide.Sell ? one : two;
            var buy = two.Side == OrderSide.Buy ? two : one;

            if (!NotLimitOrFilled(buy))
            {
                ScraperStopped.Invoke(false, NO_LIMIT_SWITCH);
                return false;
            }

            if (sell.Side == OrderSide.Sell && buy.Side == OrderSide.Buy)
            {
                IsSwitchEnabled = false;
                Reset();

                sell.ScraperStatus = false;
                sell.IsOrderHidden = true;
                Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(sell);

                Orders.RemoveHiddenOrders();

                AddMessage(SWITCHING);

                WatchingLoopStartEvent(true, buy); // -> Watching Mode            
                return true;
            }

            NotifyVM.Notification(SWITCH_ERROR);
            IsSwitchEnabled = true;
            return false;
        }

        public void Stop(object sender, string reason = EMPTY)
        {
            try
            {
                BlockButtons();
                SideTaskStarted = false;
                Reset();

                OrderViewModel? order = null;
                lock (MainOrders.OrderUpdateLock)
                {
                    InvokeUI.CheckAccess(() =>
                    {
                        if (Orders.Current.Count > ZERO)
                        {
                            order = Orders.Current[ZERO];
                        }

                        foreach (OrderViewModel o in Orders.Current)
                        {
                            o.ScraperStatus = false;
                        }
                    });
                }

                if (order != null)
                {
                    order.ScraperStatus = false;
                    Static.ManageStoredOrders.ScraperOrderContextFromMemoryStorage(order);
                }

                Orders.RemoveHiddenOrders();

                if (reason != EMPTY)
                {
                    WriteLog.Error(reason);
                    NotifyVM.Notification(reason);
                }

                SlimBuy.Dispose();
                SlimSell.Dispose();

                ScraperCounter.Stop();

                if (SymbolTicker != null)
                {
                    SymbolTicker.TickerUpdated -= TickerUpdateEvent;
                    Tickers.RemoveOwnership(Symbol, Owner.Scraper);
                }

                ToggleStart(false);
                AddMessage(STOPPED);

                if (!(bool)sender)
                {
                    UpdateStatus(STOPPED, Static.Red);
                }
                else
                {
                    UpdateStatus(SCRAPER_DIED, Static.Red);
                }

                Conductor.ChangeStatus(ConductorStatus.Idle);
            }
            catch (Exception e)
            {
                WriteLog.Error(e);
            }
        }

        private void TickerUpdateEvent(object o, TickerResultEventArgs e)
        {
            if (ScraperCounter.CounterDirection == OrderSide.Sell)
            {
                GuesserLastPriceTicker = ScraperCounter.Count(e.BestBid, GuesserLastPriceTicker);
            }
            else
            {
                GuesserLastPriceTicker = ScraperCounter.Count(e.BestAsk, GuesserLastPriceTicker);
            }
        }

        public bool NotLimitOrFilled(OrderViewModel o)
        {
            if (o.Type != OrderType.Limit)
            {
                return true;
            }

            return o.Type == OrderType.Limit && o.Status == OrderStatus.Filled;
        }

        public void ClearStats(object o)
        {
            _ = Task.Run((() =>
            {
                InvokeUI.CheckAccess(() =>
                {
                    WinCount = ZERO;
                    logText = string.Empty;
                    PropChanged(LOGTEXT);
                    StatusColor = Static.White;
                    LoseCount = ZERO;
                    RunningTotal = ZERO;
                    RunningTotalColor = Static.White;
                    ClearStatsIsChecked = false;
                });
            }));
        }

        public void CloseCurrent(object o)
        {
            IsCloseCurrentEnabled = false;
            IsAddEnabled = false;

            _ = Task.Run(() =>
            {
                if (SlimSell.Wait(ZERO))
                {
                    HardBlock();

                    if (Orders.Current.Count > ZERO)
                    {
                        OrderViewModel buyOrder = Orders.Current[ZERO];
                        if (buyOrder.Side == OrderSide.Buy && buyOrder.ScraperStatus)
                        {
                            var bid = RealTimeVM.BidPrice - QuoteVM.PriceTickSize;
                            var pnl = decimal.Round(OrderHelper.PnLBid(buyOrder, bid), App.DEFAULT_ROUNDING_PLACES);

                            if (UpdatePnlPercent(buyOrder, pnl) > 0.0002m)
                            {
                                SellSwitchClose(buyOrder, bid); // -> Success Waiting Mode // <- Failed Watching Mode
                                SlimSell.Release();
                                return;
                            }
                            else
                            {
                                WatchingLoopStartEvent(true, buyOrder); // -> Failed Watching Mode               
                                AddMessage("Refused Unprofitable Trade");
                                SlimSell.Release();
                                return;
                            }
                        }
                        else
                        {
                            NotifyVM.Notification(ORDER_MISMATCH);

                            InvokeUI.CheckAccess(() =>
                            {
                                IsAddEnabled = true;
                            });

                            SlimSell.Release();
                            return; // -> Ignore
                        }
                    }
                    else
                    {
                        ScraperStopped.Invoke(false, NO_ORDER_ERROR); // -> Error Stop
                        SlimSell.Release();
                        return;
                    }
                }
                else
                {
                    AddMessage("Busy Closing/Selling");
                }
            }).ConfigureAwait(false);
        }

        public void Switch(object o)
        {
            IsSwitchEnabled = false;

            _ = Task.Run(() =>
            {
                OrderViewModel? orderOne = null;
                OrderViewModel? orderTwo = null;

                lock (MainOrders.OrderUpdateLock)
                {
                    if (Orders.Current.Count >= TWO)
                    {
                        orderOne = Orders.Current[ZERO];
                        orderTwo = Orders.Current[ONE];
                    }
                }

                if (orderOne != null && orderTwo != null)
                {
                    if (!orderOne.IsOrderCompleted && !orderTwo.IsOrderCompleted)
                    {
                        SwitchToNext(orderOne, orderTwo);
                        return;
                    }
                    else
                    {
                        NotifyVM.Notification(ORDER_WAS_COMPLETE, Static.Red);
                    }
                }
                else
                {
                    NotifyVM.Notification(SWITCH_ERROR, Static.Red);
                }
            }).ConfigureAwait(false);
        }

        public void UserAdd(object o)
        {
            IsAddEnabled = false;
            decimal price = RealTimeVM.AskPrice;

            _ = Task.Run(() =>
            {
                bool canEnter = SlimBuy.Wait(ZERO);
                if (canEnter)
                {
                    try
                    {
                        OrderViewModel? order = null;
                        lock (MainOrders.OrderUpdateLock)
                        {
                            InvokeUI.CheckAccess(() =>
                            {
                                if (Orders.Current.Count > ZERO)
                                {
                                    order = Orders.Current[ZERO];
                                }
                            });
                        }

                        if (order != null)
                        {
                            if (NotLimitOrFilled(order))
                            {
                                Reset();

                                if (order.Side == OrderSide.Buy)
                                {
                                    BuySwitchAdd(order, USER_ADDED, true, price);  // -> Success Watching Mode // <- Fail Watching Mode
                                }
                                else
                                {
                                    BuySwitchAdd(order, USER_ADDED, false, price); // -> Success Watching Mode // <- Fail Waiting Mode

                                }
                                SlimBuy.Release();
                                return;
                            }
                            else
                            {
                                ScraperStopped.Invoke(false, NO_LIMIT_ADD);
                                SlimBuy.Release();
                                return;
                            }
                        }

                        ScraperStopped.Invoke(false, NO_BASIS);
                        SlimBuy.Release();
                        return;
                    }
                    catch (Exception ex)
                    {
                        ScraperStopped.Invoke(false, EXCEPTION_ADDING);
                        WriteLog.Error(ex);
                        SlimBuy.Release();
                        return;
                    }
                }
                else
                {
                    AddMessage(BLOCKED_BUY);
                }
            }).ConfigureAwait(false);
        }

        public void Start(object o)
        {
            IsStartEnabled = false;

            _ = Task.Run(() =>
            {
                try
                {
                    OrderViewModel? anyOrder = null;
                    lock (MainOrders.OrderUpdateLock)
                    {
                        if (Orders.Current.Count > ZERO)
                        {
                            anyOrder = Orders.Current[ZERO];
                        }
                    }

                    if (anyOrder != null)
                    {
                        if (!NotLimitOrFilled(anyOrder))
                        {
                            NotifyVM.Notification(NO_LIMIT_START);
                            return;
                        }

                        AddMessage(STARTED);

                        if (!SideTaskStarted)
                        {
                            SideTaskStarted = true;
                            SlimBuy = new SemaphoreSlim(ONE, ONE);
                            SlimSell = new SemaphoreSlim(ONE, ONE);
                            SymbolTicker = Tickers.AddTicker(Symbol, Owner.Scraper).Result;
                            SymbolTicker.TickerUpdated += TickerUpdateEvent;
                            SideTask();
                        }

                        if (WaitTimeCount == ZERO)
                        {
                            InvokeUI.CheckAccess(() =>
                            {
                                WaitTimeCount = ONE;
                            });
                        }

                        if (PriceBias == ZERO)
                        {
                            var bias = anyOrder.Price / ONE_HUNDRED_PERCENT;
                            InvokeUI.CheckAccess(() =>
                            {
                                PriceBias = bias;
                            });
                        }

                        WatchingLoopStartEvent(false, anyOrder); // -> Watching Mode
                        return;
                    }

                    ScraperStopped.Invoke(false, NO_ORDER_ERROR);
                }
                catch (Exception ex)
                {
                    ScraperStopped.Invoke(false, EXCEPTION_STARTING);
                    WriteLog.Error(ex);
                }
            }).ConfigureAwait(false);
        }

        public void Stop(object o)
        {
            IsStopEnabled = false;

            _ = Task.Run(() =>
            {
                ScraperStopped.Invoke(false, STOPPED_REQUEST);
            }).ConfigureAwait(false);
        }

        public void ToggleDontGuessSell(object o)
        {
            DontGuessSellIsChecked = !DontGuessSellIsChecked;
        }

        public void ToggleDontGuessBuy(object o)
        {
            DontGuessBuyIsChecked = !DontGuessBuyIsChecked;

            if (!DontGuessBuyIsChecked)
            {
                BuyReverseIsChecked = false;
            }
        }

        public void ToggleUseLimitBuy(object o)
        {
            UseLimitBuy = !UseLimitBuy;
        }

        public void ToggleUseLimitSell(object o)
        {
            UseLimitSell = !UseLimitSell;
        }

        public void ToggleUseLimitClose(object o)
        {
            UseLimitClose = !UseLimitClose;
        }

        public void ToggleUseLimitAdd(object o)
        {
            UseLimitAdd = !UseLimitAdd;
        }

        public void ToggleSwitchAuto(object o)
        {
            SwitchAutoIsChecked = !SwitchAutoIsChecked;
        }

        public void IncreaseWaitTime(object o)
        {
            if (WaitTime >= MAX_WAIT_TIME)
            {
                WaitTime = MAX_WAIT_TIME;
            }

            WaitTime = waitTime + WAIT_DELTA;
        }

        public void DecreaseWaitTime(object o)
        {
            if (WaitTime <= WAIT_MIN)
            {
                WaitTime = WAIT_MIN;
                return;
            }

            WaitTime = waitTime - WAIT_DELTA;
        }

        public void IncreaseBias(object o)
        {
            var b = PriceBias + StepSize;
            if (b > QuoteVM.PriceTickSize)
            {
                PriceBias = b;
            }
        }

        public void DecreaseBias(object o)
        {
            var b = PriceBias - StepSize;
            if (b > QuoteVM.PriceTickSize)
            {
                PriceBias = b;
            }
        }

        public void IncreaseStep(object o)
        {
            var b = StepSize * TWO;

            if (b < QuoteVM.PriceTickSize)
            {
                StepSize = QuoteVM.PriceTickSize;
                return;
            }

            StepSize = b;
        }

        public void DecreaseStep(object o)
        {
            var b = StepSize / TWO;

            if (b < QuoteVM.PriceTickSize)
            {
                StepSize = QuoteVM.PriceTickSize;
                return;
            }

            StepSize = b;
        }

        public void IncreaseScrapePercent(object o)
        {
            var p = SellPercent + MINIMUM_STEP;
            switch (p)
            {
                case > ONE_HUNDRED_PERCENT:
                    SellPercent = ONE_HUNDRED_PERCENT;
                    break;
                case < MINIMUM_STEP:
                    SellPercent = MINIMUM_STEP;
                    break;
                default:
                    SellPercent = p;
                    break;
            }
        }

        public void DecreaseScrapePercent(object o)
        {
            var p = SellPercent - MINIMUM_STEP;
            switch (p)
            {
                case > ONE_HUNDRED_PERCENT:
                    SellPercent = ONE_HUNDRED_PERCENT;
                    break;
                case < MINIMUM_STEP:
                    SellPercent = MINIMUM_STEP;
                    break;
                default:
                    SellPercent = p;
                    break;
            }
        }

        public void IncreaseWaitCount(object o)
        {
            WaitTimeCount = WaitTimeCount + ONE;
        }

        public void DecreaseWaitCount(object o)
        {
            if (WaitTimeCount - ONE == ZERO)
            {
                return;
            }

            WaitTimeCount = WaitTimeCount - ONE;
        }

        public void IncreaseDownReverse(object o)
        {
            var p = ReverseDownPercent + MINIMUM_STEP;
            switch (p)
            {
                case > ONE_HUNDRED_PERCENT:
                    ReverseDownPercent = ONE_HUNDRED_PERCENT;
                    break;
                case < MINIMUM_STEP:
                    ReverseDownPercent = MINIMUM_STEP;
                    break;
                default:
                    ReverseDownPercent = p;
                    break;
            }
        }

        public void DecreaseDownReverse(object o)
        {
            var p = ReverseDownPercent - MINIMUM_STEP;
            switch (p)
            {
                case > ONE_HUNDRED_PERCENT:
                    ReverseDownPercent = ONE_HUNDRED_PERCENT;
                    break;
                case < MINIMUM_STEP:
                    ReverseDownPercent = MINIMUM_STEP;
                    break;
                default:
                    ReverseDownPercent = p;
                    break;
            }
        }
    }
}
