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
using BTNET.BVVM.BT;

namespace BTNET.VM.ViewModels
{
    public partial class ScraperViewModel
    {
        private const string NO_BASIS = "There is no order to base the new order on";
        private const string TICKER_FAIL = "Scarper ticker failed to Start?";
        private const string FAILED_START = "Failed to start Scraper";
        private const string FAILED_START_PRELAUNCH = "Failed to start Scraper, Order Didn't meet criteria to start";
        private const string SWITCH_ERROR = "You need a Sell and Buy order to Switch";
        private const string STATUS = " |Status: ";
        private const string PRICE = " |Price: ";
        private const string QUANTITY = " |Quantity: ";
        private const string QUANTITY_FILLED = " |QuantityF: ";
        private const string OTHER_ORDER = "|OtherOrder: ";
        private const string CQCF = " |CQuantityF: ";
        private const string WAITING_BUY = "Waiting to buy";
        private const string NO_LIMIT_ADD = "You can't add with a Limit Order as the first order unless its filled";
        private const string NO_LIMIT_SWITCH = "Tried to switch to a Limit order that wasn't filled, Stopping Scraper";
        private const string EXCEPTION_ADDING = "Exception while Adding, Check logs";
        private const string EXCEPTION_STARTING = "Exception while starting Scraper, Check logs";
        private const string SWITCHING_NO_ORDER = "No Order to Switch";
        private const string SWITCHING = "Switching";
        private const string ORDER_WAS_COMPLETE = "Switch Order already Completed..";
        private const string ORDER_MISMATCH = "The first order is not the buy order in the scraper";
        private const string NO_ORDER_ERROR = "There are no orders to use with the scraper";
        private const string NO_LIMIT_START = "You can't start the Scraper with a Limit Order";
        private const string ORDER_PLACED = "Order Placed:";
        private const string AFTER_BUY = "AfterBuy: Id:";
        private const string AFTER_SELL = "AfterSell: Id:";
        private const string TYPE = " | Type: ";
        private const string SOLD = "Sold: [";
        private const string BAR = "|";
        private const string RIGHT_BRACKET = "]";
        private const string LEFT_BRACKET = "[";
        private const string SLASH = "/";
        private const string PLUS = "+";
        private const string EXCEPTION = "Stopped because of an Exception";
        private const string EMPTY = "";
        private const string NEW_LINE = "\n";
        private const string NEW_LOW = "NL: ";
        private const string NEW_HIGH = "NH: ";
        private const string WAITING_TO_BUY = "Waiting to buy..";
        private const string STARTED = "Started";
        private const string STOPPED = "Stopped";
        private const string SCRAPER_DIED = "Dead x_x";
        private const string WATCHING = "Watching..";
        private const string WAITING = "Waiting..";
        private const string WAITING_TWO = "Waiting";
        private const string GUESS_SELL = "Guess Sell..";
        private const string GUESS_BUY = "Guess Buy..";
        private const string SELL_PROCESSED = "Sell Processed: ";
        private const string BUY_PROCESSED = "Buy Processed";
        private const string BLOCKED_SELL = "Blocked Sell -> Busy";
        private const string BLOCKED_BUY = "Blocked Buy -> Busy";
        private const string FAILED_FOK_BUY_WAIT = "Buy Killed -> Wait Mode";
        private const string FAILED_FOK_BUY_WATCH = "Buy Killed -> Watch Mode";
        private const string FAILED_FOK_SELL_WATCH = "Sell Killed -> Watch Mode";
        private const string FAILED_MARKET_BUY_WAIT = "Buy Failed -> Wait Mode";
        private const string FAILED_MARKET_BUY_WATCH = "Buy Failed -> Watch Mode";
        private const string FAILED_MARKET_SELL_WATCH = "Sell Failed -> Watch Mode";
        private const string NEXT_U = "Up ";
        private const string NEXT_D = "Down ";
        private const string TIME_ELAPSED = "Time Elapsed";
        private const string WAIT_COUNT_ELAPSED = "Wait Count Elapsed";
        private const string PRICE_BIAS = "Bias: ";
        private const string NONE = "None";
        private const string BEARISH = "Bearish";
        private const string LOGTEXT = "LogText";
        private const string STOPPED_REQUEST = "Scraper stopped at user request";
        private const string STOPPED_DIED = "Scraper Died";
        private const string USER_ADDED = "User Added";

