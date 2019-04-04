using System;
using System.Timers;
using CoordinateSharp;
using CumulusMX.Extensions;

namespace CumulusMX
{
    public class CelestialDetails : ITagDetails
    {
        public string RegistrationName => "Celestial";

        public double Longitude { get; }
        public double Latitude { get; }
        private DateTime _today;
        private readonly Coordinate _coordinate;
        private readonly Timer _checkDate;

        public CelestialDetails(IConfigurationProvider settings)
        {
            Latitude = settings.GetValue("Station", "Latitude").AsDouble;
            Longitude = settings.GetValue("Station", "Longitude").AsDouble;
            _today = DateTime.Today;
            _coordinate = new CoordinateSharp.Coordinate(Latitude, Longitude, _today);
            _checkDate = new Timer() {Interval = 60 * 1000};
            _checkDate.Elapsed += CheckDay;
            _checkDate.AutoReset = true;
            _checkDate.Enabled = true;
        }

        private void CheckDay(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Today == _today) return;

            _coordinate.GeoDate = DateTime.Today;
            _today = DateTime.Today;
            _checkDate.Enabled = false;
            _checkDate.Interval = 24 * 60 * 60 * 1000;
            _checkDate.Enabled = true;
        }

        public DateTime? Sunrise => _coordinate.CelestialInfo.SunRise;
        public DateTime? Sunset => _coordinate.CelestialInfo.SunSet;
        public DateTime? Moonrise => _coordinate.CelestialInfo.MoonRise;
        public DateTime? Moonset => _coordinate.CelestialInfo.MoonSet;

        public TimeSpan DayLength => (_coordinate.CelestialInfo.SunSet ??_today.AddDays(1)) - 
                                     (_coordinate.CelestialInfo.SunRise ?? _today);
        public bool IsSunUp => _coordinate.CelestialInfo.IsSunUp;

        public TimeSpan MoonLength => (_coordinate.CelestialInfo.MoonSet ?? _today.AddDays(1)) -
                                     (_coordinate.CelestialInfo.MoonRise ?? _today);
        public bool IsMoonUp => _coordinate.CelestialInfo.IsMoonUp;

        public DateTime? CivilDawn => _coordinate.CelestialInfo.AdditionalSolarTimes.CivilDawn;
        public DateTime? CivilDusk => _coordinate.CelestialInfo.AdditionalSolarTimes.CivilDusk;
        public TimeSpan CivilDayLength => (_coordinate.CelestialInfo.AdditionalSolarTimes.CivilDusk ?? _today.AddDays(1)) -
                                     (_coordinate.CelestialInfo.AdditionalSolarTimes.CivilDawn ?? _today);

        public bool IsDaylight => DateTime.Now > (_coordinate.CelestialInfo.AdditionalSolarTimes.CivilDawn ?? _today) &&
                                  DateTime.Now < (_coordinate.CelestialInfo.AdditionalSolarTimes.CivilDusk ?? _today.AddDays(1));

        public DateTime? NauticalDawn => _coordinate.CelestialInfo.AdditionalSolarTimes.NauticalDawn;
        public DateTime? NauticalDusk => _coordinate.CelestialInfo.AdditionalSolarTimes.NauticalDusk;
        public TimeSpan NauticalDayLength => (_coordinate.CelestialInfo.AdditionalSolarTimes.NauticalDusk ?? _today.AddDays(1)) -
                                          (_coordinate.CelestialInfo.AdditionalSolarTimes.NauticalDawn ?? _today);

        public DateTime? AstronomicalDawn => _coordinate.CelestialInfo.AdditionalSolarTimes.AstronomicalDawn;
        public DateTime? AstronomicalDusk => _coordinate.CelestialInfo.AdditionalSolarTimes.AstronomicalDusk;
        public TimeSpan AstronomicalDayLength => (_coordinate.CelestialInfo.AdditionalSolarTimes.AstronomicalDusk ?? _today.AddDays(1)) -
                                          (_coordinate.CelestialInfo.AdditionalSolarTimes.AstronomicalDawn ?? _today);

        public MoonIllum Moon => _coordinate.CelestialInfo.MoonIllum;

    }
}
