using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CumulusMX.LogFileConverter
{
	internal static class AddUnixTimestamp
	{
		static List<DateTime> AmbiguousDates;

		public static void ProcessLogFiles(string dataDirectory, DateTime start)
		{
			try
			{
				DateTime now = DateTime.Now;

				// Generate all yyyymm strings from start to now
				var validPatterns = Enumerable.Range(0, (now.Year - start.Year) * 12 + now.Month - start.Month + 1)
					.Select(offset => start.AddMonths(offset).ToString("yyyyMM"))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				var matchingFiles = Directory.EnumerateFiles(dataDirectory)
					.Where(file =>
					{
						string name = Path.GetFileName(file);
						foreach (var yyyymm in validPatterns)
						{
							if (name.Equals($"{yyyymm}log.txt", StringComparison.OrdinalIgnoreCase) ||
								name.Equals($"ExtraLog{yyyymm}.txt", StringComparison.OrdinalIgnoreCase) ||
								name.Equals($"AirLink{yyyymm}log.txt", StringComparison.OrdinalIgnoreCase))
							{
								return true;
							}
						}
						return false;
					});

				foreach (var file in matchingFiles)
				{
					ProcessSingleFile(file);
				}
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "ProcessLogFiles: Error conversion processing log files");
			}
		}

		private static void ProcessSingleFile(string filePath)
		{
			var modified = false;

			try
			{
				Program.cumulus.LogMessage($"Conversion Processing file: {filePath}");
				var lines = File.ReadAllLines(filePath).ToList();
				var modifiedLines = new List<string>();

				AmbiguousDates = new List<DateTime>();
				
				foreach (var line in lines)
				{
					var modifiedLine = ProcessLine(line);
					if (modifiedLine != null)
					{
						modifiedLines.Add(modifiedLine);
					}

					modified |= modifiedLine != line;
				}

				// Create backup
				if (modified)
				{
					var fileName = Path.GetFileName(filePath);
					string backupPath = Path.Combine(Program.cumulus.ProgramOptions.BackupPath, "ConvertBackup");
					if (!Path.Exists(backupPath))
						Directory.CreateDirectory(backupPath);

					File.Copy(filePath, Path.Combine(backupPath, Path.GetFileName(filePath)), true);

					// Write modified content
					File.WriteAllLines(filePath, modifiedLines);
				}
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "ProcessSingleFile: Error processing file {filePath}");
			}
		}

		private static string ProcessLine(string line)
		{
			try
			{
				var parts = line.Split(',');
				if (parts.Length < 2) return line;

				// already converted?
				if (int.TryParse(parts[1], out _)) return line;

				// Assuming date is in first column and time in second column
				var dateTime = Utils.ddmmyyhhmmStrToLocalDate(parts[0], parts[1], ref AmbiguousDates);

				if (dateTime != DateTime.MinValue)
				{ 
					// Convert to Unix timestamp
					var unixTimestamp = dateTime.ToUnixTime();
					
					// Merge date and time, add unix ts, add original fileds after original time field
					var newParts = new[] { $"{parts[0]} {parts[1]}" }
						.Concat(new[] { unixTimestamp.ToString() })
						.Concat(parts.Skip(2));
					
					return string.Join(",", newParts);
				}

				return line;
			}
			catch
			{
				return line;
			}
		}
	}
}
