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

using BTNET.BV.Enum;
using BTNET.BVVM;
using BTNET.VM.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TJson.NET;

namespace BTNET.BV.Base
{
    public class StoredOrdersSymbol : Core
    {
        private static object _lock = new object();

        public List<OrderViewModel> BaseOrders { get; set; } = new List<OrderViewModel>();
        public TradingMode TradingMode { get; set; } = TradingMode.Error;
        public string Symbol { get; set; } = string.Empty;

        public StoredOrdersSymbol(string symbol, TradingMode tradingMode)
        {
            Symbol = symbol;
            TradingMode = tradingMode;
        }

        public string Path => App.OrderPath + "\\" + TradingMode + "\\" + Symbol;

        public bool Save()
        {
            var orderPath = Path + "\\StoredOrders.json";
            Directory.CreateDirectory(Path);

            lock (_lock)
            {
                return Json.Save(BaseOrders, orderPath);
            }
        }

        public void AddBulk(IEnumerable<OrderViewModel> o, TradingMode tradingMode)
        {
            lock (_lock)
            {
                foreach (OrderViewModel order in o)
                {
                    order.OrderTradingMode = tradingMode;
                    order.Total = order.CumulativeQuoteQuantityFilled;
                    BaseOrders.Add(order);
                }
            }
        }

        public OrderViewModel AddOrder(OrderViewModel order, bool canUpdateHide)
        {
            var existingOrder = BaseOrders.Where(t => t.OrderId == order.OrderId).FirstOrDefault();
            if (existingOrder != null)
            {
                existingOrder.Price = order.Price;
                existingOrder.Quantity = order.Quantity;
                existingOrder.Type = order.Type;

                if (existingOrder.QuantityFilled <= order.QuantityFilled)
                {
                    existingOrder.CumulativeQuoteQuantityFilled = order.CumulativeQuoteQuantityFilled;
                    existingOrder.QuantityFilled = order.QuantityFilled;
                    existingOrder.Fulfilled = order.Fulfilled;
                    existingOrder.UpdateTime = order.UpdateTime;
                    existingOrder.Status = order.Status;
                    existingOrder.Fee = order.Fee;
                }

                existingOrder.Pnl = order.Pnl;
                existingOrder.CreateTime = order.CreateTime;
                existingOrder.MinPos = order.MinPos;
                existingOrder.InterestToDate = order.InterestToDate;
                existingOrder.InterestToDateQuote = order.InterestToDateQuote;
                existingOrder.InterestPerHour = order.InterestPerHour;
                existingOrder.InterestPerDay = order.InterestPerDay;
                existingOrder.ResetTime = order.ResetTime;
                existingOrder.IsMaker = order.IsMaker;

                if (canUpdateHide)
                {
                    existingOrder.IsOrderHidden = order.IsOrderHidden;
                }

                return existingOrder;
            }
            else
            {
                BaseOrders.Add(order);
            }

            return order;
        }

        public OrderViewModel AddOrderScraper(OrderViewModel order)
        {
            var existingOrder = BaseOrders.Where(t => t.OrderId == order.OrderId).FirstOrDefault();
            if (existingOrder != null)
            {
                existingOrder.Type = order.Type;
                existingOrder.Price = order.Price;
                existingOrder.Quantity = order.Quantity;
                existingOrder.CumulativeQuoteQuantityFilled = order.CumulativeQuoteQuantityFilled;
                existingOrder.QuantityFilled = order.QuantityFilled;
                existingOrder.Fulfilled = order.Fulfilled;
                existingOrder.Status = order.Status;
                existingOrder.UpdateTime = order.UpdateTime;
                existingOrder.Pnl = order.Pnl;
                existingOrder.CreateTime = order.CreateTime;
                existingOrder.PurchasedByScraper = order.PurchasedByScraper;
                existingOrder.ScraperStatus = order.ScraperStatus;
                existingOrder.IsOrderHidden = order.IsOrderHidden;
                existingOrder.OrderTradingMode = order.OrderTradingMode;
                existingOrder.IsOrderCompleted = order.IsOrderCompleted;
            }
            else
            {
                BaseOrders.Add(order);
            }

            return order;
        }
    }
}
