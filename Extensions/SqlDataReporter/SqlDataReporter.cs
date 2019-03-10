using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace SqlDataReporter
{
    public class SqlDataReporter : IDataReporter
    {
        private ILogger _logger;
        public string ServiceName => "SQL Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public string Identifier => "TBC"; //TODO

        public SqlDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }


        /*
        private void MySqlCatchup()
        {
            var mySqlConn = new MySqlConnection();
            mySqlConn.Host = MySqlHost;
            mySqlConn.Port = MySqlPort;
            mySqlConn.UserId = MySqlUser;
            mySqlConn.Password = MySqlPass;
            mySqlConn.Database = MySqlDatabase;

            for (int i = 0; i < MySqlList.Count; i++)
            {
                LogMessage("Uploading MySQL archive #" + (i + 1));
                MySqlCommand cmd = new MySqlCommand();
                cmd.CommandText = MySqlList[i];
                cmd.Connection = mySqlConn;
                LogDebugMessage(MySqlList[i]);

                try
                {
                    mySqlConn.Open();
                    int aff = cmd.ExecuteNonQuery();
                    LogMessage("MySQL: Table " + MySqlMonthlyTable + "  " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error encountered during catchup MySQL operation.");
                    LogMessage(ex.Message);
                }
                finally
                {
                    mySqlConn.Close();
                }
            }


            LogMessage("End of MySQL archive upload");
            MySqlList.Clear();
        }

        private void CustomMysqlSecondsTimerTick(object sender, ElapsedEventArgs e)
        {
            if (!customMySqlSecondsUpdateInProgress)
            {
                customMySqlSecondsUpdateInProgress = true;

                try
                {
                    customMysqlSecondsTokenParser.InputText = CustomMySqlSecondsCommandString;
                    CustomMysqlSecondsCommand.CommandText = customMysqlSecondsTokenParser.ToStringFromString();
                    CustomMysqlSecondsConn.Open();
                    int aff = CustomMysqlSecondsCommand.ExecuteNonQuery();
                    //LogMessage("MySQL: " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error encountered during custom seconds MySQL operation.");
                    LogMessage(ex.Message);
                }
                finally
                {
                    CustomMysqlSecondsConn.Close();
                    customMySqlSecondsUpdateInProgress = false;
                }
            }
        }

        internal void CustomMysqlMinutesTimerTick()
        {
            if (!customMySqlMinutesUpdateInProgress)
            {
                customMySqlMinutesUpdateInProgress = true;

                try
                {
                    customMysqlMinutesTokenParser.InputText = CustomMySqlMinutesCommandString;
                    CustomMysqlMinutesCommand.CommandText = customMysqlMinutesTokenParser.ToStringFromString();
                    LogDebugMessage(CustomMysqlMinutesCommand.CommandText);
                    CustomMysqlMinutesConn.Open();
                    int aff = CustomMysqlMinutesCommand.ExecuteNonQuery();
                    LogDebugMessage("MySQL: " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error encountered during custom minutes MySQL operation.");
                    LogMessage(ex.Message);
                }
                finally
                {
                    CustomMysqlMinutesConn.Close();
                    customMySqlMinutesUpdateInProgress = false;
                }
            }
        }

        internal void CustomMysqlRolloverTimerTick()
        {
            if (!customMySqlRolloverUpdateInProgress)
            {
                customMySqlRolloverUpdateInProgress = true;

                try
                {
                    customMysqlRolloverTokenParser.InputText = CustomMySqlRolloverCommandString;
                    CustomMysqlRolloverCommand.CommandText = customMysqlRolloverTokenParser.ToStringFromString();
                    LogDebugMessage(CustomMysqlRolloverCommand.CommandText);
                    CustomMysqlRolloverConn.Open();
                    int aff = CustomMysqlRolloverCommand.ExecuteNonQuery();
                    LogDebugMessage("MySQL: " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error encountered during custom Rollover MySQL operation.");
                    LogMessage(ex.Message);
                }
                finally
                {
                    CustomMysqlRolloverConn.Close();
                    customMySqlRolloverUpdateInProgress = false;
                }
            }
        }

*/
    }
}
