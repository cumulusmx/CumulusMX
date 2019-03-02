using System;
using System.Dynamic;
using Autofac.Core.Activators.Reflection;
using UnitsNet;

namespace CumulusMX.Data
{
    internal class MaxMinAverageUnit<TBase, TUnitType>
        where TUnitType : Enum where TBase : IComparable, IQuantity<TUnitType>
    {
        private int _count = 0;
        private int _nonZero = 0;
        private TBase _minimum;
        private TBase _maximum;

        public TBase Minimum
        {
            get
            {
                if (_count == 0)
                    throw new InvalidOperationException("An empty set has no minimum.");
                return _minimum;
            }
            private set
            {
                _minimum = value;
            }

        }

        public TBase Maximum
        {
            get
            {
                if (_count == 0)
                    throw new InvalidOperationException("An empty set has no maximum.");
                return _maximum;
            }
            private set
            {
                _maximum = value;
            }

        }

        public TBase Average
        {
            get
            {
                if (_count == 0) 
                    throw new DivideByZeroException("Cannot get the average of 0 values.");
                if (!_averageValid) CalculateAverage();
                return _average;
            }
        }

        public TBase Total
        {
            get
            {
                if (!_unitTotalValid) CreateTotal();
                return _unitTotal;
            }
        }

        public int NonZero => _nonZero;

        private TBase _average;
        private bool _averageValid = false;
        private double _total;
        private TBase _unitTotal;
        private bool _unitTotalValid = false;

        private readonly TUnitType _itemOne;
        private readonly TBase _zeroQuantity;

        public MaxMinAverageUnit()
        {
            _itemOne = (TUnitType) Enum.ToObject(typeof(TUnitType), 1);
            _zeroQuantity = (TBase)Activator.CreateInstance(typeof(TBase), 0, _itemOne);
            Reset();
        }

        public void Reset()
        {
            _count = 0;
            _total = 0;
            _averageValid = false;
            _unitTotal = _zeroQuantity;
            _unitTotalValid = true;
            _nonZero = 0;
        }

        public void AddValue(TBase newValue)
        {
            _count++;
            _total += newValue.As(_itemOne);
            if (newValue.CompareTo(Maximum) > 0 || _count == 1) Maximum = newValue;
            if (newValue.CompareTo(Minimum) < 0 || _count == 1) Minimum = newValue;
            _averageValid = false;
            _unitTotalValid = false;
            if (newValue.CompareTo(_zeroQuantity) != 0)
                _nonZero++;
        }

        private void CalculateAverage()
        {
            double avgValue = _total / _count;
            _average = (TBase) Activator.CreateInstance(typeof(TBase), avgValue, _itemOne);
            _averageValid = true;
        }

        private void CreateTotal()
        {
            _unitTotal = (TBase)Activator.CreateInstance(typeof(TBase), _total, _itemOne);
            _unitTotalValid = true;
        }
    }
}