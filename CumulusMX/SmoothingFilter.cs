using System;
using System.Collections.Generic;
using System.Linq;

namespace CumulusMX
{
	internal class SmoothingFilter
	{
		private readonly TimeSpan medianWindow;
		private readonly double timeConstantMinutes;
		private readonly double clipDelta;

		private readonly LinkedList<(DateTime ts, double value)> buffer = new LinkedList<(DateTime, double)>();

		private DateTime lastTimestamp;
		private double y;
		private bool initialised = false;

		/// <summary>
		/// Initializes a new instance of the SmoothingFilter class with the specified parameters for median window size, time
		/// constant, and clipping delta.
		/// </summary>
		/// <remarks>A larger median window produces smoother results but may introduce additional lag. The time
		/// constant controls the responsiveness of the exponential moving average, with higher values resulting in slower
		/// adaptation to changes. The clipping delta can be used to prevent abrupt changes in the filtered output.
		/// </remarks>
		/// <param name="medianMins">he time span (duration) of recent samples to include in the median filter window. Must be a positive TimeSpan representing the window length (for example, TimeSpan.FromMinutes(5)).</param>
		/// <param name="timeConstantMinutes">The time constant for the exponential moving average, expressed in minutes.
		/// Controls how quickly the smoothed value adapts to changes: smaller values make the filter more responsive, larger values make it slower. Must be a positive, non-zero value.</param>
		/// <param name="clipDelta">The maximum allowed change (in the same units as the input value) between the current smoothed value and the median-adjusted update in a single Update call.
		/// Use a large value to effectively disable clipping; a small value limits abrupt jumps.</param>
		public SmoothingFilter(TimeSpan medianMins, double timeConstantMinutes, double clipDelta)
		{
			this.medianWindow = medianMins;
			this.clipDelta = clipDelta;
			this.timeConstantMinutes = timeConstantMinutes;
		}

		/// <summary>
		/// Update the smoother with a new raw reading and timestamp
		/// Returns the smoothed value.
		/// </summary>
		public double Update(DateTime timestamp, double x)
		{
			if (!initialised)
			{
				y = x;
				lastTimestamp = timestamp;
				initialised = true;
			}

			// --- 1. Median update ---
			buffer.AddLast((timestamp, x));

			// Remove old entries
			DateTime cutoff = timestamp - medianWindow;
			while (buffer.First != null && buffer.First.Value.ts < cutoff)
				buffer.RemoveFirst();

			// Compute median of current window
			double m = ComputeMedian(buffer);

			// --- 2. Delta clipping ---
			double d = m - y;
			if (d > clipDelta)
				d = clipDelta;
			else if (d < -clipDelta)
				d = -clipDelta;

			// --- 3. Time‑correct EMA update ---
			double dtMinutes = (timestamp - lastTimestamp).TotalMinutes;
			if (dtMinutes < 0) dtMinutes = 0; // guard against clock issues

			double alpha = 1.0 - Math.Exp(-dtMinutes / timeConstantMinutes);

			y += alpha * d;

			lastTimestamp = timestamp;
			return y;
		}

		public double CurrentValue {  get { return y; } }

		private static double ComputeMedian(LinkedList<(DateTime ts, double value)> buf)
		{
			int count = buf.Count;
			if (count == 0)
				return 0;

			double[] arr = new double[count];
			int i = 0;
			foreach (var item in buf)
				arr[i++] = item.value;

			Array.Sort(arr);

			int mid = count / 2;
			if (count % 2 == 1)
				return arr[mid];
			else
				return 0.5 * (arr[mid - 1] + arr[mid]);
		}
	}
}
