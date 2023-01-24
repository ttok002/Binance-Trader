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

using BinanceAPI.Enums;
using BinanceAPI.Objects;
using BinanceAPI.Objects.Shared;
using BinanceAPI.Objects.Spot.UserStream;
using BinanceAPI.Objects.Spot.WalletData;
using BTNET.BV.Base;
using BTNET.BV.Enum;
using BTNET.BVVM.BT;
using BTNET.BVVM.BT.Orders;
using BTNET.BVVM.Helpers;
using BTNET.BVVM.Log;
using BTNET.VM.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BTNET.BVVM
{
    public class MainOrders : Core
    {
        private const string FAILURE_GETTING_ORDERS = "Failure while getting Orders: ";
        private const string ORDER = "Order: ";
        private const string REJECTED_UPDATE = " was Rejected OnOrderUpdate: ";
        private const string MAIN_TRADE_FEES = "[Main] Trade Fees for ";
        private const string TAKER = " | Taker Fee: [";
        private const string MAKER = " %) | Maker Fee: [";
        private const string UGH3 = "](";
        private const string UGH = " %)";
        private const string UGH2 = ": ";
        private const string PERCENTT = "% | T:";
        private const string RIGHT_BRACKET = "]";
        private const string LEFT_BRACKET = "[";
        private const string UGH4 = "(";
        private const string M = "M:";
        private const string PERCENT = "%";

        private const int ZERO = 0;
        private const int ONE_HUNDRED = 100;
        private const int DELAY_TIME_MS = 1;
        private const int EXPIRE_TIME_MS = 60000;
        private const int RECV_WINDOW = 1500;
        private const int NUM_ORDERS = 500;
        private const int SERVER_ORDER_DELAY_MINS = 5;
        private const int INTEREST_RATE_CHECK_DELAY_MINS = 60;
        private const int TRADE_FEE_CHECK_DELAY_MINS = 60;

        private decimal _currentInterestRate;
        private BinanceTradeFee? _currentTradeFee;

        public static readonly object OrderUpdateLock = new object();

        public bool LastChanceStop { get; set; } = false;

        /// <summary>
        /// The last time orders were updated, Needs to be reset when mode/symbol changes
        /// </summary>
        public static DateTime LastRun { get; set; }

        private ObservableCollection<OrderViewModel> orders = new();

        public ObservableCollection<OrderViewModel> Current
        {
            get => orders;
            set
            {
                orders = value;
                PropChanged();
            }
        }

        public static ConcurrentQueue<OrderUpdate> DataEventWaiting { get; } = new();

        public void AddNewOrderUpdateEventsToQueue(BinanceStreamOrderUpdate order, TradingMode tradingMode)
        {
            OrderUpdate orderUpdate = new OrderUpdate(order, tradingMode);
            DataEventWaiting.Enqueue(orderUpdate);
#if DEBUG
            WriteLog.Info("Got An Order Update: " + order.OrderId + " | " + order.Quantity + "| " + order.Type + " | " + order.Status);
#endif
        }

        public async Task UpdateOrdersCurrentSymbolAsync(string symbol)
        {
            try
            {
                if (!StoredExchangeInfo.IsNull())
                {
                    while (DataEventWaiting.TryPeek(out _))
                    {
                        bool b = DataEventWaiting.TryDequeue(out OrderUpdate NewOrder);
                        if (b)
                        {
                            switch (NewOrder.Update.Status)
                            {
                                case OrderStatus.Canceled:
                                    {
                                        continue;
                                    }
                                case OrderStatus.Rejected:
                                    {
                                        WriteLog.Error(ORDER + NewOrder.Update.OrderId + REJECTED_UPDATE + NewOrder.Update.RejectReason);
                                        continue;
                                    }
                                default:
                                    {
                                        ProcessOrder(NewOrder);
                                        continue;
                                    }
                            }
                        }
                    }
                }

                bool defer = false;
                lock (MainOrders.OrderUpdateLock)
                {
                    IEnumerable<OrderViewModel>? OrderUpdate = Static.ManageStoredOrders.GetCurrentSymbolMemoryStoredOrders(symbol, out defer);
                    if (OrderUpdate != null)
                    {
                        if (OrderUpdate.Any())
                        {
                            bool updated = false;

                            List<OrderViewModel> temp = Current.ToList();
                            if (temp.Count == ZERO)
                            {
                                temp = OrderUpdate.ToList();
                                if (temp.Count > ZERO)
                                {
                                    updated = true;
                                }
                            }
                            else
                            {
                                foreach (var order in OrderUpdate.ToList())
                                {
                                    var exists = temp.Where(t => t.OrderId == order.OrderId).Any();
                                    if (!exists)
                                    {
                                        temp.Add(order);
                                        updated = true;
#if DEBUG
                                    WriteLog.Info("Dequeued: " + order.OrderId + " | " + order.Quantity + "| " + order.Type + " | " + order.Status);
#endif
                                    }
                                }
                            }

                            if (updated && !LastChanceStop)
                            {
                                var orders = new ObservableCollection<OrderViewModel>(temp.OrderByDescending(d => d.CreateTime));

                                InvokeUI.CheckAccess(() =>
                                {
                                    Current = orders;
                                });

#if DEBUG
                            WriteLog.Info("Updated Orders: " + OrderUpdate.Count() + "/" + temp.Count);
#endif
                            }

                        }
                    }
                }

                if (Core.MainVM.IsSymbolSelected)
                {
                    var time = DateTime.UtcNow;
                    if (!Static.IsInvalidSymbol() && time > (LastRun + TimeSpan.FromMinutes(SERVER_ORDER_DELAY_MINS)) || defer)
                    {
                        LastRun = time;

                        WebCallResult<IEnumerable<BinanceOrderBase>>? webCallResult;

                        if ((webCallResult = Static.CurrentTradingMode switch
                        {
                            TradingMode.Spot =>
                            await Client.Local.Spot.Order.GetOrdersAsync(Static.SelectedSymbolViewModel.SymbolView.Symbol, null, null, null, NUM_ORDERS, receiveWindow: RECV_WINDOW),
                            TradingMode.Margin =>
                            await Client.Local.Margin.Order.GetMarginAccountOrdersAsync(Static.SelectedSymbolViewModel.SymbolView.Symbol, null, null, null, NUM_ORDERS, receiveWindow: RECV_WINDOW),
                            TradingMode.Isolated =>
                            await Client.Local.Margin.Order.GetMarginAccountOrdersAsync(Static.SelectedSymbolViewModel.SymbolView.Symbol, null, null, null, NUM_ORDERS, true, receiveWindow: RECV_WINDOW),
                            _ => null,
                        }) != null && webCallResult.Success)
                        {
                            List<OrderViewModel> OrderUpdate = new();

                            if (webCallResult.Data.Count() > ZERO)
                            {
                                foreach (BinanceOrderBase o in webCallResult.Data)
                                {
                                    OrderUpdate.Add(OrderBase.NewOrderFromServer(o, Static.CurrentTradingMode));
                                }

                                if (defer && Static.ManageStoredOrders.FilteredOrderIds.Count() > ZERO)
                                {
                                    foreach (var order in OrderUpdate)
                                    {
                                        if (!Static.ManageStoredOrders.FilteredOrderIds.Contains(order.OrderId))
                                        {
                                            order.IsOrderHidden = true;
                                        }
                                    }
                                }

                                if (OrderUpdate.Count > ZERO)
                                {
                                    foreach (var order in OrderUpdate)
                                    {
                                        Static.ManageStoredOrders.AddSingleOrderToMemoryStorage(order, false);
                                    }
                                }
                            }
                        }
                    }
                }

                if (Hidden.TriggerUpdate)
                {
                    Hidden.TriggerUpdate = false;
                    RemoveHiddenOrders();
                }
            }
            catch (Exception ex)
            {
                WriteLog.Error(FAILURE_GETTING_ORDERS, ex);
            }
        }

        public void RemoveHiddenOrders()
        {
            lock (MainOrders.OrderUpdateLock)
            {
                List<OrderViewModel> copy = Orders.Current.ToList();
                foreach (var r in copy)
                {
                    InvokeUI.CheckAccess(() =>
                    {
                        r.ScraperStatus = r.ScraperStatus;
                    });

                    if (r.IsOrderHidden)
                    {
                        InvokeUI.CheckAccess(() =>
                        {
                            r.SettleOrderEnabled = false;
                            r.SettleControlsEnabled = false;
                            Orders.Current.Remove(r);
                        });
                    }
                }
            }
        }

        private void ProcessOrder(OrderUpdate update)
        {
            decimal orderPrice;
            if (update.Update.QuoteQuantityFilled != decimal.Zero)
            {
                orderPrice = update.Update.QuoteQuantityFilled / update.Update.QuantityFilled;
            }
            else
            {
                orderPrice = update.Update.Price != decimal.Zero ? update.Update.Price : update.Update.LastPriceFilled;
            }

            OrderViewModel OrderIsCurrentSymbol;

            lock (OrderUpdateLock)
            {
                OrderIsCurrentSymbol = Current.Where(x => x.OrderId == update.Update.OrderId).FirstOrDefault();
            }

            if (OrderIsCurrentSymbol != null)
            {
                OrderIsCurrentSymbol.Quantity = update.Update.Quantity;
                OrderIsCurrentSymbol.QuantityFilled = update.Update.QuantityFilled;
                OrderIsCurrentSymbol.CumulativeQuoteQuantityFilled = update.Update.QuoteQuantityFilled;
                OrderIsCurrentSymbol.Price = orderPrice;
                OrderIsCurrentSymbol.Status = update.Update.Status;
                OrderIsCurrentSymbol.TimeInForce = OrderHelper.TIF(update.Update.TimeInForce.ToString());
                OrderIsCurrentSymbol.Fulfilled = OrderHelper.Fulfilled(update.Update.Quantity, update.Update.QuantityFilled);
                OrderIsCurrentSymbol.CreateTime = update.Update.CreateTime;
                OrderIsCurrentSymbol.UpdateTime = update.Update.UpdateTime;
                Static.ManageStoredOrders.AddSingleOrderToMemoryStorage(OrderIsCurrentSymbol, false);
            }
            else
            {
                Static.ManageStoredOrders.AddSingleOrderToMemoryStorage(OrderBase.NewOrderOnUpdate(update.Update, orderPrice, update.TradingMode), false);
            }
        }

        public Task UpdateTradeFeeAsync()
        {
            if ((MainVM.IsSymbolSelected && Static.HasAuth()))
            {
                _currentTradeFee = TradeFee.GetTradeFee(Static.SelectedSymbolViewModel.SymbolView.Symbol);
                Static.SelectedSymbolViewModel.TradeFee = _currentTradeFee;

                if (_currentTradeFee != null)
                {
                    if (_currentTradeFee.TakerFee == _currentTradeFee.MakerFee)
                    {
                        Static.SelectedSymbolViewModel.TradeFeeString = (_currentTradeFee.TakerFee * ONE_HUNDRED).ToString().Normalize() + PERCENT;

                        WriteLog.Info(MAIN_TRADE_FEES + _currentTradeFee.Symbol + UGH2 + LEFT_BRACKET + _currentTradeFee.TakerFee + RIGHT_BRACKET + UGH4 + (_currentTradeFee.TakerFee * ONE_HUNDRED) + UGH);
                    }
                    else
                    {
                        Static.SelectedSymbolViewModel.TradeFeeString = M + (_currentTradeFee.MakerFee * ONE_HUNDRED).ToString().Normalize() + PERCENTT + (_currentTradeFee.TakerFee * ONE_HUNDRED).ToString().Normalize() + PERCENT;

                        WriteLog.Info(MAIN_TRADE_FEES + _currentTradeFee.Symbol
                            + TAKER + _currentTradeFee.TakerFee + UGH3 + (_currentTradeFee.TakerFee * ONE_HUNDRED)
                            + MAKER + _currentTradeFee.MakerFee + UGH3 + (_currentTradeFee.MakerFee * ONE_HUNDRED) + UGH);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task UpdateInterestRateCurrentSymbolAsync()
        {
            if ((MainVM.IsSymbolSelected && Static.HasAuth()))
            {
                _currentInterestRate = InterestRate.GetDailyInterestRate(Static.SelectedSymbolViewModel.SymbolView.Symbol);
                Static.SelectedSymbolViewModel.InterestRate = _currentInterestRate;
            }

            return Task.CompletedTask;
        }

        public Task UpdatePnlAsync()
        {
            lock (MainOrders.OrderUpdateLock)
            {
                decimal combinedPnl = decimal.Zero;
                decimal combinedTotal = decimal.Zero;
                decimal combinedTotalBase = decimal.Zero;

                foreach (var order in Current)
                {
                    var pnl = decimal.Round(OrderHelper.PnL(order, RealTimeVM.AskPrice, RealTimeVM.BidPrice), App.DEFAULT_ROUNDING_PLACES);
                    var pnlp = OrderHelper.UpdatePnlPercent(order, pnl);

                    InvokeUI.CheckAccess(() =>
                    {
                        order.Pnl = pnl;
                        order.PnlPercent = pnlp;
                    });

                    if (_currentInterestRate > decimal.Zero)
                    {
                        var dailyInterestRate = _currentInterestRate;
                        var interestPerHour = OrderHelper.InterestPerHour(order.QuantityFilled, dailyInterestRate);
                        var interestToDate = OrderHelper.InterestToDate(interestPerHour, InterestTimeStamp(order.CreateTime, order.ResetTime));
                        var interestPerDay = OrderHelper.InterestPerDay(order.QuantityFilled, dailyInterestRate);
                        var interestToDateQuote = OrderHelper.InterestToDateQuote(interestToDate, order.Price);
                        order.InterestPerHour = interestPerHour;
                        order.InterestPerDay = interestPerDay;
                        order.InterestToDate = interestToDate;
                        order.InterestToDateQuote = interestToDateQuote;
                    }

                    combinedTotal += order.CumulativeQuoteQuantityFilled;
                    combinedTotalBase += order.QuantityFilled;
                    combinedPnl += order.Pnl;
                }

                QuoteVM.CombinedPnL = combinedPnl;
                QuoteVM.CombinedTotal = combinedTotal;
                QuoteVM.CombinedTotalBase = combinedTotalBase;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines which Timestamp to use for running Interest Update
        /// </summary>
        /// <param name="CreateTime">Time Order was created</param>
        /// <param name="ResetTime">Time the Reset interest button was clicked</param>
        /// <returns></returns>
        public static long InterestTimeStamp(DateTime CreateTime, DateTime ResetTime)
        {
            if (ResetTime != DateTime.MinValue)
            {
                return ResetTime.Ticks;
            }

            return CreateTime.Ticks;
        }


    }
}