        private const int MAX_WAIT_TIME = 800;
        private const int WAIT_DELTA = 5;
        private const int WAIT_MIN = 40;
        private const int FIVE_HUNDRED = 500;
        private const int TWO = 2;
        private const int ONE = 1;
        private const int ZERO = 0;
        private const int GUESSER_HIGH_COUNT_MAX = 17500;
        private const int GUESSER_LOW_COUNT_MAX = 12500;
        private const int GUESSER_LOW_HIGH_BIAS = 5;
        private const int GUESSER_START_MIN_MS = 20;
        private const int ONE_HUNDRED = 100;

        private const decimal MINIMUM_STEP = 0.001m;
        private const decimal ONE_HUNDRED_PERCENT = ONE_HUNDRED;

        protected private static decimal UpdateReversePnLPercent(OrderViewModel sell, decimal pnl)
        {
            if (pnl == ZERO)
            {
                return ZERO;
            }

            return (pnl / ((sell.CumulativeQuoteQuantityFilled / sell.QuantityFilled) * sell.QuantityFilled)) * ONE_HUNDRED;
        }

        protected private static bool CalculateReverseInternal(OrderViewModel sell, decimal reverseDown, bool reverse)
        {
            var r = UpdateReversePnLPercent(sell, sell.Pnl);
            if (r > reverseDown)
            {
                return true;
            }

            if (reverse && r < -reverseDown)
            {
                return true;
            }

            return false;
        }

        protected private static decimal UpdateCurrentPnlPercentInternal(OrderViewModel workingBuy, out decimal pnl)
        {
            pnl = workingBuy.Pnl;
            return UpdatePnlPercent(workingBuy, workingBuy.Pnl);
        }

        protected private static decimal UpdatePnlPercent(OrderViewModel order, decimal pnl)
        {
            decimal total = ZERO;
            if (order.CumulativeQuoteQuantityFilled != ZERO)
            {
                total = (order.CumulativeQuoteQuantityFilled / order.QuantityFilled) * order.QuantityFilled;
            }
            else
            {
                total = order.Price * order.QuantityFilled;
            }

            decimal currentPnlPercent = ZERO;
            if (pnl != ZERO && total != ZERO)
            {
                currentPnlPercent = (pnl / total) * ONE_HUNDRED;
            }

            return currentPnlPercent;
        }

        protected private static bool NextBias(decimal price, decimal nextDown, decimal nextUp, decimal bias, Bias directionBias, out bool nextBias)
        {
            if (price != ZERO && nextDown != ZERO && nextUp != ZERO)
            {
                if (nextDown != -bias)
                {
                    bool b = directionBias == Bias.None || directionBias == Bias.Bearish;
                    if (price < nextDown && b)
                    {
                        nextBias = true;
                        return false;
                    }
                }

                if (nextUp != bias)
                {
                    bool b = directionBias == Bias.None || directionBias == Bias.Bullish;
                    if (price > nextUp && b)
                    {
                        nextBias = true;
                        return true;
                    }
                }
            }

            nextBias = false;
            return false;
        }

        protected private static bool? CountLowHigh(ScraperCounter counter)
        {
            if (counter.GuesserBias < GUESSER_LOW_HIGH_BIAS)
            {
                if (counter.GuessNewLowCount > GUESSER_LOW_COUNT_MAX || counter.GuessNewLowCountTwo > GUESSER_LOW_COUNT_MAX)
                {
                    return true;
                }
            }
            else
            {
                if (counter.GuessNewLowCount > GUESSER_HIGH_COUNT_MAX || counter.GuessNewLowCountTwo > GUESSER_HIGH_COUNT_MAX)
                {
                    return false;
                }
            }

            return null;
        }
    }
}