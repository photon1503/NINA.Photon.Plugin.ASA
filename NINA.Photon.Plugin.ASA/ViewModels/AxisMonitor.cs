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
using OxyPlot.Series;
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

        private bool showCurrent = true;

        /// <summary>
        /// Whether the current series and its axis are shown. This helps declutter the graph when
        /// the motor current is not relevant to the current observation.
        /// </summary>
        public bool ShowCurrent
        {
            get => showCurrent;
            set
            {
                if (showCurrent != value)
                {
                    showCurrent = value;
                    RaisePropertyChanged();
                }
            }
        }

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

        private double errorScaleArcsec;

        /// <summary>
        /// Fixed +/- scale of the position error axis in arcseconds. Zero means auto scale.
        /// </summary>
        public double ErrorScaleArcsec
        {
            get => errorScaleArcsec;
            set
            {
                if (errorScaleArcsec != value)
                {
                    errorScaleArcsec = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ErrorAxisMinimum));
                    RaisePropertyChanged(nameof(ErrorAxisMaximum));
                }
            }
        }

        /// <summary>
        /// Lower bound of the error axis, or NaN to let OxyPlot scale it to the data.
        /// </summary>
        public double ErrorAxisMinimum => errorScaleArcsec > 0 ? -errorScaleArcsec : double.NaN;

        public double ErrorAxisMaximum => errorScaleArcsec > 0 ? errorScaleArcsec : double.NaN;

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
        private readonly List<ExposureOverlay> exposureOverlays = new List<ExposureOverlay>();

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

        private IList<ExposureRectangleItem> exposureBackgrounds = new List<ExposureRectangleItem>();

        public IList<ExposureRectangleItem> ExposureBackgrounds
        {
            get => exposureBackgrounds;
            private set
            {
                exposureBackgrounds = value;
                RaisePropertyChanged();
            }
        }

        public void StartExposure(DateTime startTime, double? exposureSeconds)
        {
            lock (lockObj)
            {
                exposureOverlays.Add(new ExposureOverlay
                {
                    StartTime = startTime,
                    ExposureSeconds = exposureSeconds
                });
            }

            Rebuild();
        }

        public void CompleteExposure(DateTime? startTime, DateTime endTime)
        {
            lock (lockObj)
            {
                var overlay = FindExposureOverlay(startTime);
                if (overlay != null)
                {
                    overlay.EndTime = endTime;
                }
            }

            Rebuild();
        }

        public void UpdateExposureInfo(DateTime? startTime, double? exposureSeconds, string imageName, string filter, string fileName)
        {
            lock (lockObj)
            {
                var overlay = FindExposureOverlay(startTime);
                if (overlay != null)
                {
                    if (exposureSeconds.HasValue && exposureSeconds.Value > 0d)
                    {
                        overlay.ExposureSeconds = exposureSeconds.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(imageName))
                    {
                        overlay.ImageName = imageName;
                    }

                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        overlay.Filter = filter;
                    }

                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        overlay.FileName = fileName;
                    }
                }
            }

            Rebuild();
        }

        public void RefreshTimeOverlays()
        {
            Rebuild();
        }

        public void Clear()
        {
            lock (lockObj)
            {
                samples.Clear();
                epochMarkers.Clear();
                exposureOverlays.Clear();
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

        private double? lastImageRms;
        private DateTime? lastImageRmsTimestamp;

        public double? LastImageRms => lastImageRms;

        public double? LastImageRmsAgeSeconds
        {
            get
            {
                if (!lastImageRmsTimestamp.HasValue)
                {
                    return null;
                }

                return Math.Max(0d, (DateTime.Now - lastImageRmsTimestamp.Value).TotalSeconds);
            }
        }

        public void SetCapturedImageRms(double rms, DateTime timestamp)
        {
            lastImageRms = rms;
            lastImageRmsTimestamp = timestamp;
            RaisePropertyChanged(nameof(LastImageRms));
            RaisePropertyChanged(nameof(LastImageRmsAgeSeconds));
        }

        public void RefreshCaptureTiming()
        {
            if (!lastImageRmsTimestamp.HasValue)
            {
                return;
            }

            RaisePropertyChanged(nameof(LastImageRmsAgeSeconds));
        }

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
            List<ExposureOverlay> overlays;
            var now = DateTime.Now;
            var cutoff = now.AddSeconds(-HistorySeconds);
            lock (lockObj)
            {
                samples.RemoveAll(s => s.Timestamp < cutoff);
                epochMarkers.RemoveAll(m => m < cutoff);
                exposureOverlays.RemoveAll(o => (o.EndTime ?? now) < cutoff);
                snapshot = samples.ToList();
                markers = epochMarkers.ToList();
                overlays = exposureOverlays.Select(o => o.Clone()).ToList();
            }

            ErrorStatistics.Update(now);

            CurrentHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.QCurr)).ToList();
            PositionErrorHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.PosErrArcsec)).ToList();
            VelocityHistory = snapshot.Select(s => new DataPoint(-(now - s.Timestamp).TotalSeconds, s.Report.VelocityArcsecPerSecond)).ToList();
            EpochMarkerOffsets = markers.Select(m => -(now - m).TotalSeconds).ToList();
            EpochMarkerSeries = BuildEpochMarkerSeries(EpochMarkerOffsets);
            ExposureBackgrounds = BuildExposureBackgrounds(overlays, now);
            RaisePropertyChanged(nameof(TimeAxisMinimum));
        }

        private ExposureOverlay FindExposureOverlay(DateTime? startTime)
        {
            if (exposureOverlays.Count == 0)
            {
                return null;
            }

            if (!startTime.HasValue)
            {
                return exposureOverlays.LastOrDefault();
            }

            return exposureOverlays
                .OrderBy(o => Math.Abs((o.StartTime - startTime.Value).TotalMilliseconds))
                .FirstOrDefault();
        }

        private static IList<ExposureRectangleItem> BuildExposureBackgrounds(IEnumerable<ExposureOverlay> overlays, DateTime now)
        {
            return overlays
                .Select(o => new ExposureRectangleItem(
                    new DataPoint(-(now - o.StartTime).TotalSeconds, 0d),
                    new DataPoint(-(now - (o.EndTime ?? now)).TotalSeconds, 1d),
                    1d,
                    BuildExposureTooltip(o)))
                .ToList();
        }

        private static string BuildExposureTooltip(ExposureOverlay overlay)
        {
            var lines = new List<string> { "Exposure" };

            if (!string.IsNullOrWhiteSpace(overlay.ImageName))
            {
                lines.Add($"Image: {overlay.ImageName}");
            }

            if (overlay.ExposureSeconds.HasValue)
            {
                lines.Add($"Exposure: {overlay.ExposureSeconds.Value:0.0}s");
            }

            if (!string.IsNullOrWhiteSpace(overlay.Filter))
            {
                lines.Add($"Filter: {overlay.Filter}");
            }

            if (!string.IsNullOrWhiteSpace(overlay.FileName))
            {
                lines.Add($"File: {overlay.FileName}");
            }

            return string.Join(Environment.NewLine, lines);
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

        public sealed class ExposureRectangleItem : RectangleItem
        {
            public ExposureRectangleItem(DataPoint a, DataPoint b, double value, string title)
                : base(a, b, value)
            {
                Title = title;
            }

            public string Title { get; }
        }

        private sealed class ExposureOverlay
        {
            public DateTime StartTime { get; set; }

            public DateTime? EndTime { get; set; }

            public double? ExposureSeconds { get; set; }

            public string ImageName { get; set; }

            public string Filter { get; set; }

            public string FileName { get; set; }

            public ExposureOverlay Clone()
            {
                return new ExposureOverlay
                {
                    StartTime = StartTime,
                    EndTime = EndTime,
                    ExposureSeconds = ExposureSeconds,
                    ImageName = ImageName,
                    Filter = Filter,
                    FileName = FileName
                };
            }
        }
    }
}
