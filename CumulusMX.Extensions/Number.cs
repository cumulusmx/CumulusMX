using System;
using System.Linq;
using UnitsNet.Units;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace UnitsNet
{
    /// <summary>
    ///     In mathematics, a Number is a simple scalar value - not necessarily an integer.  It is unitless.
    /// </summary>
    public struct Number : IQuantity<NumberUnit>, IEquatable<Number>, IComparable, IComparable<Number>
    {
        /// <summary>
        ///     The numeric value this quantity was constructed with.
        /// </summary>
        private readonly double _value;

        /// <summary>
        ///     The unit this quantity was constructed with.
        /// </summary>
        private readonly NumberUnit? _unit;

        private static readonly QuantityInfo<NumberUnit> Info;

        static Number()
        {
            BaseDimensions = BaseDimensions.Dimensionless;

            Info = new QuantityInfo<NumberUnit>
            (
                QuantityType.Ratio,
                new[] { new UnitInfo<NumberUnit>(NumberUnit.Value, BaseUnits.Undefined), },
                NumberUnit.Value,
                Zero,
                BaseDimensions.Dimensionless
            );

            //QuantityFactory.Default.AddUnit(typeof(Number), typeof(NumberUnit));
        }

        /// <summary>
        ///     Creates the quantity with the given numeric value and unit.
        /// </summary>
        /// <param name="numericValue">The numeric value  to contruct this quantity with.</param>
        /// <param name="unit">The unit representation to contruct this quantity with.</param>
        /// <remarks>Value parameter cannot be named 'value' due to constraint when targeting Windows Runtime Component.</remarks>
        /// <exception cref="ArgumentException">If value is NaN or Infinity.</exception>
        public Number(double numericValue, NumberUnit unit)
        {
            if (unit == NumberUnit.Undefined)
                throw new ArgumentException("The quantity can not be created with an undefined unit.", nameof(unit));

            _value = numericValue;
            _unit = unit;

        }

        #region Static Properties

        /// <summary>
        ///     The <see cref="BaseDimensions" /> of this quantity.
        /// </summary>
        public static BaseDimensions BaseDimensions { get; }

        /// <summary>
        ///     The base unit of Number, which is Value. All conversions go via this value.
        /// </summary>
        public static NumberUnit BaseUnit => NumberUnit.Value;

        public static implicit operator double(Number theNumber)
        {
            return theNumber.Value;
        }

        public static implicit operator Number(double theDouble)
        {
            return new Number(theDouble,NumberUnit.Value);
        }

        /// <summary>
        /// Represents the largest possible value of Number
        /// </summary>
        public static Number MaxValue => new Number(double.MaxValue, BaseUnit);

        /// <summary>
        /// Represents the smallest possible value of Number
        /// </summary>
        public static Number MinValue => new Number(double.MinValue, BaseUnit);

        /// <summary>
        ///     The <see cref="QuantityType" /> of this quantity.
        /// </summary>
        public static QuantityType QuantityType => QuantityType.Undefined;

        /// <summary>
        ///     All units of measurement for the Number quantity.
        /// </summary>
        public static NumberUnit[] Units { get; } = Enum.GetValues(typeof(NumberUnit)).Cast<NumberUnit>().Except(new NumberUnit[] { NumberUnit.Undefined }).ToArray();

        /// <summary>
        ///     Gets an instance of this quantity with a value of 0 in the base unit Value.
        /// </summary>
        public static Number Zero => new Number(0, BaseUnit);

        #endregion

        #region Properties

        /// <summary>
        ///     The numeric value this quantity was constructed with.
        /// </summary>
        public double Value => _value;

        public IQuantity ToUnit(Enum unit)
        {
            throw new NotImplementedException();
        }

        public IQuantity<NumberUnit> ToUnit(UnitSystem unitSystem)
        {
            throw new NotImplementedException();
        }

        IQuantity<NumberUnit> IQuantity<NumberUnit>.ToUnit(NumberUnit unit)
        {
            return ToUnit(unit);
        }

        IQuantity IQuantity.ToUnit(UnitSystem unitSystem)
        {
            return ToUnit(unitSystem);
        }

        public QuantityInfo<NumberUnit> QuantityInfo => Info;

        public double As(Enum unit)
        {
            throw new NotImplementedException();
        }

        public double As(UnitSystem unitSystem)
        {
            throw new NotImplementedException();
        }

        Enum IQuantity.Unit => Unit;

        /// <summary>
        ///     The unit this quantity was constructed with -or- <see cref="BaseUnit" /> if default ctor was used.
        /// </summary>
        public NumberUnit Unit => _unit.GetValueOrDefault(BaseUnit);

        /// <summary>
        ///     The <see cref="QuantityType" /> of this quantity.
        /// </summary>
        public QuantityType Type => Number.QuantityType;

        /// <summary>
        ///     The <see cref="BaseDimensions" /> of this quantity.
        /// </summary>
        public BaseDimensions Dimensions => Number.BaseDimensions;

        QuantityInfo IQuantity.QuantityInfo => QuantityInfo;

        #endregion

        #region Conversion Properties

        /// <summary>
        ///     Get Number in Values.
        /// </summary>
        public double Values => As(NumberUnit.Value);

        #endregion

        #region Static Methods

        /// <summary>
        ///     Get unit abbreviation string.
        /// </summary>
        /// <param name="unit">Unit to get abbreviation for.</param>
        /// <returns>Unit abbreviation string.</returns>
        public static string GetAbbreviation(NumberUnit unit)
        {
            return GetAbbreviation(unit, null);
        }

        /// <summary>
        ///     Get unit abbreviation string.
        /// </summary>
        /// <param name="unit">Unit to get abbreviation for.</param>
        /// <returns>Unit abbreviation string.</returns>
        /// <param name="provider">Format to use for localization. Defaults to <see cref="GlobalConfiguNumbern.DefaultCulture" /> if null.</param>
        public static string GetAbbreviation(NumberUnit unit, IFormatProvider provider)
        {
            return UnitAbbreviationsCache.Default.GetDefaultAbbreviation(unit, provider);
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        ///     Get Number from Values.
        /// </summary>
        /// <exception cref="ArgumentException">If value is NaN or Infinity.</exception>
        public static Number FromValue(QuantityValue values)
        {
            double value = (double)values;
            return new Number(value, NumberUnit.Value);
        }


        /// <summary>
        ///     Dynamically convert from value and unit enum <see cref="NumberUnit" /> to <see cref="Number" />.
        /// </summary>
        /// <param name="value">Value to convert from.</param>
        /// <param name="fromUnit">Unit to convert from.</param>
        /// <returns>Number unit value.</returns>
        public static Number From(QuantityValue value, NumberUnit fromUnit)
        {
            return new Number((double)value, fromUnit);
        }

        #endregion

      
        #region Arithmetic Operators

        public static Number operator -(Number right)
        {
            return new Number(-right.Value, right.Unit);
        }

        public static Number operator +(Number left, Number right)
        {
            return new Number(left.Value + right.AsBaseNumericType(left.Unit), left.Unit);
        }

        public static Number operator -(Number left, Number right)
        {
            return new Number(left.Value - right.AsBaseNumericType(left.Unit), left.Unit);
        }

        public static Number operator *(double left, Number right)
        {
            return new Number(left * right.Value, right.Unit);
        }

        public static Number operator *(Number left, double right)
        {
            return new Number(left.Value * right, left.Unit);
        }

        public static Number operator /(Number left, double right)
        {
            return new Number(left.Value / right, left.Unit);
        }

        public static double operator /(Number left, Number right)
        {
            return left.Values / right.Values;
        }

        #endregion

        #region Equality / IComparable

        public static bool operator <=(Number left, Number right)
        {
            return left.Value <= right.AsBaseNumericType(left.Unit);
        }

        public static bool operator >=(Number left, Number right)
        {
            return left.Value >= right.AsBaseNumericType(left.Unit);
        }

        public static bool operator <(Number left, Number right)
        {
            return left.Value < right.AsBaseNumericType(left.Unit);
        }

        public static bool operator >(Number left, Number right)
        {
            return left.Value > right.AsBaseNumericType(left.Unit);
        }

        public static bool operator ==(Number left, Number right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Number left, Number right)
        {
            return !(left == right);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));
            if (!(obj is Number objNumber)) throw new ArgumentException("Expected type Number.", nameof(obj));

            return CompareTo(objNumber);
        }

        // Windows Runtime Component does not allow public methods/ctors with same number of parameters: https://msdn.microsoft.com/en-us/library/br230301.aspx#Overloaded methods
        public int CompareTo(Number other)
        {
            return _value.CompareTo(other.AsBaseNumericType(this.Unit));
        }

        public override bool Equals(object obj)
        {
            if (obj is null || !(obj is Number objNumber))
                return false;

            return Equals(objNumber);
        }

        public bool Equals(Number other)
        {
            return _value.Equals(other.AsBaseNumericType(this.Unit));
        }

        /// <summary>
        ///     <para>
        ///     Compare equality to another Number within the given absolute or relative tolerance.
        ///     </para>
        ///     <para>
        ///     Relative tolerance is defined as the maximum allowable absolute difference between this quantity's value and
        ///     <paramref name="other"/> as a percentage of this quantity's value. <paramref name="other"/> will be converted into
        ///     this quantity's unit for comparison. A relative tolerance of 0.01 means the absolute difference must be within +/- 1% of
        ///     this quantity's value to be considered equal.
        ///     <example>
        ///     In this example, the two quantities will be equal if the value of b is within +/- 1% of a (0.02m or 2cm).
        ///     <code>
        ///     var a = Length.FromMeters(2.0);
        ///     var b = Length.FromInches(50.0);
        ///     a.Equals(b, 0.01, ComparisonType.Relative);
        ///     </code>
        ///     </example>
        ///     </para>
        ///     <para>
        ///     Absolute tolerance is defined as the maximum allowable absolute difference between this quantity's value and
        ///     <paramref name="other"/> as a fixed number in this quantity's unit. <paramref name="other"/> will be converted into
        ///     this quantity's unit for comparison.
        ///     <example>
        ///     In this example, the two quantities will be equal if the value of b is within 0.01 of a (0.01m or 1cm).
        ///     <code>
        ///     var a = Length.FromMeters(2.0);
        ///     var b = Length.FromInches(50.0);
        ///     a.Equals(b, 0.01, ComparisonType.Absolute);
        ///     </code>
        ///     </example>
        ///     </para>
        ///     <para>
        ///     Note that it is advised against specifying zero difference, due to the nature
        ///     of floating point opeNumberns and using System.Double internally.
        ///     </para>
        /// </summary>
        /// <param name="other">The other quantity to compare to.</param>
        /// <param name="tolerance">The absolute or relative tolerance value. Must be greater than or equal to 0.</param>
        /// <param name="comparisonType">The comparison type: either relative or absolute.</param>
        /// <returns>True if the absolute difference between the two values is not greater than the specified relative or absolute tolerance.</returns>
        public bool Equals(Number other, double tolerance, ComparisonType comparisonType)
        {
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException("tolerance", "Tolerance must be greater than or equal to 0.");

            double thisValue = (double)this.Value;
            double otherValueInThisUnits = other.As(this.Unit);

            return UnitsNet.Comparison.Equals(thisValue, otherValueInThisUnits, tolerance, comparisonType);
        }

        /// <summary>
        ///     Returns the hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current Number.</returns>
        public override int GetHashCode()
        {
            return new { QuantityType, Value, Unit }.GetHashCode();
        }

        #endregion

        #region Conversion Methods

        /// <summary>
        ///     Convert to the unit representation <paramref name="unit" />.
        /// </summary>
        /// <returns>Value converted to the specified unit.</returns>
        public double As(NumberUnit unit)
        {
            return Value;

        }

        /// <summary>
        ///     Converts this Number to another Number with the unit representation <paramref name="unit" />.
        /// </summary>
        /// <returns>A Number with the specified unit.</returns>
        public Number ToUnit(NumberUnit unit)
        {
            return new Number(Value, unit);
        }

        /// <summary>
        ///     Converts the current value + unit to the base unit.
        ///     This is typically the first step in converting from one unit to another.
        /// </summary>
        /// <returns>The value in the base unit representation.</returns>
        private double AsBaseUnit()
        {
            switch (Unit)
            {
                case NumberUnit.Value: return _value;

                default:
                    throw new NotImplementedException($"Can not convert {Unit} to base units.");
            }
        }

        private double AsBaseNumericType(NumberUnit unit)
        {
            if (Unit == unit)
                return _value;

            var baseUnitValue = AsBaseUnit();

            switch (unit)
            {
                case NumberUnit.Value: return baseUnitValue;

                default:
                    throw new NotImplementedException($"Can not convert {Unit} to {unit}.");
            }
        }

        #endregion

        #region ToString Methods

        /// <summary>
        ///     Get default string representation of value and unit.
        /// </summary>
        /// <returns>String representation.</returns>
        public override string ToString()
        {
            return ToString(null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return _value.ToString(format,formatProvider);
        }

        /// <summary>
        ///     Get string representation of value and unit. Using two significant digits after radix.
        /// </summary>
        /// <returns>String representation.</returns>
        /// <param name="provider">Format to use for localization and number formatting. Defaults to <see cref="GlobalConfiguNumbern.DefaultCulture" /> if null.</param>
        public string ToString(IFormatProvider provider)
        {
            return ToString(provider, 2);
        }

        public static string GetFormat(double value, int significantDigitsAfterRadix)
        {
            double v = Math.Abs(value);
            var sigDigitsAfterRadixStr = new string('#', significantDigitsAfterRadix);
            string format;

            if (v < 1e-3)
            {
                format = "{0:0." + sigDigitsAfterRadixStr + "e-00} {1}";
            }
            // Values from 1e-3 to 1 use fixed point notation.
            else if ((v > 1e-4) && (v < 1))
            {
                format = "{0:g" + significantDigitsAfterRadix + "} {1}";
            }
            // Values between 1 and 1e5 use fixed point notation with digit grouping.
            else if ((v >= 1) && (v < 1e6))
            {
                // The comma will be automatically replaced with the correct digit separator if a different culture is used.
                format = "{0:#,0." + sigDigitsAfterRadixStr + "} {1}";
            }
            // Values above 1e5 use scientific notation.
            else
            {
                format = "{0:0." + sigDigitsAfterRadixStr + "e+00} {1}";
            }

            return format;
        }

        /// <summary>
        ///     Get string representation of value and unit.
        /// </summary>
        /// <param name="significantDigitsAfterRadix">The number of significant digits after the radix point.</param>
        /// <returns>String representation.</returns>
        /// <param name="provider">Format to use for localization and number formatting. Defaults to <see cref="GlobalConfiguNumbern.DefaultCulture" /> if null.</param>
        public string ToString(IFormatProvider provider, int significantDigitsAfterRadix)
        {
            var value = Convert.ToDouble(Value);
            var format = GetFormat(value, significantDigitsAfterRadix);
            return ToString(provider, format);
        }

        /// <summary>
        ///     Get string representation of value and unit.
        /// </summary>
        /// <param name="format">String format to use. Default:  "{0:0.##} {1} for value and unit abbreviation respectively."</param>
        /// <param name="args">Arguments for string format. Value and unit are implictly included as arguments 0 and 1.</param>
        /// <returns>String representation.</returns>
        /// <param name="provider">Format to use for localization and number formatting. Defaults to <see cref="GlobalConfiguNumbern.DefaultCulture" /> if null.</param>
        public string ToString(IFormatProvider provider, string format, params object[] args)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (args == null) throw new ArgumentNullException(nameof(args));

            provider = provider ?? GlobalConfiguration.DefaultCulture;

            var value = Convert.ToDouble(Value);
            var formatArgs = GetFormatArgs(Unit, value, provider, args);
            return string.Format(provider, format, formatArgs);
        }

        #endregion
        public static object[] GetFormatArgs<TUnitType>(TUnitType unit, double value, IFormatProvider culture, IEnumerable<object> args)
            where TUnitType : Enum
        {
            string abbreviation = string.Empty;
            return new object[] { value, abbreviation }.Concat(args).ToArray();
        }
    }
}
