using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CumulusMX.LogFileConverter
{
	internal static class AddUnixTimestamp
	{
		static List<DateTime> AmbiguousDates;

		public static void ProcessLogFiles(string dataDirectory)
		{
			try
			{
				var now = int.Parse(DateTime.Now.ToString("yyyyMM"));

				// Find all matching log files
				var logFiles = Directory.GetFiles(dataDirectory, "*log.txt")
					.Where(f => Path.GetFileName(f).Length == 13 && 
							   Path.GetFileName(f).EndsWith("log.txt") &&
							   int.TryParse(Path.GetFileName(f).Substring(0, 6), out int dat) &&
							   dat > 200001 && dat <= now);

				foreach (var file in logFiles)
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
					
					// Merge date and time, remove original time field
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
