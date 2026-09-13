#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Photon.Plugin.ASA.Model;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Photon.Plugin.ASA.ViewModels
{
    /// <summary>
    /// Holds the rolling history of the report values of a single mount axis and
    /// exposes them as OxyPlot data point collections.
    /// </summary>
    public class AxisMonitor : BaseINPC
    {
        private readonly List<(DateTime Timestamp, AxisReport Report)> samples = new List<(DateTime, AxisReport)>();
        private readonly object lockObj = new object();

        public AxisMonitor(int axis, string title)
        {
            Axis = axis;
            Title = title;
        }

        public int Axis { get; }

        /// <summary>
        /// Rolling max / trailing average / RMS deviation of the position error for this axis.
        /// </summary>
        public ErrorStatisticsTracker ErrorStatistics { get; } = new ErrorStatisticsTracker();

        public string Title { get; }

        private bool showVelocity = true;

        /// <summary>
        /// Whether the velocity series and its axis are shown. The velocity scale differs greatly
        /// from the error scale, so it can be hidden to declutter the graph.
        /// </summary>
        public bool ShowVelocity
        {
            get => showVelocity;
            set
            {
                if (showVelocity != value)
                {
                    showVelocity = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int historySeconds = 60;

        public int HistorySeconds
        {
            get => historySeconds;
            set
            {
                if (historySeconds != value)
                {
                    historySeconds = value;
                    RaisePropertyChanged();
                    Rebuild();
                }
            }
        }

        public void Add(AxisReport report)
        {
            if (report == null)
            {
                return;
            }

            var timestamp = DateTime.Now;
            lock (lockObj)
            {
                samples.Add((timestamp, report));
            }
            ErrorStatistics.Add(timestamp, report.PosErrArcsec);
            LastReport = report;
            Rebuild();
        }

        /// <summary>
        /// Updates the live readout without recording the sample in the history or statistics.
        /// Used while the mount is slewing, when the position error is not meaningful.
        /// </summary>
        public void SetLiveOnly(AxisReport report)
        {
            if (report == null)
            {
                return;
            }

            LastReport = report;
        }

        public void Clear()
        {
            lock (lockObj)
            {
                samples.Clear();
            }
            ErrorStatistics.Clear();
            LastReport = null;
            Rebuild();
        }

        private AxisReport lastReport;

        public AxisReport LastReport
        {
            get => lastReport;
            private set
            {
                lastReport = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Current));
                RaisePropertyChanged(nameof(PositionErrorArcsec));
                RaisePropertyChanged(nameof(EncoderPositionDegrees));
                RaisePropertyChanged(nameof(VelocityArcsecPerSecond));
                RaisePropertyChanged(nameof(LastUpdate));
            }
        }

        public double Current => lastReport?.QCurr ?? double.NaN;

        public double PositionErrorArcsec => lastReport?.PosErrArcsec ?? double.NaN;

        public double EncoderPositionDegrees => lastReport?.EncPosDegrees ?? double.NaN;

        /// <summary>
        /// Axis velocity in arcseconds per second. Sidereal rate is about 15.04.
        /// </summary>
        public double VelocityArcsecPerSecond => lastReport?.VelocityArcsecPerSecond ?? double.NaN;

        public DateTime? LastUpdate => lastReport?.LastTime;

        private IList<DataPoint> currentHistory = new List<DataPoint>();

        public IList<DataPoint> CurrentHistory
        {
            get => currentHistory;
            private set
            {
                currentHistory = value;
                RaisePropertyChanged();
            }
        }

        private IList<DataPoint> positionErrorHistory = new List<DataPoint>();

        public IList<DataPoint> PositionErrorHistory
        {
            get => positionErrorHistory;
            private set
            {
                positionErrorHistory = value;
                RaisePropertyChanged();
            }
        }

        private IList<DataPoint> velocityHistory = new List<DataPoint>();

        public IList<DataPoint> VelocityHistory
        {
            get => velocityHistory;
            private set
            {
                velocityHistory = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// X axis minimum, in seconds relative to now. The newest sample is always at x = 0.
        /// </summary>
        public double TimeAxisMinimum => -HistorySeconds;

        private void Rebuild()
        {
            List<(DateTime Timestamp, AxisReport Report)> snapshot;
            var now = DateTime.Now;
            var cutoff = now.AddSeconds(-HistorySeconds);
            lock (lockObj)
            {
                samples.RemoveAll(s => s.Timestamp < cutoff);
                snapshot = samples.ToList();
            }

            ErrorStatistics.Update(now);

            CurrentHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.QCurr)).ToList();
            PositionErrorHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.PosErrArcsec)).ToList();
            VelocityHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.VelocityArcsecPerSecond)).ToList();
            RaisePropertyChanged(nameof(TimeAxisMinimum));
        }
    }
}
