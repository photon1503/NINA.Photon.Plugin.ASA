#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NINA.Photon.Plugin.ASA.ViewModels
{
    /// <summary>
    /// Max, trailing average and RMS deviation of a position error series over a fixed time window.
    /// All values are in arcseconds.
    /// </summary>
    public class ErrorStatistics : BaseINPC
    {
        public ErrorStatistics(string label, double windowSeconds)
        {
            Label = label;
            WindowSeconds = windowSeconds;
        }

        public string Label { get; }

        public double WindowSeconds { get; }

        private double max = double.NaN;

        public double Max
        {
            get => max;
            private set
            {
                if (max != value)
                {
                    max = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double average = double.NaN;

        /// <summary>
        /// Trailing average of the absolute error. Comparable to the ASA driver's "Average"
        /// column, and consistent between a single axis and the combined total.
        /// </summary>
        public double Average
        {
            get => average;
            private set
            {
                if (average != value)
                {
                    average = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double bias = double.NaN;

        /// <summary>
        /// Signed trailing mean. A non-zero value indicates a systematic tracking offset rather
        /// than random noise. Always zero-ish for the combined total, which has no sign.
        /// </summary>
        public double Bias
        {
            get => bias;
            private set
            {
                if (bias != value)
                {
                    bias = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double rms = double.NaN;

        public double RMS
        {
            get => rms;
            private set
            {
                if (rms != value)
                {
                    rms = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int sampleCount;

        public int SampleCount
        {
            get => sampleCount;
            private set
            {
                if (sampleCount != value)
                {
                    sampleCount = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(HasData));
                }
            }
        }

        public bool HasData => sampleCount > 0;

        internal void Update(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                SampleCount = 0;
                Max = double.NaN;
                Average = double.NaN;
                Bias = double.NaN;
                RMS = double.NaN;
                return;
            }

            var maxValue = 0.0d;
            var sumOfMagnitudes = 0.0d;
            var signedSum = 0.0d;
            var sumOfSquares = 0.0d;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                var magnitude = Math.Abs(value);
                if (magnitude > maxValue)
                {
                    maxValue = magnitude;
                }
                sumOfMagnitudes += magnitude;
                signedSum += value;
                sumOfSquares += value * value;
            }

            SampleCount = values.Count;
            Max = maxValue;
            Average = sumOfMagnitudes / values.Count;
            Bias = signedSum / values.Count;
            RMS = Math.Sqrt(sumOfSquares / values.Count);
        }
    }

    /// <summary>
    /// Keeps a rolling window of position error samples and derives max / trailing average / RMS
    /// statistics over several time windows.
    /// </summary>
    public class ErrorStatisticsTracker
    {
        private static readonly (string Label, double Seconds)[] Windows =
        {
            ("10s", 10.0d),
            ("60s", 60.0d),
            ("5min", 300.0d)
        };

        private readonly List<(DateTime Timestamp, double Value)> samples = new List<(DateTime, double)>();
        private readonly object lockObj = new object();

        public ErrorStatisticsTracker()
        {
            Statistics = new ObservableCollection<ErrorStatistics>(Windows.Select(w => new ErrorStatistics(w.Label, w.Seconds)));
        }

        public ObservableCollection<ErrorStatistics> Statistics { get; }

        /// <summary>
        /// Longest window that has to be retained for statistics.
        /// </summary>
        public static double MaxWindowSeconds => Windows.Max(w => w.Seconds);

        public void Add(DateTime timestamp, double value)
        {
            if (double.IsNaN(value))
            {
                return;
            }

            lock (lockObj)
            {
                samples.Add((timestamp, value));
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                samples.Clear();
            }
            foreach (var statistic in Statistics)
            {
                statistic.Update(null);
            }
        }

        /// <summary>
        /// Drops expired samples and recomputes the statistics for every window.
        /// </summary>
        public void Update(DateTime now)
        {
            List<(DateTime Timestamp, double Value)> snapshot;
            var cutoff = now.AddSeconds(-MaxWindowSeconds);
            lock (lockObj)
            {
                samples.RemoveAll(s => s.Timestamp < cutoff);
                snapshot = samples.ToList();
            }

            foreach (var statistic in Statistics)
            {
                var windowStart = now.AddSeconds(-statistic.WindowSeconds);
                var values = snapshot.Where(s => s.Timestamp >= windowStart).Select(s => s.Value).ToList();
                statistic.Update(values);
            }
        }
    }
}
