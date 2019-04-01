using System;
using UnitsNet;

namespace CumulusMX.Data
{
    public static class UnitTools
    {
        public static TOutput Add<TOutput, TOutputUnit>(TOutput arg1, TOutput arg2)
            where TOutput : IQuantity<TOutputUnit>
            where TOutputUnit : Enum
        {
            var itemOne = (TOutputUnit)Enum.ToObject(typeof(TOutputUnit), 1);
            return Add(arg1, arg2, itemOne);
        }

        public static TOutput Add<TOutput, TOutputUnit>(TOutput arg1, TOutput arg2, TOutputUnit itemOne)
            where TOutput : IQuantity<TOutputUnit>
            where TOutputUnit : Enum
        {
            return (TOutput)Activator.CreateInstance(
                typeof(TOutput),
                arg1.As(itemOne) + arg2.As(itemOne),
                itemOne
            );
        }

        public static TOutput Subtract<TOutput, TOutputUnit>(TOutput arg1, TOutput arg2)
            where TOutput : IQuantity<TOutputUnit>
            where TOutputUnit : Enum
        {
            var itemOne = (TOutputUnit)Enum.ToObject(typeof(TOutputUnit), 1);
            return Subtract(arg1, arg2, itemOne);
        }

        public static TOutput Subtract<TOutput, TOutputUnit>(TOutput arg1, TOutput arg2, TOutputUnit itemOne)
            where TOutput : IQuantity<TOutputUnit>
            where TOutputUnit : Enum
        {
            return (TOutput)Activator.CreateInstance(
                typeof(TOutput),
                arg1.As(itemOne) - arg2.As(itemOne),
                itemOne
            );
        }

        public static TUnitType FirstUnit<TUnitType>() where TUnitType : Enum
        {
            return (TUnitType)Enum.ToObject(typeof(TUnitType), 1);
        }

        public static TBase ZeroQuantity<TBase, TUnitType>() where TBase : IComparable, IQuantity where TUnitType : Enum
        {
            var unitType = FirstUnit<TUnitType>();
            return (TBase)Activator.CreateInstance(typeof(TBase), 0, unitType);
        }

    }
}
