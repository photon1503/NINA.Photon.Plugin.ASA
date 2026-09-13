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

        /// <summary>
        /// Records a sample. The graph history always keeps the sample so that the run-up to a
        /// slew stays visible, but the statistics only include it once the mount is tracking
        /// steadily again.
        /// </summary>
        public void Add(AxisReport report, bool includeInStatistics = true)
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
            if (includeInStatistics)
            {
                ErrorStatistics.Add(timestamp, report.PosErrArcsec);
            }
            LastReport = report;
            Rebuild();
        }

        /// <summary>
        /// Starts a fresh statistics epoch, marked in the graph by a vertical line, without
        /// discarding the plotted history.
        /// </summary>
        public void StartNewEpoch(DateTime timestamp)
        {
            ErrorStatistics.Clear();
            lock (lockObj)
            {
                epochMarkers.Add(timestamp);
            }
            Rebuild();
        }

        private readonly List<DateTime> epochMarkers = new List<DateTime>();

        private IList<double> epochMarkerOffsets = new List<double>();

        /// <summary>
        /// X positions, in seconds relative to now, at which a new statistics epoch began.
        /// </summary>
        public IList<double> EpochMarkerOffsets
        {
            get => epochMarkerOffsets;
            private set
            {
                epochMarkerOffsets = value;
                RaisePropertyChanged();
            }
        }

        private IList<DataPoint> epochMarkers2D = new List<DataPoint>();

        /// <summary>
        /// The epoch markers as a single broken line series, drawn against a dedicated 0..1 axis so
        /// each marker spans the full plot height without affecting the other axes' auto scaling.
        /// </summary>
        public IList<DataPoint> EpochMarkerSeries
        {
            get => epochMarkers2D;
            private set
            {
                epochMarkers2D = value;
                RaisePropertyChanged();
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                samples.Clear();
                epochMarkers.Clear();
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
            List<DateTime> markers;
            var now = DateTime.Now;
            var cutoff = now.AddSeconds(-HistorySeconds);
            lock (lockObj)
            {
                samples.RemoveAll(s => s.Timestamp < cutoff);
                epochMarkers.RemoveAll(m => m < cutoff);
                snapshot = samples.ToList();
                markers = epochMarkers.ToList();
            }

            ErrorStatistics.Update(now);

            CurrentHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.QCurr)).ToList();
            PositionErrorHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.PosErrArcsec)).ToList();
            VelocityHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.VelocityArcsecPerSecond)).ToList();
            EpochMarkerOffsets = markers.Select(m => -(now - m).TotalSeconds).ToList();
            EpochMarkerSeries = BuildEpochMarkerSeries(EpochMarkerOffsets);
            RaisePropertyChanged(nameof(TimeAxisMinimum));
        }

        /// <summary>
        /// Builds one vertical stroke per marker, separated by NaN points so OxyPlot breaks the
        /// line instead of connecting consecutive markers.
        /// </summary>
        private static IList<DataPoint> BuildEpochMarkerSeries(IList<double> offsets)
        {
            var points = new List<DataPoint>(offsets.Count * 3);
            foreach (var x in offsets)
            {
                points.Add(new DataPoint(x, 0.0d));
                points.Add(new DataPoint(x, 1.0d));
                points.Add(new DataPoint(double.NaN, double.NaN));
            }
            return points;
        }
    }
}
