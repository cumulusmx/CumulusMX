using System;
using System.Collections.Generic;
using System.Text;

namespace TestStation
{
    public class MeanRevertingRandomWalk
    {
        private readonly Func<DateTime, double> _meanCurve;
        private readonly Func<DateTime, double> _volatility;
        private readonly double _meanReversion;
        private readonly double _cropMin;
        private readonly double _cropMax;

        private double _value;
        private bool _initialised = false;
        private readonly Random _random;

        public MeanRevertingRandomWalk(Func<DateTime,double> meanCurve,Func<DateTime,double> volatility,double meanReversion,double cropMin, double cropMax)
        {
            _meanCurve = meanCurve;
            _volatility = volatility;
            _meanReversion = meanReversion;
            _cropMin = cropMin;
            _cropMax = cropMax;
            _random =  new Random();
        }

        public double GetValue(DateTime date)
        {
            if (!_initialised)
            {
                _value = _meanCurve(date);
                _initialised = true;
            }


            _value -= (_value - _meanCurve(date)) * _meanReversion;
            _value += _volatility(date) * (2 * _random.NextDouble() - 1);
            if (_value < _cropMin) return _cropMin;
            if (_value > _cropMax) return _cropMax;
            return _value;
        }
    }
}
