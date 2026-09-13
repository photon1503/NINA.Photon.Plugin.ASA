#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Equipment.Equipment;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NINA.Equipment.Interfaces.ViewModel;

namespace NINA.Photon.Plugin.ASA.ViewModels
{
    [Export(typeof(IDockableVM))]
    public class MountInfoVM : DockableVM, IMountInfoVM, ITelescopeConsumer
    {
        private readonly IMount mount;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IASAOptions options;
        private readonly DispatcherTimer updateTimer;
        private bool disposed = false;
        private bool reportingEnabled = false;
        private int updateInProgress = 0;

        [ImportingConstructor]
        public MountInfoVM(IProfileService profileService, ITelescopeMediator telescopeMediator) :
            this(profileService, telescopeMediator, ASAPlugin.Mount, ASAPlugin.ASAOptions)
        {
        }

        public MountInfoVM(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            IMount mount,
            IASAOptions options) : base(profileService)
        {
            this.Title = "ASA Mount Info";
            this.mount = mount;
            this.telescopeMediator = telescopeMediator;
            this.options = options;

            var dict = new ResourceDictionary();
            dict.Source = new Uri("NINA.Photon.Plugin.ASA;component/Resources/SVGDataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ASAMountInfoSVG"];
            ImageGeometry.Freeze();

            this.Axis1 = new AxisMonitor(1, "Axis 1 (RA)") { HistorySeconds = options.MountInfoHistorySeconds };
            this.Axis2 = new AxisMonitor(2, "Axis 2 (DEC)") { HistorySeconds = options.MountInfoHistorySeconds };

            this.HistoryChoices = new ObservableCollection<int> { 30, 60, 120, 300, 600 };
            this.RefreshIntervalChoices = new ObservableCollection<double> { 0.2d, 0.5d, 1.0d, 2.0d, 5.0d, 10.0d };
            EnsureChoice(this.HistoryChoices, options.MountInfoHistorySeconds);
            EnsureChoice(this.RefreshIntervalChoices, options.MountInfoRefreshIntervalSeconds);

            this.updateTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) { Interval = GetPollInterval() };
            this.updateTimer.Tick += UpdateTimer_Tick;

            if (this.options is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += Options_PropertyChanged;
            }

            this.telescopeMediator.RegisterConsumer(this);
            this.monitoringVisible = base.IsVisible;
            this.PropertyChanged += MountInfoVM_PropertyChanged;
        }

        private bool monitoringEnabled = true;

        /// <summary>
        /// User toggle to start/stop polling the mount without having to close the dock.
        /// </summary>
        public bool MonitoringEnabled
        {
            get => monitoringEnabled;
            set
            {
                if (monitoringEnabled != value)
                {
                    monitoringEnabled = value;
                    RaisePropertyChanged();
                    EvaluateMonitoringState();
                }
            }
        }

        /// <summary>
        /// The total error is the vector sum of both axis errors, so it needs both axis reports
        /// from the same poll cycle.
        /// </summary>
        private void UpdateTotalStatistics(AxisReport axis1, AxisReport axis2)
        {
            var now = DateTime.Now;
            if (axis1 != null && axis2 != null)
            {
                var total = Math.Sqrt((axis1.PosErrArcsec * axis1.PosErrArcsec) + (axis2.PosErrArcsec * axis2.PosErrArcsec));
                TotalErrorArcsec = total;
                TotalErrorStatistics.Add(now, total);
            }
            TotalErrorStatistics.Update(now);
        }

