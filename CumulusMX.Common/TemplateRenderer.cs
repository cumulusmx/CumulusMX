using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Antlr.Runtime;
using Antlr4.StringTemplate;
using CumulusMX.Common.StringTemplate;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace AwekasDataReporter
{
    public class TemplateRenderer
    {
        private readonly string _templatePath;
        private readonly IWeatherDataStatistics _data;
        private readonly IDataReporterSettings _settings;
        private readonly Dictionary<string, object> _extraRenderParameters;
        private readonly ILogger _logger;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<ITagDetails> ExtraTags { get; set; } = new List<ITagDetails>();

        public TemplateRenderer(string templatePath, IWeatherDataStatistics data, IDataReporterSettings settings, Dictionary<string, object> extraRenderParameters, ILogger logger)
        {
            _templatePath = templatePath;
            _data = data;
            _settings = settings;
            _extraRenderParameters = extraRenderParameters;
            _logger = logger;
        }

        public string DoubleFormat { get; set; } = "{0:F1}";

        public string DateFormat { get; set; } = "{0:d}";

        public string Render()
        {
            var templateGroup = new TemplateGroup('[',']');
            templateGroup.RegisterRenderer(typeof(double), new DefaultNumberRenderer(DoubleFormat));
            templateGroup.RegisterRenderer(typeof(DateTime), new DefaultDateRenderer(DateFormat));
            var templateString = File.ReadAllText(_templatePath);
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