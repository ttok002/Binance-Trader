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

namespace BTNET.BVVM.BT
{
    public class CompareRandom
    {
        private const decimal DIFFERENCE = 1.7m;

        public decimal a;
        public decimal b;
        public decimal c;
        public decimal d;

        public bool Compare(decimal pnl)
        {
            this.d = this.c;
            this.c = this.b;
            this.b = this.a;
            this.a = pnl;

            if (b != decimal.Zero && b / DIFFERENCE > a)
            {
                return true;
            }

            if (c != decimal.Zero && c / DIFFERENCE > a)
            {

                return true;
            }

            if (d != decimal.Zero && d / DIFFERENCE > a)
            {
                return true;
            }

            return false;
        }

        public void Clear()
        {
            this.a = decimal.Zero;
            this.b = decimal.Zero;
            this.c = decimal.Zero;
            this.d = decimal.Zero;
        }
    }
}
