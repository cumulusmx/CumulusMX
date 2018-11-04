using System;
using System.ComponentModel;

namespace CumulusMX
{
    internal class WeatherData
    {
        public DateTime DT { get; set; }

        public double WindSpeed
        {
            get { return _windspeed; }
            set
            {
                _windspeed = value;
                var handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs("WindSpeed"));
                }
            }
        }

        private double _windspeed;

        public double WindAverage
        {
            get { return _windaverage; }
            set
            {
                _windaverage = value;
                var handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs("WindAverage"));
                }
            }
        }

        private double _windaverage;

        public double OutdoorTemp
        {
            get { return _outdoortemp; }
            set
            {
                _outdoortemp = value;
                var handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs("OutdoorTemp"));
                }
            }
        }

        private double _outdoortemp;

        public double Pressure
        {
            get { return _pressure; }
            set
            {
                _pressure = value;
                var handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs("Pressure"));
                }
            }
        }

        private double _pressure;

        public double Raintotal
        {
            get { return _raintotal; }
            set
            {
                _raintotal = value;
                var handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs("Raintotal"));
                }
            }
        }

        private double _raintotal;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
