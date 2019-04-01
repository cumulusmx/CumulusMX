using System;
using UnitsNet;

namespace CumulusMX.Data.Statistics.Unit
{
    internal class DayStatisticUnit<TBase, TUnitType>
        where TUnitType : Enum where TBase : IComparable, IQuantity<TUnitType>
    {
        public TBase HighestMinimum { get; private set; }

        public TBase LowestMaximum { get; private set; }

        public DateTime LowestMaximumDay { get; private set; }
        public DateTime HighestMinimumDay { get; private set; }

        public TBase HighestTotal => _total.Maximum;
        public TBase LowestTotal => _total.Minimum;
        public DateTime HighestTotalDay => _total.MaximumTime;
        public DateTime LowestTotalDay => _total.MinimumTime;

        private readonly MaxMinAverageUnit<TBase,TUnitType> _range;
        private readonly MaxMinAverageUnit<TBase,TUnitType> _total;
        private readonly TBase _zeroQuantity;
        private int _count;
        private TUnitType _itemOne;

        public TBase HighestRange => _range.Maximum;
        public TBase LowestRange => _range.Minimum;
        public DateTime HighestRangeDay => _range.MaximumTime;
        public DateTime LowestRangeDay => _range.MinimumTime;

        public DayStatisticUnit()
        {
            _range = new MaxMinAverageUnit<TBase, TUnitType>();
            _total = new MaxMinAverageUnit<TBase, TUnitType>();
            _itemOne = (TUnitType)Enum.ToObject(typeof(TUnitType), 1);
            _zeroQuantity = (TBase)Activator.CreateInstance(typeof(TBase), 0, _itemOne);
            Reset();
        }

        public void Add(MaxMinAverageUnit<TBase, TUnitType> dayStatistics)
        {
            DateTime day = dayStatistics.MaximumTime.Date;
            _range.AddValue
                (
                day, 
                UnitTools.Subtract<TBase, TUnitType>(dayStatistics.Maximum,dayStatistics.Minimum,_itemOne)
                );
            _total.AddValue(day, dayStatistics.Total);
            _count++;

            if (dayStatistics.Maximum.CompareTo(LowestMaximum) < 0 || _count == 0)
            {
                LowestMaximum = dayStatistics.Maximum;
                LowestMaximumDay = day;
            }
            if (dayStatistics.Minimum.CompareTo(HighestMinimum) > 0 || _count == 0)
            {
                HighestMinimum = dayStatistics.Minimum;
                HighestMinimumDay = day;
            }
        }

        public void Reset()
        {
            LowestMaximum = _zeroQuantity;
            HighestMinimum = _zeroQuantity;
            _count = 0;
            LowestMaximumDay = DateTime.Today;
            HighestMinimumDay = DateTime.Today;
            _total.Reset();
            _range.Reset();
        }
    }
}