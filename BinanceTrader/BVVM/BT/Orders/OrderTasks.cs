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
using BinanceAPI.Objects.Spot.MarginData;
using BinanceAPI.Objects.Spot.MarketData;
using BinanceAPI.Objects.Spot.SpotData;
using BTNET.BV.Base;
using BTNET.BV.Enum;
using BTNET.BVVM.Helpers;
using BTNET.BVVM.Log;
using BTNET.VM.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BTNET.BVVM.BT.Orders
{
    internal class InternalOrderTasks : Core
    {
        private const int DELAY_MS = 1;
        private const int EXPIRE_TIME = 3000;

        private const string S = " | ";
        private const string TRACE = "| Trace: ";
        private const string OUTER = "SettleQuote: OUTER: ";
        private const string INNER = "| INNER: ";
        private const string SELECT_SYMBOL_AGAIN = "Select Symbol Again";
        private const string TRADING_MODE_EXPECTED = "Trading Mode was Expected: ";
        private const string ZERO = "SettleWidget: Settle Amount was Zero";
        private const string BORROW_ZERO = "Borrow Was Zero";
        private const string FAILED_TO_PLACE = "Failed to place order";

        private const string SETTLED = "Settled: ";
        private const string SUCCESS = "Repay was Successful, " + SETTLED;
        private const string REQUIRED_VALUE_ZERO = "Settle Failed: A required value was zero";
        private const string DEUG_SETTLE = "DEBUG SETTLE: Asset :";

        private const string FREE_AMOUNT = " | freeAmount :";
        private const string BORROW_AMOUNT = " | borrowedAmount :";
        private const string SYMBOL = " | Symbol :";
        private const string ISOLATED = "| Isolated: ";

        private const string EXCHANGE_INFO_MISSING = "Exchange Information was empty, this shouldn't happen.\n\nYour order was NOT settled.\n\nIf you did something specific please report it";
        private const string RESTART_CLIENT = "Please restart client";

        private static int lastInt = int.MinValue;
        
        /// <summary>
        /// Next Order tracking Int
        /// </summary>
        /// <returns></returns>
        private static int NextTrackInt()
        {
            if (lastInt == int.MaxValue)
            {
                lastInt = int.MinValue;
            }

            return lastInt++;
        }

        /// <summary>
        /// Creates a discardable <see cref="Task"/> and Settles Requested Asset
        /// <para>Task will be awaited if you use the <see cref="Task{TResult}"/></para>
        /// </summary>
        /// <param name="freeAmount">The current free amount of Asset</param>
        /// <param name="borrowedAmount">The current borrowed amount of Asset</param>
        /// <param name="Asset">The Asset</param>
        /// <param name="Symbol">The Symbol for the Asset</param>
        /// <returns>Boolean indicating success or failure</returns>
        public static async Task<bool> SettleAssetAsync(decimal freeAmount, decimal borrowedAmount, string Asset, string Symbol, bool isolated)
        {
#if DEBUG || DEBUG_SLOW

            WriteLog.Info(
                "TEST SETTLE: Asset :" + Asset +
                " | freeAmount :" + freeAmount +
                " | borrowedAmount :" + borrowedAmount +
                " | Symbol :" + Symbol +
                " | isolated :" + isolated
            );

            NotifyVM.Notification("Test Settle - Check Logs", Static.Green);

            await Task.CompletedTask;
            return true;
#else

            bool SettleBool = await Task.Run(() =>
            {
                try
                {
                    if (freeAmount == 0 || borrowedAmount == 0)
                    {
                        WriteLog.Error(REQUIRED_VALUE_ZERO);
                        App.OrderTaskFailed?.Invoke(REQUIRED_VALUE_ZERO, null);
                        return false;
                    }

                    var resultQ = Client.Local.Margin.RepayAsync(Asset, freeAmount, isolated, Symbol, 2000);

                    if (resultQ.Result.Success)
                    {
                        WriteLog.Info(SUCCESS + Asset + S + resultQ.Result.Data.TransactionId + ISOLATED + isolated);
                        NotifyVM.Notification(SETTLED + Asset + S + resultQ.Result.Data.TransactionId);
                        return true;
                    }
                    else
                    {
                        WriteLog.Error(resultQ.Result.Error.Message + ISOLATED + isolated);
                        NotifyVM.Notification(resultQ.Result.Error.Message);

                        WriteLog.Info(DEUG_SETTLE + Asset + FREE_AMOUNT + freeAmount + BORROW_AMOUNT + borrowedAmount + SYMBOL + Symbol + ISOLATED + isolated);
                        App.OrderTaskFailed?.Invoke(resultQ.Result.Error.Message + ISOLATED + isolated, null);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    App.OrderTaskFailed?.Invoke(OUTER + ex.Message + INNER + ex.InnerException + TRACE + ex.StackTrace, null);
                    WriteLog.Error(OUTER + ex.Message + INNER + ex.InnerException + TRACE + ex.StackTrace);
                    return false;
                }
            }).ConfigureAwait(false);

            return SettleBool;
#endif
        }

        /// <summary>
        /// Available amount of asset
        /// </summary>
        /// <typeparam name="T">Accepts collection of Assets that must contain IBinanceBalance</typeparam>
        /// <param name="asset">Asset to return the Available amount for</param>
        /// <param name="assetCollection">Collection of Assets</param>
        /// <returns></returns>
        private static decimal AssetFreeAmount(string asset, ObservableCollection<BinanceBalance> assetCollection)
        {
            var s = assetCollection.SingleOrDefault(p => p.Asset == asset);
            if (s != null)
            {
                return s.Available;
            }

            return 0;
        }

        /// <summary>
        /// Available amount of asset
        /// </summary>
        /// <typeparam name="T">Accepts collection of Assets that must contain IBinanceBalance</typeparam>
        /// <param name="asset">Asset to return the Available amount for</param>
        /// <param name="assetCollection">Collection of Assets</param>
        /// <returns></returns>
        private static decimal AssetFreeAmount(string asset, ObservableCollection<BinanceMarginBalance> assetCollection)
        {
            var s = assetCollection.SingleOrDefault(p => p.Asset == asset);
            if (s != null)
            {
                return s.Available;
            }

            return 0;
        }

        /// <summary>
        /// Current Available Amount based on selected TradingMode
        /// </summary>
        /// <param name="asset"></param>
        /// <returns>Current Available Free Amount of provided Asset based on TradingMode</returns>
        private static decimal CurrentAvailableAmount(BinanceSymbol asset, TradingMode tradingMode)
        {
            switch (tradingMode)
            {
                case TradingMode.Spot:
                    lock (Assets.SpotAssetLock)
                    {
                        return AssetFreeAmount(asset.BaseAsset, Assets.SpotAssets);
                    }

                case TradingMode.Margin:
                    lock (Assets.MarginAssetLock)
                    {
                        return AssetFreeAmount(asset.BaseAsset, Assets.MarginAssets);
                    }

                case TradingMode.Isolated:
                    lock (Assets.IsolatedAssetLock)
                    {
                        return Assets.IsolatedAssets.FirstOrDefault(x => x.Symbol == asset.Name).BaseAsset.Available;
                    }
                default:
                    Prompt.ShowBox(TRADING_MODE_EXPECTED, SELECT_SYMBOL_AGAIN);
                    WriteLog.Error(TRADING_MODE_EXPECTED + asset.BaseAsset);
                    MainContext.ResetSymbol();
                    return 0;
            }
        }

        /// <summary>
        /// Process Automatic Order / Settle via User Interface
        /// </summary>
        /// <param name="previousOrder">The Order</param>
        /// <param name="orderSide">The OrderSide</param>
        /// <param name="borrow">true if the newly placed order should borrow where available</param>
        /// <param name="quantitypadding">Additional quantity to be added to the order (default 0)</param>
        /// <param name="settle"></param>
        /// <param name="track">track the result of the order from the queue</param>
        public static void ProcessOrder(OrderViewModel previousOrder, OrderSide orderSide, bool borrow, bool settle = true, decimal quantitypadding = 0, bool track = false)
        {
#if DEBUG
            WriteLog.Info("Symbol: " + previousOrder.Symbol + " | Filled: " + previousOrder.QuantityFilled + " | OrderPadding: " + quantitypadding + " | Price: " + previousOrder.Price + " | Mode: " + previousOrder.OrderTradingMode + " | Borrow: " + borrow + " | Side: " + orderSide);
#endif

            _ = Task.Run(async () =>
            {
                var symbolInfo = StoredExchangeInfo.Get().Symbols.SingleOrDefault(r => r.Name == previousOrder.Symbol);

                if (symbolInfo == null)
                {
                    WriteLog.Error(EXCHANGE_INFO_MISSING);
                    Prompt.ShowBox(EXCHANGE_INFO_MISSING, RESTART_CLIENT);
                    return;
                }

                decimal settleAmount = 0;
                decimal amountBeforeOrder = CurrentAvailableAmount(symbolInfo, previousOrder.OrderTradingMode);

#if DEBUG
                WriteLog.Info("Settle Amount Before Order: " + amountBeforeOrder);
#endif

                var newOrderQuantity = previousOrder.QuantityFilled + quantitypadding;

                int trackId = track ? NextTrackInt() : 0;

                // Add to Queue
                TradeVM.TradeRunner.AddToQueue(previousOrder.Symbol, newOrderQuantity, 0, previousOrder.OrderTradingMode, borrow, orderSide, false, trackId);

                bool success = false;

                // Wait for Order Queue
                if (trackId != 0)
                {
                    var startTimeQueue = DateTime.UtcNow.Ticks;
                    while (await Loop.Delay(startTimeQueue, DELAY_MS, EXPIRE_TIME).ConfigureAwait(false))
                    {
                        var o = OrderTracker.GetOrder(trackId);
                        if (o != default)
                        {
                            if (o.Success)
                            {
                                decimal pnl = 0;

                                if (o.Order.Side == OrderSide.Sell)
                                {
                                    pnl = (o.Order.QuoteQuantityFilled != 0 ? o.Order.QuoteQuantityFilled / o.Order.QuantityFilled * o.Order.QuantityFilled : o.Order.Price * o.Order.QuantityFilled) - previousOrder.Total;
                                }
                                else
                                {
                                    pnl = previousOrder.Total - (o.Order.QuoteQuantityFilled != 0 ? o.Order.QuoteQuantityFilled / o.Order.QuantityFilled * o.Order.QuantityFilled : o.Order.Price * o.Order.QuantityFilled);
                                }

                                InvokeUI.CheckAccess(() =>
                                {
                                    QuoteVM.RunningTotalTask += pnl;
                                });

                                success = true;
                            }

                            OrderTracker.Remove(o);
                            break;
                        }
                    }
                }

                if (!success)
                {
                    App.OrderTaskFailed?.Invoke(FAILED_TO_PLACE, previousOrder);
                    WriteLog.Error(FAILED_TO_PLACE);
                    return;
                }

                if (!settle)
                {
                    return;
                }

                // Wait for Account update
                var startTime = DateTime.UtcNow.Ticks;
                while (await Loop.Delay(startTime, DELAY_MS, EXPIRE_TIME).ConfigureAwait(false))
                {
                    settleAmount = CurrentAvailableAmount(symbolInfo, previousOrder.OrderTradingMode);
                    if (settleAmount != amountBeforeOrder)
                    {
                        break;
                    }
                }

#if DEBUG
                WriteLog.Info("Settle Amount After Order: " + settleAmount);
#endif

                if (amountBeforeOrder + newOrderQuantity == settleAmount)
                {
#if DEBUG
                    WriteLog.Info("Defer Loop");
#endif
                    settleAmount = await DeferAsync(startTime, settleAmount, previousOrder.OrderTradingMode, symbolInfo).ConfigureAwait(false);
                }

#if DEBUG
                WriteLog.Info("Settle Amount After Defer: " + settleAmount);
#endif

                if (settleAmount != 0)
                {
                    decimal borrowed = 0;

                    if (previousOrder.OrderTradingMode == TradingMode.Margin)
                    {
                        lock (Assets.MarginAssetLock)
                        {
                            borrowed = Assets.MarginAssets.Where(t => t.Asset == symbolInfo.BaseAsset).FirstOrDefault().Borrowed;
                        }
                    }
                    else
                    {
                        lock (Assets.IsolatedAssetLock)
                        {
                            borrowed = Assets.IsolatedAssets.Where(t => t.Symbol == previousOrder.Symbol).FirstOrDefault().BaseAsset.Borrowed;
                        }
                    }

#if DEBUG
                    WriteLog.Info("Borrow Amount Before Settle: " + borrowed);
#endif

                    if (borrowed != 0)
                    {
                        await SettleAssetAsync(settleAmount, borrowed, symbolInfo.BaseAsset, symbolInfo.Name, previousOrder.OrderTradingMode == TradingMode.Isolated).ConfigureAwait(false);
                    }
                    else
                    {
                        App.OrderTaskFailed?.Invoke(BORROW_ZERO, previousOrder);
                        WriteLog.Error(BORROW_ZERO);
                    }
                }
                else
                {
                    App.OrderTaskFailed?.Invoke(ZERO, previousOrder);
                    WriteLog.Info(ZERO);
                }
            }).ConfigureAwait(false);
        }

        private static async Task<decimal> DeferAsync(long startTime, decimal amountBeforeDefer, TradingMode tradingMode, BinanceSymbol baseAsset)
        {
            var deferAmount = amountBeforeDefer;
            while (await Loop.Delay(startTime, DELAY_MS, EXPIRE_TIME).ConfigureAwait(false))
            {
                deferAmount = CurrentAvailableAmount(baseAsset, tradingMode);
                if (amountBeforeDefer != deferAmount)
                {
                    break;
                }
            }

            return deferAmount;
        }
    }
}
