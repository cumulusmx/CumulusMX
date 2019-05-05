using System;

namespace CumulusMX.Extensions
{
    public interface IAddable
    {
        void Add(DateTime timestamp, object value);
    }
}