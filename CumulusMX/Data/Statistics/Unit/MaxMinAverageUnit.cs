using System;
using CumulusMX.Extensions.Station;
using Newtonsoft.Json;
using UnitsNet;
using Unosquare.Swan.Formatters;

namespace CumulusMX.Data.Statistics.Unit
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MaxMinAverageUnit<TBase, TUnitType> : IRecordsAndAverage<TBase>
        where TUnitType : Enum where TBase : IQuantity<TUnitType>
    {
        [JsonProperty]
        private int _count = 0;
        [JsonProperty]
        private int _nonZero = 0;
        [JsonProperty]
        private TBase _minimum;
        [JsonProperty]
        private TBase _maximum;

        public TBase Minimum
        {
            get => _count == 0 ? _zeroQuantity : _minimum;
            private set => _minimum = value;
        }

        public TBase Maximum
        {
            get => _count == 0 ? _zeroQuantity : _maximum;
            private set => _maximum = value;
        }

        [JsonProperty]
        public DateTime MinimumTime { get; private set; }
        [JsonProperty]
        public DateTime MaximumTime { get; private set; }

        public TBase Average
        {
            get
            {
                if (_count == 0)
                    return _zeroQuantity;
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
        [JsonProperty]
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
            MaximumTime = DateTime.Now;
            MinimumTime = DateTime.Now;
        }

        public void AddValue(DateTime newTime, object inValue)
        {
            if (!(inValue is TBase newValue))
            {
                //TODO: Log
                throw new ArgumentException("Invalid input type.", "inSample");
            }

            _count++;
            _total += newValue.As(_itemOne);
            if ((newValue as IComparable).CompareTo(Maximum) > 0 || _count == 1)
            {
                Maximum = newValue;
                MaximumTime = newTime;
            }
            if ((newValue as IComparable).CompareTo(Minimum) < 0 || _count == 1)
            {
                Minimum = newValue;
                MinimumTime = newTime;
            }
            _averageValid = false;
            _unitTotalValid = false;
            if ((newValue as IComparable).CompareTo(_zeroQuantity) != 0)
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