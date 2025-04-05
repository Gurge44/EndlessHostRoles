// Copyright(c) 2015 vpmedia
// Released under the MIT license

// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;

namespace EHR;

public class MersenneTwister : IRandom
{
    // 参考元
    public const string REFERENCE_HOMEPAGE = "http://www.math.sci.hiroshima-u.ac.jp/m-mat/MT/mt.html";
    public const string REFERENCE_SOURCE_CODE = "https://github.com/vpmedia/template-unity/blob/master/Framework/Assets/Frameworks/URandom/MersenneTwister.cs";

    /// <summary>
    ///     数値の上限を設定
    ///     これより下の値の一部は参考元のソースより拝借
    /// </summary>
    private const int N = 624;

    private const int M = 397;
    private const uint MatrixA = 0x9908b0df;
    private const uint UpperMask = 0x80000000;
    private const uint LowerMask = 0x7fffffff;
    private const uint TemperingMaskB = 0x9d2c5680;
    private const uint TemperingMaskC = 0xefc60000;
    private readonly uint[] _mag01 = [0x0, MatrixA];

    private readonly uint[] _mt = new uint[N];
    private short _mtItems;

    public MersenneTwister() : this((int)DateTime.UtcNow.Ticks) { }

    public MersenneTwister(int seed)
    {
        Init((uint)seed);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue < 0 || maxValue < 0) throw new ArgumentOutOfRangeException("minValue and maxValue must be bigger than 0.");

        if (minValue > maxValue) throw new ArgumentException("maxValue must be bigger than minValue.");

        if (minValue == maxValue) return minValue;

        return (int)(minValue + (Next() % (maxValue - minValue)));
    }

    public int Next(int maxValue)
    {
        return Next(0, maxValue);
    }

    private static uint ShiftU(uint y)
    {
        return y >> 11;
    }

    private static uint ShiftS(uint y)
    {
        return y << 7;
    }

    private static uint ShiftT(uint y)
    {
        return y << 15;
    }

    private static uint ShiftL(uint y)
    {
        return y >> 18;
    }

    private void Init(uint seed)
    {
        _mt[0] = seed & 0xffffffffU;

        for (_mtItems = 1; _mtItems < N; _mtItems++)
        {
            _mt[_mtItems] = (uint)((1812433253U * (_mt[_mtItems - 1] ^ (_mt[_mtItems - 1] >> 30))) + _mtItems);
            _mt[_mtItems] &= 0xffffffffU;
        }
    }

    public uint Next()
    {
        uint y;

        /* _mag01[x] = x * MatrixA  for x=0,1 */
        if (_mtItems >= N) /* generate N words at one time */
        {
            short kk = 0;

            for (; kk < N - M; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            for (; kk < N - 1; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
            _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ _mag01[y & 0x1];

            _mtItems = 0;
        }

        y = _mt[_mtItems++];
        y ^= ShiftU(y);
        y ^= ShiftS(y) & TemperingMaskB;
        y ^= ShiftT(y) & TemperingMaskC;
        y ^= ShiftL(y);

        return y;
    }
}