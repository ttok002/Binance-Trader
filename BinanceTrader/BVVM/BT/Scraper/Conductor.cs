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
using PrecisionTiming;

namespace BTNET.BVVM.BT
{
    public class Conductor
    {
        protected internal volatile PrecisionTimer WaitingTimer = new PrecisionTimer();
        protected internal volatile PrecisionTimer WatchingGuesserTimer = new PrecisionTimer();
        protected internal volatile PrecisionTimer WaitingGuesserTimer = new PrecisionTimer();

        PrecisionTimer ConductorTimer { get; set; }

        public ConductorStatus CurrentStatus { get; set; }

        public Conductor()
        {
            ConductorTimer = new PrecisionTimer();
            CurrentStatus = ConductorStatus.Idle;

            ConductorTimer.SetInterval(() =>
            {
                RunConductor();
            }, 1, true, true, 1);
        }

        public void ChangeStatus(ConductorStatus status)
        {
            //WriteLog.Info("Changed Status: " + status.ToString());
            CurrentStatus = status;
        }

        public void RunConductor()
        {
            switch (CurrentStatus)
            {
                case ConductorStatus.Watching or ConductorStatus.Interupt:
                    WaitingTimer.StopSilent();
                    WaitingGuesserTimer.StopSilent();
                    WatchingGuesserTimer.StopSilent();
                    break;
                case ConductorStatus.Waiting:
                    WaitingTimer.StartSilent();
                    WaitingGuesserTimer.StopSilent();
                    WatchingGuesserTimer.StopSilent();
                    break;
                case ConductorStatus.GuessingWait:
                    WaitingGuesserTimer.StartSilent();
                    WaitingTimer.StopSilent();
                    WatchingGuesserTimer.StopSilent();
                    break;
                case ConductorStatus.GuessingWatch:
                    WatchingGuesserTimer.StartSilent();
                    WaitingGuesserTimer.StopSilent();
                    WaitingTimer.StopSilent();
                    break;
            }
        }

        public void InteruptWaiting()
        {
            CurrentStatus = ConductorStatus.Interupt;
            WaitingTimer.StopSilent();
        }
    }
}
