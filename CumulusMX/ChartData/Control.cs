using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public void CreateGraphDataFiles()
		{
			// Chart data for Highcharts graphs
			string json;
			// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
			GraphFileIdx[] createReqOnce = [GraphFileIdx.CONFIG, GraphFileIdx.AVAILABLE, GraphFileIdx.DAILYRAIN, GraphFileIdx.DAILYTEMP, GraphFileIdx.SUNHOURS];

			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				if (GraphDataFiles[i].Create && GraphDataFiles[i].CreateRequired)
				{
#if DEBUG
					LogDebugMessage("CreateGraphDataFiles: Creating " + GraphDataFiles[i].FileName);
#endif
					try
					{
						json = CreateGraphDataJson(GraphDataFiles[i].FileName, false);

						LogDebugMessage("CreateGraphDataFiles: Writing " + GraphDataFiles[i].FileName);
						var dest = Path.Combine(GraphDataFiles[i].LocalPath, GraphDataFiles[i].FileName);
						using (var file = new StreamWriter(dest, false))
						{
							file.WriteLine(json);
							file.Close();
						}

						// The config and daily files only need creating once per change
						// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
						if (createReqOnce.Contains((GraphFileIdx) i))
						{
							GraphDataFiles[i].CreateRequired = false;
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"Error creating/writing {GraphDataFiles[i].FileName}: {ex}");
					}
#if DEBUG
					LogDebugMessage("CreateGraphDataFiles: Completed " + GraphDataFiles[i].FileName);
#endif
				}
			}
		}


		public string CreateGraphDataJson(string filename, bool incremental)
		{
			// Chart data for Highcharts graphs

			return filename switch
			{
				"graphconfig.json" => GetGraphConfig(false),
				"availabledata.json" => GetAvailGraphData(false),
				"tempdata.json" => GetTempGraphData(incremental, false),
				"pressdata.json" => GetPressGraphData(incremental),
				"winddata.json" => GetWindGraphData(incremental),
				"wdirdata.json" => GetWindDirGraphData(incremental),
				"humdata.json" => GetHumGraphData(incremental, false),
				"raindata.json" => GetRainGraphData(incremental),
				"dailyrain.json" => GetDailyRainGraphData(),
				"dailytemp.json" => GetDailyTempGraphData(false),
				"solardata.json" => GetSolarGraphData(incremental, false),
				"sunhours.json" => GetSunHoursGraphData(false),
				"airquality.json" => GetAqGraphData(incremental),
				"extratempdata.json" => GetExtraTempGraphData(incremental, false),
				"extrahumdata.json" => GetExtraHumGraphData(incremental, false),
				"extradewdata.json" => GetExtraDewPointGraphData(incremental, false),
				"soiltempdata.json" => GetSoilTempGraphData(incremental, false),
				"soilmoistdata.json" => GetSoilMoistGraphData(incremental, false),
				"soilecdata.json" => GetSoilEcGraphData(incremental, false),
				"leafwetdata.json" => GetLeafWetnessGraphData(incremental, false),
				"usertempdata.json" => GetUserTempGraphData(incremental, false),
				"co2sensordata.json" => GetCo2SensorGraphData(incremental, false),
				"laserdepthdata.json" => GetLaserDepthGraphData(incremental, false),
				"snow24data.json" => GetSnow24hGraphData(incremental, false),
				_ => "{}",
			};
		}


	}
}