        private void EvaluateMonitoringState()
        {
            if (MonitoringEnabled && monitoringVisible && TelescopeConnected)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        private void MountInfoVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsVisible))
            {
                OnVisibilityChanged();
            }
        }

        public AxisMonitor Axis1 { get; }

        public AxisMonitor Axis2 { get; }

        /// <summary>
        /// Combined (total) position error statistics, computed from the vector sum of both axes.
        /// </summary>
        public ErrorStatisticsTracker TotalErrorStatistics { get; } = new ErrorStatisticsTracker();

        private double totalErrorArcsec = double.NaN;

        /// <summary>
        /// Current total position error, i.e. the vector sum of both axis errors.
        /// </summary>
        public double TotalErrorArcsec
        {
            get => totalErrorArcsec;
            private set
            {
                if (totalErrorArcsec != value)
                {
                    totalErrorArcsec = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<int> HistoryChoices { get; }

        public ObservableCollection<double> RefreshIntervalChoices { get; }

        /// <summary>
        /// Interval in seconds at which the mount refreshes its report values and at which the dock polls them.
        /// </summary>
        public double RefreshIntervalSeconds
        {
            get => options.MountInfoRefreshIntervalSeconds;
            set
            {
                if (value >= 0 && options.MountInfoRefreshIntervalSeconds != value)
                {
                    options.MountInfoRefreshIntervalSeconds = value;
                }
            }
        }

        /// <summary>
        /// Makes sure a value configured in the plugin options is selectable in the dock dropdown.
        /// </summary>
        private static void EnsureChoice<T>(ObservableCollection<T> choices, T current) where T : IComparable<T>
        {
            if (current.CompareTo(default) <= 0 || choices.Contains(current))
            {
                return;
            }

            var index = 0;
            while (index < choices.Count && choices[index].CompareTo(current) < 0)
            {
                index++;
            }
            choices.Insert(index, current);
        }

        private TimeSpan GetPollInterval()
        {
            var seconds = options.MountInfoRefreshIntervalSeconds;
            if (seconds <= 0)
            {
                seconds = 1.0d;
            }
            return TimeSpan.FromSeconds(seconds);
        }

        public int HistorySeconds
        {
            get => options.MountInfoHistorySeconds;
            set
            {
                if (value > 0 && options.MountInfoHistorySeconds != value)
                {
                    options.MountInfoHistorySeconds = value;
                }
            }
        }

        private bool telescopeConnected;

        public bool TelescopeConnected
        {
            get => telescopeConnected;
            private set
            {
                if (telescopeConnected != value)
                {
                    telescopeConnected = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string errorMessage;

        public string ErrorMessage
        {
            get => errorMessage;
            private set
            {
                if (errorMessage != value)
                {
                    errorMessage = value;
                    RaisePropertyChanged();
                }
            }
        }

        public override bool IsTool { get; } = true;

        private void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IASAOptions.MountInfoHistorySeconds))
            {
                Axis1.HistorySeconds = options.MountInfoHistorySeconds;
                Axis2.HistorySeconds = options.MountInfoHistorySeconds;
                EnsureChoice(HistoryChoices, options.MountInfoHistorySeconds);
                RaisePropertyChanged(nameof(HistorySeconds));
            }
            else if (e.PropertyName == nameof(IASAOptions.MountInfoRefreshIntervalSeconds))
            {
                EnsureChoice(RefreshIntervalChoices, options.MountInfoRefreshIntervalSeconds);
                RaisePropertyChanged(nameof(RefreshIntervalSeconds));
                ApplyRefreshInterval();
            }
        }

        private void ApplyRefreshInterval()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)ApplyRefreshInterval);
                return;
            }

            updateTimer.Interval = GetPollInterval();
            if (reportingEnabled)
            {
                SendRefreshInterval();
            }
        }

        private void SendRefreshInterval()
        {
            try
            {
                mount.SetReportRefreshInterval(options.MountInfoRefreshIntervalSeconds);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set ASA mount report refresh interval: {ex.Message}");
                ErrorMessage = ex.Message;
            }
        }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo)
        {
            TelescopeConnected = deviceInfo.Connected;
            UpdateMountState(deviceInfo.Connected && deviceInfo.Slewing, deviceInfo.Connected && deviceInfo.TrackingEnabled);
            EvaluateMonitoringState();
        }

        private bool telescopeSlewing;
        private bool telescopeTracking;
        private bool mountStateKnown;
        private DateTime? settleUntil;

        /// <summary>
        /// A slew, or any period without tracking, makes the position error meaningless and would
        /// otherwise dominate the longer statistics windows for minutes. The statistics are
        /// therefore restarted whenever the mount begins tracking again. The plotted history is
        /// deliberately kept so the run-up to the event stays visible.
        /// </summary>
        private void UpdateMountState(bool slewing, bool tracking)
        {
            var wasPaused = StatisticsPaused;
            var slewingChanged = telescopeSlewing != slewing;
            var trackingChanged = telescopeTracking != tracking;
            if (!slewingChanged && !trackingChanged && mountStateKnown)
            {
                return;
            }

            mountStateKnown = true;

            telescopeSlewing = slewing;
            telescopeTracking = tracking;

            if (slewing || !tracking)
            {
                // Disturbed: hold the statistics until the mount is tracking steadily again.
                settleUntil = null;
            }
            else
            {
                // Settled tracking has (re)started, so schedule a fresh epoch.
                settleUntil = DateTime.Now.AddSeconds(Math.Max(0, options.MountInfoSlewSettleSeconds));
            }

            if (slewingChanged)
            {
                RaisePropertyChanged(nameof(IsSlewing));
            }
            if (trackingChanged)
            {
                RaisePropertyChanged(nameof(IsTracking));
            }
            if (wasPaused != StatisticsPaused)
            {
                RaisePropertyChanged(nameof(StatisticsPaused));
            }
            RaisePropertyChanged(nameof(StatisticsPausedReason));
        }

        /// <summary>
        /// True while the position error is not meaningful, i.e. while slewing, while not tracking,
        /// or during the settle time after tracking resumes.
        /// </summary>
        public bool StatisticsPaused => telescopeSlewing || !telescopeTracking || (settleUntil.HasValue && DateTime.Now < settleUntil.Value);

        public string StatisticsPausedReason
            => telescopeSlewing ? "Slewing - statistics paused"
                : !telescopeTracking ? "Not tracking - statistics paused"
                : "Settling - statistics paused";

        public bool IsSlewing => telescopeSlewing;

        public bool IsTracking => telescopeTracking;

        /// <summary>
        /// Restarts the statistics and marks the graphs with a vertical line, keeping the
        /// plotted history intact.
        /// </summary>
        private void StartNewEpoch()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)StartNewEpoch);
                return;
            }

            var timestamp = DateTime.Now;
            Axis1.StartNewEpoch(timestamp);
            Axis2.StartNewEpoch(timestamp);
            TotalErrorStatistics.Clear();
            TotalErrorArcsec = double.NaN;
        }

        private void ResetHistory()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)ResetHistory);
                return;
            }

            Axis1.Clear();
            Axis2.Clear();
            TotalErrorStatistics.Clear();
            TotalErrorArcsec = double.NaN;
        }

        private bool monitoringVisible;

        /// <summary>
        /// Set by NINA when the dock becomes (in)visible. Reporting is only enabled while the dock is shown.
        /// </summary>
        private void OnVisibilityChanged()
        {
            if (monitoringVisible == base.IsVisible)
            {
                return;
            }

            monitoringVisible = base.IsVisible;
            EvaluateMonitoringState();
        }

        private void StartMonitoring()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)StartMonitoring);
                return;
            }

            if (updateTimer.IsEnabled)
            {
                return;
            }

            try
            {
                mount.SetReporting(true);
                reportingEnabled = true;
                ErrorMessage = null;
                SendRefreshInterval();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enable ASA mount reporting: {ex.Message}");
                ErrorMessage = ex.Message;
            }
            mountStateKnown = false;
            ResetHistory();
            updateTimer.Interval = GetPollInterval();
            updateTimer.Start();
        }

        private void StopMonitoring()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)StopMonitoring);
                return;
            }

            if (updateTimer.IsEnabled)
            {
                updateTimer.Stop();
            }

            if (reportingEnabled)
            {
                reportingEnabled = false;
                try
                {
                    mount.SetReporting(false);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to disable ASA mount reporting: {ex.Message}");
                }
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref updateInProgress, 1, 0) != 0)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    var axis1 = mount.GetAxisReport(1)?.Value;
                    var axis2 = mount.GetAxisReport(2)?.Value;
                    Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        var paused = StatisticsPaused;
                        if (!paused && settleUntil.HasValue)
                        {
                            // Settling just finished: begin a fresh statistics epoch and mark it
                            // in the graphs, without discarding the plotted history.
                            settleUntil = null;
                            StartNewEpoch();
                            RaisePropertyChanged(nameof(StatisticsPaused));
                        }
                        else if (paused)
                        {
                            // Keep the banner in sync as the settle timer expires.
                            RaisePropertyChanged(nameof(StatisticsPaused));
                        }

                        // The graph always keeps the sample; only the statistics are gated.
                        Axis1.Add(axis1, !paused);
                        Axis2.Add(axis2, !paused);
                        if (!paused)
                        {
                            UpdateTotalStatistics(axis1, axis2);
                        }
                    }));
                    ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to read ASA mount axis report: {ex.Message}");
                    ErrorMessage = ex.Message;
                }
                finally
                {
                    Interlocked.Exchange(ref updateInProgress, 0);
                }
            });
        }

        public void Dispose()
        {
            if (!disposed)
            {
                StopMonitoring();
                updateTimer.Tick -= UpdateTimer_Tick;
                if (this.options is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged -= Options_PropertyChanged;
                }
                telescopeMediator.RemoveConsumer(this);
                this.PropertyChanged -= MountInfoVM_PropertyChanged;
                disposed = true;
            }
        }
    }
}
