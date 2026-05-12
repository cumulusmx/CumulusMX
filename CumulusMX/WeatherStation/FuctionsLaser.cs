using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoLaserDistance(double? value, int index, DateTime dataTime)
		{
			if (index > 0 && index < MetData.LaserDist.Length)
			{
				MetData.LaserDist[index] = value;

				// calculate depth?
				if (cumulus.LaserDepthBaseline[index] > -1)
				{
					DoLaserDepth(value.HasValue ? cumulus.LaserDepthBaseline[index] - value : value, index, dataTime);
				}
			}
		}

		/// <summary>
		/// Processes a single laser-distance sensor reading and updates snow-related state.
		///
		/// When `cumulus.SnowLogging` is enabled a dedicated NLog target is lazily created and debug information
		/// is written to a per-sensor debug file (`debug_snow_log{index}.txt`). Additional debug messages are
		/// emitted under `#if DEBUG`.
		///
		/// Side effects:
		/// - Mutates `LaserDepth`, `LastLaserSnowDepth`, `Snow24h`, `SnowSeason`, `snowSpikeTime`, and `lastSnowMinute`.
		/// - May create and write to `SnowLog`.
		/// </summary>
		/// <param name="value">Nullable laser distance reading (in the USER configured laser distance units).</param>
		/// <param name="index">Sensor channel index (must be > 0 and < `LaserDepth.Length`).</param>
		/// <param name="dataTime">Timestamp associated with the reading (passed into the smoothing filter).</param>
		public void DoLaserDepth(double? value, int index, DateTime dataTime)
		{
			if (index > 0 && index < MetData.LaserDepth.Length)
			{
				var logEntry = false;

				MetData.LaserDepth[index] = value;

				if (value.HasValue && cumulus.LaserIsSnowSensor[index])
				{
					if (!MetData.Snow24h[index].HasValue)
					{
						MetData.Snow24h[index] = 0;
					}

					if (!MetData.SnowSeason[index].HasValue)
					{
						MetData.SnowSeason[index] = 0;
					}

					// calculate a smoothed value of the depth
					var newDepth = SnowDepthAverage[index].Update(dataTime, value.Value);

					var lastDepth = MetData.LastLaserSnowDepth[index];
					var snowInc = 0.0;
					var laserFmtPlus1dp = "F" + (cumulus.LaserDPlaces + 1);

					if (!MetData.LastLaserSnowDepth[index].HasValue)
					{
						MetData.LastLaserSnowDepth[index] = newDepth;
						logEntry = true;
					}
					else
					{
						var multiplier = cumulus.Units.LaserDistance switch
						{
							0 => 10,  // cm
							1 => 100, // in
							2 => 1,  // mm
							_ => 1
						};

						// calculate the snowfall since the last increment - round towards zero in steps of the laser distance units
						var depthInc = Math.Truncate((newDepth - lastDepth.Value) * multiplier) / multiplier;

						if (Math.Round(depthInc, cumulus.LaserDPlaces) == 0)
						{
							// no change in depth
#if DEBUG
							cumulus.LogDebugMessage($"Laser #{index} No change in depth");
#endif
							snowSpikeTime = DateTime.UtcNow;
						}
						else if (depthInc < -cumulus.SnowDepthMinInc || (newDepth <= 0 && MetData.LastLaserSnowDepth[index] != newDepth))
						{
							// decrease the last depth if less than -minIncrement or we have reached the baseline
							MetData.LastLaserSnowDepth[index] = newDepth;
							if (cumulus.SnowLogging)
							{
								cumulus.LogDebugMessage($"Laser #{index} snow depth decreased to: {MetData.LastLaserSnowDepth[index].Value.ToString(laserFmtPlus1dp)} {cumulus.Units.LaserDistanceText}");
							}
							logEntry = true;

							snowSpikeTime = DateTime.UtcNow;
						}
						else if (depthInc >= cumulus.SnowDepthMinInc)
						{
							if (depthInc < cumulus.Spike.SnowDiff)
							{
								snowInc = ConvertUnits.LaserToSnow(depthInc);
								MetData.Snow24h[index] = (MetData.Snow24h[index] ?? 0) + snowInc;
								MetData.SnowSeason[index] = (MetData.SnowSeason[index] ?? 0) + snowInc;
								MetData.LastLaserSnowDepth[index] = newDepth;
								if (cumulus.SnowLogging)
								{
									cumulus.LogDebugMessage($"Laser #{index} depth increase added to snow accumulation: {depthInc.ToString(cumulus.LaserFormat)}, new value: {newDepth.ToString(cumulus.LaserFormat)} {cumulus.Units.LaserDistanceText}");
								}
								snowSpikeTime = DateTime.UtcNow;
								logEntry = true;
							}
							else
							{
								cumulus.LogSpikeRemoval($"Laser #{index} depth increase is greater than allowed for snow accumulation: {depthInc.ToString(cumulus.LaserFormat)}, max: {cumulus.Spike.SnowDiff} {cumulus.Units.LaserDistanceText}");

								// If we get a spike value for more 400 seconds, then rebaseline on the new value
								if ((DateTime.UtcNow - snowSpikeTime).TotalSeconds > 400)
								{
									cumulus.LogWarningMessage($"Laser #{index} has had an increase above the spike level for six or more consecutive readings. Rebaselining on the new value: {newDepth.ToString(cumulus.LaserFormat)} was: {(MetData.LastLaserSnowDepth[index] ?? 0).ToString(cumulus.LaserFormat)}");
									snowSpikeTime = DateTime.UtcNow;
									MetData.LastLaserSnowDepth[index] = newDepth;
									logEntry = true;
								}
							}
						}
						else
						{
							if (cumulus.SnowLogging)
							{
								cumulus.LogDebugMessage($"Laser #{index} depth change is less than required for snow accumulation: {depthInc.ToString(cumulus.LaserFormat)}, min = {cumulus.SnowDepthMinInc} {cumulus.Units.LaserDistanceText}");
							}
						}
					}

					if (cumulus.SnowLogging)
					{
						if (SnowLog == null)
						{
							// create the logger
							var logfile = new NLog.Targets.FileTarget("snowlogFile")
							{
								FileName = Path.Combine(Program.MxDiagsPath, $"debug_snow_log{index}.txt"),
								ArchiveAboveSize = 2097152,
								ArchiveOldFileOnStartup = true,
								//MaxArchiveFiles = 5,
								Layout = "${message}",
								Header = "time,dist,depth,avg_snowdepth,last_snowdepth,new_snowdepth,depth_added,accum_24,accum_yr,min_increment,median_mins,time_const,clip_delta"
							};

							var asyncLogFile = new NLog.Targets.Wrappers.AsyncTargetWrapper(logfile)
							{
								Name = "AsyncSnowLogfile",
								OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
								QueueLimit = 10000,
								BatchSize = 200,
								TimeToSleepBetweenBatches = 1
							};

							NLog.LogManager.Configuration.AddTarget(asyncLogFile);

							LogManager.Configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, asyncLogFile, "snowlog");

							var serviceProvider = new ServiceCollection()
								.AddLogging(loggingBuilder =>
								{
									loggingBuilder.ClearProviders();
									loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
									loggingBuilder.AddNLog();
								})
								 .BuildServiceProvider();

							NLog.LogManager.ReconfigExistingLoggers();
							SnowLog = LogManager.GetLogger("snowlog");
						}

						// debug logging of the values
						if (DateTime.Now.Minute != lastSnowMinute || logEntry)
						{
							lastSnowMinute = DateTime.Now.Minute;
							try
							{
								SnowLog.Info(
									string.Join(',', [
										DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
										MetData.LaserDist[index].ToFixed(cumulus.LaserFormat, "-"),
										value.ToFixed(laserFmtPlus1dp),
										newDepth.ToFixed(laserFmtPlus1dp),
										lastDepth.ToFixed(laserFmtPlus1dp , ""),
										MetData.LastLaserSnowDepth[index].ToFixed(laserFmtPlus1dp, ""),
										snowInc.ToFixed(cumulus.SnowFormat),
										MetData.Snow24h[index].ToFixed(cumulus.SnowFormat, "-"),
										MetData.SnowSeason[index].ToFixed(cumulus.SnowFormat, "-"),
										cumulus.SnowDepthMinInc.ToFixed(laserFmtPlus1dp),
										SnowDepthAverage[index].MedianWindow.ToFixed("F1"),
										SnowDepthAverage[index].TimeConst.ToFixed("F1"),
										SnowDepthAverage[index].ClipDelta.ToFixed("F2")
									])
								);
							}
							catch (Exception ex)
							{
								cumulus.LogExceptionMessage(ex, "Error creating snow depth debug log");
							}
						}
					}
				}
			}
		}

	}
}
