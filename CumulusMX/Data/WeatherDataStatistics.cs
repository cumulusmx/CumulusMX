using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Common;
using CumulusMX.Configuration;
using CumulusMX.Data.Statistics;
using CumulusMX.Data.Statistics.Double;
using CumulusMX.Data.Statistics.Unit;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using UnitsNet;
using UnitsNet.Serialization.JsonNet;
using UnitsNet.Units;

namespace CumulusMX.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WeatherDataStatistics : IWeatherDataStatistics
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly List<string> RESERVED_NAMES = new List<string> {"Timestamp"};

        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public Dictionary<Type, Type> UNIT_TYPES = new Dictionary<Type, Type>
        {
            {typeof(Speed), typeof(SpeedUnit)},
            {typeof(Angle),typeof(AngleUnit) },
            {typeof(Length),typeof(LengthUnit) },
            {typeof(Irradiance),typeof(IrradianceUnit) },
            {typeof(Temperature),typeof(TemperatureUnit) },
            {typeof(Ratio),typeof(RatioUnit) },
            {typeof(Pressure),typeof(PressureUnit) },
            {typeof(Number),typeof(NumberUnit) }
        };

        [JsonProperty]
        private Dictionary<string, IStatistic> _measures = new Dictionary<string,IStatistic>();
        private List<CalculationDetails> _calculations;
        [JsonProperty]
        private Dictionary<string,IDayBooleanStatistic> _dayStatistics;

        public IStatistic this[string key]
        {
            get
            {
                if (_measures.ContainsKey(key)) return (IStatistic)_measures[key];

                if (_dayStatistics.ContainsKey(key)) return (IStatistic) _dayStatistics[key];
                    
                _log.Warn($"No weather statistic named {key} defined.");

                return null;
                
            }
        }

        public IDayBooleanStatistic HeatingDegreeDays { get; }
        public IDayBooleanStatistic CoolingDegreeDays { get; }
        public IDayBooleanStatistic DryDays { get; }
        public IDayBooleanStatistic RainDays { get; }
        // ? Forecast

        [JsonProperty]
        public DateTime Time { get; private set; }
        public DateTime Yesterday => Time.AddDays(-1);
        [JsonProperty]
        public DateTime FirstRecord { get; private set; }
        public TimeSpan SinceFirstRecord => Time - FirstRecord;

        public WeatherDataStatistics()
        {

            Time = DateTime.MinValue;
            FirstRecord = DateTime.MinValue;
            _calculations = new List<CalculationDetails>();
            _dayStatistics = new Dictionary<string, IDayBooleanStatistic>();
        }

        public bool DefineStatistic(string statisticName, Type statisticType)
        {
            if (RESERVED_NAMES.Contains(statisticName))
            {
                _log.Warn($"The statistic name {statisticName} is reserved.");
                return false;
            }

            if (_measures.ContainsKey(statisticName))
            {
                if (!_measures[statisticName].Linked)
                {
                    _measures[statisticName].Linked = true;
                    return true;
                }
                else
                {
                    _log.Warn($"A weather statistic named {statisticName} is already defined. Ignoring the new one.");
                    return false;
                }
                
            }

            Type typeInfo;
            if (statisticType == typeof(double))
            {
                typeInfo = typeof(StatisticUnit<,>).MakeGenericType(typeof(Number), typeof(NumberUnit));
            }
            else
            {
                typeInfo =
                    typeof(StatisticUnit<,>).MakeGenericType(statisticType, UNIT_TYPES[statisticType]);
            }

            var statistic = (IStatistic)Activator.CreateInstance(typeInfo);
            statistic.Linked = true;
            _measures[statisticName] = statistic;

            return true;
        }

        public bool DefineCalculation(string measureName, IEnumerable<string> inputs, MethodInfo method)
        {
            if (!_measures.ContainsKey(measureName))
            {
                _log.Error($"Please define measure {measureName} before defining calculation for it.");
                return false;
            }

            if (_calculations.Any(x => x.Measure == measureName) || _dayStatistics.ContainsKey(measureName))
            {
                _log.Error($"Existing calculation defined for measure {measureName}.");
                return false;
            }

            var missingInputs = inputs.Where(x => !_measures.ContainsKey(x));
            if (missingInputs.Any())
            {
                _log.Error($"Please define input measure{(missingInputs.Count() > 1 ? "s" : string.Empty)} for calculation {measureName} before the calculation.  Missing inputs are {string.Join(',',missingInputs)}.");
                return false;
            }

            //TODO: This could do with more checks - particularly around types.

            _calculations.Add(new CalculationDetails() {Measure = measureName, Inputs = inputs, Method = method});
            return true;
        }

        public bool DefineDayStatistic(string measureName, string input, string lambda)
        {
            if (_measures.ContainsKey(measureName))
            {
                if (!_measures[measureName].Linked)
                {
                    _measures[measureName].Linked = true;
                    return true;
                }
                else
                {
                    _log.Error($"Measure named {measureName} already defined when defining a day statistic.");
                    return false;
                }
            }

            //var targetMeasure = targetMeasureObj as StatisticUnit<IQuantity<Enum>, Enum>;

            if (_dayStatistics.ContainsKey(measureName))
            {
                if (!_dayStatistics[measureName].Linked)
                {
                    _dayStatistics[measureName].Linked = true;
                    return true;
                }
                else
                {
                    _log.Error($"Existing day statistic defined named {measureName}.");
                    return false;
                }
            }

            if (!_measures.ContainsKey(input))
            {
                _log.Error($"Please define input measure {input} for calculation {measureName} before the calculation.");
                return false;
            }

            dynamic inputMeasure = _measures[input];
            if (inputMeasure == null)
            {
                _log.Error($"Input measure {input} for calculation {measureName} has an unsupported type.");
                return false;
            }

            var statisticsType = inputMeasure.GetType();
            var underlyingType = statisticsType.GetGenericArguments()[0];

            var options = ScriptOptions.Default.WithImports("UnitsNet").WithReferences("Microsoft.CSharp")
                .AddReferences(underlyingType.Assembly,typeof(Temperature).Assembly);

            var genericType = typeof(DayBooleanStatistic<>).MakeGenericType(underlyingType);
            var lambdaExpression = CSharpScript.Create<bool>(lambda, options, typeof(InputGlobals));

            var booleanStat = (IDayBooleanStatistic)Activator.CreateInstance(genericType, inputMeasure, lambdaExpression);
            booleanStat.Linked = true;
            inputMeasure.AddBooleanStatistics(booleanStat);
            _dayStatistics.Add(measureName,booleanStat);
            return true;
        }

        public void Add(WeatherDataModel data)
        {
            _lock.EnterWriteLock();

            var mappings = data.Mappings;

            var timestamp = data.Timestamp;
            if (FirstRecord == DateTime.MinValue)
                FirstRecord = timestamp;

            try
            {
                foreach (var observation in data.Keys)
                {
                    if (mappings.ContainsKey(observation) && _measures.ContainsKey(mappings[observation]))
                        ((IAddable) _measures[mappings[observation]]).Add(timestamp, data[observation]);
                }

                foreach (var calc in _calculations)
                {
                    try
                    {
                        if (data.Keys.Intersect(calc.Inputs).Any())
                        {
                            var parameters = calc.Inputs.Select(x => ((IStatistic) _measures[x]).LatestObject)
                                .ToArray();
                            if (parameters.Any(x => x == null)) continue;

                            var value = (IQuantity) calc.Method.Invoke(null, parameters);
                            ((IAddable) _measures[calc.Measure]).Add(timestamp, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error applying calculation {calc.Measure}.  Error is {ex}.");
                    }
                }

                Time = timestamp;
            }
            catch (Exception ex)
            {
                _log.Error($"Error while updating weather data.  Error is {ex}.");
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
        }

        public void GetReadLock()
        {
            _lock.EnterReadLock();
        }

        public void ReleaseReadLock()
        {
            _lock.ExitReadLock();
        }

        public static WeatherDataStatistics TryLoad(string dataFile)
        {
            try
            {
                
                using (var fileReader = File.OpenText(dataFile))
                {
                    var serialiser = new JsonSerializer();
                    //var unitsNetJson = new UnitsNetJsonConverter();
                    //unitsNetJson.AddUnit(typeof(Number), typeof(NumberUnit));
                    serialiser.Converters.Add(new UnitsNetJsonConverter());
                    serialiser.TypeNameHandling = TypeNameHandling.Auto;
                    var reader = new JsonTextReader(fileReader);
                    var newWds = serialiser.Deserialize<WeatherDataStatistics>(reader);
                    newWds.Filename = dataFile;
                    _log.Info($"Loaded existing weather data from {dataFile} up to {newWds.Time}.");
                    return newWds;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to load existing weather data from {dataFile}. Creating new data.",ex);
                return new WeatherDataStatistics() {Filename = dataFile};
            }
        }

        [JsonIgnore]
        public string Filename { get; set; }

        public void Save()
        {
            SaveAs(Filename);
        }

        public void SaveAs(string filename)
        { 
            GetReadLock();
            try
            {
                var serialiser = new JsonSerializer();
                serialiser.Converters.Add(new UnitsNetJsonConverter());
                serialiser.TypeNameHandling = TypeNameHandling.Auto;
                using (var fileWriter = File.Create(filename))
                {
                    using (var streamW = new StreamWriter(fileWriter))
                    {
                        using (var writer = new JsonTextWriter(streamW))
                            serialiser.Serialize(writer, this);
                    }
                }
            }
            finally
            {
                ReleaseReadLock();
            }
        }
    }
}
