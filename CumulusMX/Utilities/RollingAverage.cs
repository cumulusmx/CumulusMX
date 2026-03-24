using System;
using System.Collections.Generic;
using System.Linq;

namespace CumulusMX.Utilities
{
	public class RollingAverage
	{
		private readonly TimeSpan _window;
		private readonly Queue<(DateTime Timestamp, double Value)> _samples = new();

		public RollingAverage(TimeSpan window)
		{
			if (window <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

			_window = window;
		}

		public void AddSample(double value)
		{
			var now = DateTime.UtcNow;
			_samples.Enqueue((now, value));

			// Remove samples outside the window
			while (_samples.Count > 0 && now - _samples.Peek().Timestamp > _window)
			{
				_samples.Dequeue();
			}
		}

		public double GetAverage()
		{
			if (_samples.Count == 0)
				return double.NaN;

			return _samples.Average(s => s.Value);
		}

		public double GetAverage(TimeSpan period)
		{
			if (period <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(period), "Period must be positive.");

			if (_samples.Count == 0)
				return double.NaN;

			// Anchor to the timestamp of the last entry
			var lastTimestamp = _samples.Last().Timestamp;
			var cutoff = lastTimestamp - period;

			var subset = _samples.Where(s => s.Timestamp >= cutoff);

			if (!subset.Any())
				return double.NaN;

			return subset.Average(s => s.Value);
		}
	}
}
