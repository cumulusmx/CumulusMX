using System;
using System.Linq;
using CumulusMX.Extensions;
using log4net;
using log4net.Appender;

namespace CumulusMX.Common
{
    public class LogWrapper : ILogger
    {
        private readonly ILog _log;

        public LogWrapper(log4net.ILog log)
        {
            _log = log;
        }

        public void Debug(string message)
        {
            _log.Debug(message);
        }

        public void Debug(string message, Exception ex)
        {
            _log.Debug(message,ex);
        }

        public void Error(string message)
        {
            _log.Error(message);
        }

        public void Error(string message, Exception ex)
        {
            _log.Error(message,ex);
        }

        public void Fatal(string message)
        {
            _log.Fatal(message);
        }

        public void Fatal(string message, Exception ex)
        {
            _log.Fatal(message,ex);
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Info(string message, Exception ex)
        {
            _log.Info(message,ex);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Warn(string message, Exception ex)
        {
            _log.Warn(message,ex);
        }

        public string GetFileName()
        {
           
            return _log.Logger.Repository.GetAppenders().OfType<FileAppender>()
                .FirstOrDefault().File;
        }
    }
}
