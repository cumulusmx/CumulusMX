using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace CumulusMX.ThirdParty
{
	internal abstract class WebUploadServiceBase
	{
		internal Cumulus cumulus;
		internal WeatherStation station;

		internal string Name;
		internal string Server;
		internal int Port;
		internal string ID;
		internal string PW;
		internal bool Enabled;
		internal int Interval;
		internal int DefaultInterval;
		internal bool SynchronisedUpdate;
		internal bool SendUV;
		internal bool SendSolar;
		internal bool SendIndoor;
		internal bool SendAirQuality;
		internal bool SendSoilTemp;
		internal int SoilTempSensor;
		internal bool SendSoilMoisture;
		internal int SoilMoistureSensor;
		internal bool CatchUp;
		internal bool CatchingUp;
		internal bool Updating;


		internal List<string> CatchupList = [];

		internal Timer IntTimer = new();

		private protected WebUploadServiceBase(Cumulus cumulus, string name)
		{
			this.cumulus = cumulus;
			Name = name;
		}

		internal void CatchUpIfRequired()
		{
			if (CatchupList == null)
			{
				// we've already been through here
				// do nothing
				cumulus.LogDebugMessage($"{Name} catch-up list is null");
			}
			else if (CatchupList.Count == 0)
			{
				// No archived entries to upload
				CatchupList = null;
				cumulus.LogDebugMessage($"{Name} catch-up list is zero length");
			}
			else
			{
				// start the archive upload thread
				_ = DoCatchUp();
			}
		}

		internal virtual async Task DoCatchUp()
		{
			Updating = true;

			for (int i = 0; i < CatchupList.Count; i++)
			{
				cumulus.LogMessage($"Uploading {Name} archive #" + (i + 1));
				try
				{
					using var response = await Cumulus.MyHttpClient.GetAsync(CatchupList[i]);
					cumulus.LogMessage($"{Name} Response: {response.StatusCode}: {response.ReasonPhrase}");
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"{Name} update error");
				}
			}

			cumulus.LogMessage($"End of {Name} archive upload");
			CatchupList.Clear();
			CatchingUp = false;
			Updating = false;
		}

		internal void AddToList(DateTime timestamp)
		{
			if (Enabled && CatchUp)
			{
				string pwstring;
				string URL = GetURL(out pwstring, timestamp);

				CatchupList.Add(URL);

				string LogURL = URL;
				if (pwstring != null)
				{
					LogURL = LogURL.Replace(pwstring, new string('*', pwstring.Length));
				}

				cumulus.LogMessage($"Creating {Name} URL #{CatchupList.Count} - {LogURL}");
			}
		}

		internal abstract Task DoUpdate(DateTime timestamp);

		internal abstract string GetURL(out string pwstring, DateTime timestamp);
	}
}
