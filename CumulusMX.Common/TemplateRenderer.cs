using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Antlr4.StringTemplate;
using CumulusMX.Common.StringTemplate;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace CumulusMX.Common
{
    public class TemplateRenderer
    {
        private readonly TextReader _templateStream;
        private readonly IWeatherDataStatistics _data;
        private readonly IDataReporterSettings _settings;
        private readonly Dictionary<string, object> _extraRenderParameters;
        private readonly ILogger _logger;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<ITagDetails> ExtraTags { get; set; } = new List<ITagDetails>();

        public TemplateRenderer(TextReader templateStream, IWeatherDataStatistics data, IDataReporterSettings settings, Dictionary<string, object> extraRenderParameters, ILogger logger)
        {
            _templateStream = templateStream;
            _data = data;
            _settings = settings;
            _extraRenderParameters = extraRenderParameters;
            _logger = logger;
        }

        public string DoubleFormat { get; set; } = "{0:F1}";

        public string DateFormat { get; set; } = "{0:d}";

        public string Render()
        {
            var templateGroup = new TemplateGroup('«', '}');
            templateGroup.RegisterRenderer(typeof(double), new DefaultNumberRenderer(DoubleFormat));
            templateGroup.RegisterRenderer(typeof(DateTime), new DefaultDateRenderer(DateFormat));
            templateGroup.RegisterRenderer(typeof(Length), new DefaultLengthRenderer());
            templateGroup.RegisterRenderer(typeof(Pressure), new DefaultPressureRenderer());
            templateGroup.RegisterRenderer(typeof(Speed), new DefaultSpeedRenderer());
            templateGroup.RegisterRenderer(typeof(Temperature), new DefaultTemperatureRenderer());
            templateGroup.RegisterRenderer(typeof(Ratio), new DefaultRatioRenderer());
            templateGroup.RegisterModelAdaptor(typeof(object),new ExtendedObjectModelAdaptor());

            var templateString = _templateStream.ReadToEnd();
            templateString = templateString.Replace("${", "«");
            var template = new Template(templateGroup, templateString);
            
            template.Add("Settings", _settings);
            template.Add("data", _data);
            template.Add("timestamp", Timestamp);

            foreach (var extraParameter in _extraRenderParameters)
            {
                template.Add(extraParameter.Key, extraParameter.Value);
            }

            foreach (var tagDetails in ExtraTags)
            {
                template.Add(tagDetails.RegistrationName, tagDetails);
            }

            _data.GetReadLock();
            var result = string.Empty;
            try
            {
                result = template.Render(CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error generating Awekas call : {ex}");
            }
            finally
            {
                _data.ReleaseReadLock();
            }

            return result;
        }
    }
}