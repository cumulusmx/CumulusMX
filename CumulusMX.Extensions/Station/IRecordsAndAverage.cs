using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public interface IRecordsAndAverage<TBase> : IRecords<TBase>
    {
        TBase Average { get; }
        TBase Total { get; }
    }
}
