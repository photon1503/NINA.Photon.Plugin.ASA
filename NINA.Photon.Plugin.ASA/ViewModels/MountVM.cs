#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Windows.Input;
using NINA.Photon.Plugin.ASA.Model;
using System.Windows;
using NINA.Equipment.Interfaces;
using System.Threading;
using System.Net;
using NINA.Core.Model;
using NINA.WPF.Base.Interfaces.Mediator;
using System.Windows.Threading;

namespace NINA.Photon.Plugin.ASA.ViewModels {

    [Export(typeof(IDockableVM))]
    public class MountVM : DockableVM, IMountVM, ITelescopeConsumer {

        private readonly SynchronizationContext synchronizationContext =
            Application.Current?.Dispatcher != null
            ? new DispatcherSynchronizationContext(Application.Current.Dispatcher)
            : null;

        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly IMount mount;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IMountMediator mountMediator;
        private readonly IASAOptions options;
        private IProgress<ApplicationStatus> progress;
        private DeviceUpdateTimer updateTimer;
        private bool disposed = false;
        private bool supportedMountConnected = false;
        private bool previousTelescopeConnected = false;

        [ImportingConstructor]
        public MountVM(IProfileService profileService, ITelescopeMediator telescopeMediator, IApplicationStatusMediator applicationStatusMediator) :
            this(profileService, telescopeMediator, applicationStatusMediator, ASAPlugin.Mount, ASAPlugin.MountMediator, ASAPlugin.ASAOptions) {
        }

        public MountVM(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            IApplicationStatusMediator applicationStatusMediator,
            IMount mount,
            IMountMediator mountMediator,
            IASAOptions options) : base(profileService) {
            this.Title = "ASA Mount Info";

            var dict = new ResourceDictionary();
            dict.Source = new Uri("NINA.Photon.Plugin.ASA;component/Resources/SVGDataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ASASVG"];
            ImageGeometry.Freeze();

            this.mount = mount;
            this.telescopeMediator = telescopeMediator;
            this.mountMediator = mountMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.options = options;

            MountInfo.Status = MountStatusEnum.NotConnected;

            if (SynchronizationContext.Current == synchronizationContext) {
                this.progress = new Progress<ApplicationStatus>(p => {
                    p.Source = this.Title;
                    this.applicationStatusMediator.StatusUpdate(p);
                });
            } else {
                synchronizationContext.Send(_ => {
                    this.progress = new Progress<ApplicationStatus>(p => {
                        p.Source = this.Title;
                        this.applicationStatusMediator.StatusUpdate(p);
                    });
                }, null);
            }

            this.telescopeMediator.RegisterConsumer(this);
            this.mountMediator.RegisterHandler(this);

            ResetMeridianSlewLimitCommand = new RelayCommand(ResetMeridianSlewLimit);
            ResetSlewSettleLimitCommand = new RelayCommand(ResetSlewSettleTime);
            TogglePowerCommand = new AsyncCommand<bool>((object o) => Task.Run(() => TogglePower(o)));
        }

        private void BroadcastMountInfo() {
            this.mountMediator.Broadcast(MountInfo);
        }

        private void ResetMeridianSlewLimit(object o) {
            try {
                this.mount.SetMeridianSlewLimit(0);
            } catch (Exception e) {
                Notification.ShowError($"Failed to reset meridian limit: {e.Message}");
                Logger.Error(e);
            }
        }

        private void ResetSlewSettleTime(object o) {
            try {
                this.mount.SetSlewSettleTime(decimal.Zero);
            } catch (Exception e) {
                Notification.ShowError($"Failed to reset slew settle time: {e.Message}");
                Logger.Error(e);
            }
        }

        private Task<bool> TogglePower(object o) {
            if (MountInfo.Connected) {
                return Task.FromResult(Shutdown());
            } else {
                return PowerOn(CancellationToken.None);
            }
        }

        public async Task<bool> PowerOn(CancellationToken ct) {
            if (MountInfo.Connected) {
                return true;
            }

            Task progressDelay = null;
            CancellationTokenSource timeoutCts = null;
            try {
                if (string.IsNullOrEmpty(this.Options.IPAddress) || string.IsNullOrEmpty(this.Options.MACAddress)) {
                    Notification.ShowError("Cannot power on mount. No IP address is set. If you connect via IP, connect at least once to initialize settings");
                    Logger.Error("Cannot power on mount. No IP address is set. If you connect via IP, connect at least once to initialize settings");
                    return false;
                }

                if (string.IsNullOrEmpty(this.Options.DriverID)) {
                    Notification.ShowError("Cannot power on mount. Connect at least once to initialize settings");
                    Logger.Error("Cannot power on mount. Connect at least once to initialize settings");
                    return false;
                }

                this.progress.Report(new ApplicationStatus() {
                    Status = "Sending WOL packet"
                });

                progressDelay = Task.Delay(1000, ct);
                var ipAddress = IPAddress.Parse(this.Options.IPAddress);
                var mac = this.Options.MACAddress;
                await MountUtility.WakeOnLan(mac, this.Options.WolBroadcastIP, ct);

                await progressDelay;
                this.progress.Report(new ApplicationStatus() {
                    Status = "Waiting for mount to power on"
                });

                progressDelay = Task.Delay(1000, ct);
                timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
                await MountUtility.WaitUntilResponding(ipAddress, this.Options.Port, linkedCt.Token);

                profileService.ActiveProfile.TelescopeSettings.Id = this.Options.DriverID;
                await telescopeMediator.Rescan();
                return await telescopeMediator.Connect();
            } catch (OperationCanceledException) {
                if (timeoutCts?.IsCancellationRequested == true) {
                    Notification.ShowError("Timed out waiting for mount to power on");
                    Logger.Error("Timed out waiting for mount to power on");
                } else {
                    Logger.Info("ASA power on cancelled");
                }
            } catch (Exception e) {
                Notification.ShowError($"Failed to power on ASA mount: {e.Message}");
                Logger.Error("Failed to power on ASA mount", e);
            } finally {
                if (progressDelay != null) {
                    await progressDelay;
                }
                this.progress.Report(new ApplicationStatus() { });
            }
            return false;
        }

        public void Dispose() {
            if (!this.disposed) {
                updateTimer?.Stop();
                this.telescopeMediator.RemoveConsumer(this);
                this.disposed = true;
            }
        }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            if (previousTelescopeConnected == deviceInfo.Connected) {
                return;
            }

            try {
                if (deviceInfo.Connected) {
                    try {
                        ProductFirmware productFirmware = null;
                        try {
                            productFirmware = mount.GetProductFirmware();
                        } catch (Exception e) {
                            Logger.Error("Failed to query product firmware after telescope connected", e);
                            Notification.ShowWarning($"Not a ASA mount. ASA utilities disabled.");
                            return;
                        }

                        if (!MountUtility.IsSupportedProduct(productFirmware)) {
                            Logger.Error($"{productFirmware.ProductName} is not a supported ASA mount. ASA utilities disabled");
                            Notification.ShowInformation($"{productFirmware.ProductName} is not a supported ASA mount. ASA utilities disabled");
                            return;
                        }

                        var ascomConfig = MountUtility.GetMountAscomConfig(deviceInfo.DeviceId);
                        if (ascomConfig != null) {
                            if (!MountUtility.ValidateMountAscomConfig(ascomConfig)) {
                                Logger.Error($"ASCOM configuration validation failed. Leaving ASA mount utilities disconnected");
                                return;
                            }

                            RefractionOverrideFilePath = ascomConfig.RefractionUpdateFile;
                        }

                        mount.SetMaximumPrecision(productFirmware);
                        MountInfo.ProductFirmware = productFirmware;
                        MountInfo.MountId = mount.GetId();
                        MountInfo.Status = MountStatusEnum.Unknown;
                        Options.DriverID = profileService.ActiveProfile.TelescopeSettings.Id;

                        UpdateAddressConfig();
                        supportedMountConnected = true;
                        MountInfo.Connected = true;

                        // This cannot be initialized in the constructor because it is running in an Async context at that time, which causes UpdateMountValues to never fire
                        _ = updateTimer?.Stop();
                        updateTimer = new DeviceUpdateTimer(
                            GetMountValues,
                            UpdateMountValues,
                            profileService.ActiveProfile.ApplicationSettings.DevicePollingInterval
                        );
                        updateTimer.Start();
                    } catch (Exception e) {
                        Notification.ShowError($"Failed to connect ASA utilities. {e.Message}");
                    }
                } else {
                    _ = updateTimer?.Stop();

                    MountInfo = DeviceInfo.CreateDefaultInstance<MountInfo>();
                    supportedMountConnected = false;
                }
            } finally {
                previousTelescopeConnected = deviceInfo.Connected;
            }
        }

        private void UpdateAddressConfig() {
            try {
                var ipAddress = mount.GetIPAddress();
                var macAddress = mount.GetMACAddress();
                options.IPAddress = ipAddress.Value.IP;
                options.MACAddress = macAddress;
            } catch (Exception e) {
                Logger.Warning($"Failed to get IP and MAC address from ASA mount. {e.Message}");
            }
        }

        private MountInfo mountInfo = DeviceInfo.CreateDefaultInstance<MountInfo>();

        public MountInfo MountInfo {
            get => mountInfo;
            private set {
                mountInfo = value;
                RaisePropertyChanged();
            }
        }

        public IASAOptions Options => options;

        private string refractionOverrideFilePath;

        public string RefractionOverrideFilePath {
            get => refractionOverrideFilePath;
            set {
                if (refractionOverrideFilePath != value) {
                    this.refractionOverrideFilePath = value;
                    RaisePropertyChanged();
                }
            }
        }

        public async Task Disconnect() {
            if (updateTimer != null) {
                await updateTimer.Stop();
            }

            MountInfo = DeviceInfo.CreateDefaultInstance<MountInfo>();
            MountInfo.Status = MountStatusEnum.NotConnected;
            supportedMountConnected = false;

            BroadcastMountInfo();
        }

        private void UpdateMountValues(Dictionary<string, object> mountValues) {
            object o = null;
            mountValues.TryGetValue(nameof(MountInfo.Connected), out o);
            MountInfo.Connected = (bool)(o ?? false);

            if (!MountInfo.Connected) {
                _ = Disconnect();
                return;
            }

            mountValues.TryGetValue(nameof(MountInfo.UnattendedFlipEnabled), out o);
            MountInfo.UnattendedFlipEnabled = (bool)(o ?? false);
            UnattendedFlipEnabled = MountInfo.UnattendedFlipEnabled;

            mountValues.TryGetValue(nameof(MountInfo.TrackingRateArcsecPerSec), out o);
            MountInfo.TrackingRateArcsecPerSec = (decimal)(o ?? decimal.Zero);

            mountValues.TryGetValue(nameof(MountInfo.Status), out o);
            MountInfo.Status = (MountStatusEnum)(o ?? MountStatusEnum.Unknown);

            mountValues.TryGetValue(nameof(MountInfo.SlewSettleTimeSeconds), out o);
            MountInfo.SlewSettleTimeSeconds = (decimal)(o ?? decimal.Zero);

            mountValues.TryGetValue(nameof(MountInfo.MeridianLimitDegrees), out o);
            MountInfo.MeridianLimitDegrees = (int)(o ?? 0);

            mountValues.TryGetValue(nameof(MountInfo.DualAxisTrackingEnabled), out o);
            MountInfo.DualAxisTrackingEnabled = (bool)(o ?? false);

            BroadcastMountInfo();
        }

        private Dictionary<string, object> GetMountValues() {
            var mountValues = new Dictionary<string, object>();
            try {
                if (!supportedMountConnected) {
                    return mountValues;
                }

                mountValues.Add(nameof(MountInfo.Connected), true);
                mountValues.Add(nameof(MountInfo.UnattendedFlipEnabled), this.mount.GetUnattendedFlipEnabled().Value);
                mountValues.Add(nameof(MountInfo.TrackingRateArcsecPerSec), this.mount.GetTrackingRateArcsecsPerSec().Value);
                mountValues.Add(nameof(MountInfo.Status), this.mount.GetStatus().Value);
                mountValues.Add(nameof(MountInfo.SlewSettleTimeSeconds), this.mount.GetSlewSettleTimeSeconds().Value);
                mountValues.Add(nameof(MountInfo.MeridianLimitDegrees), this.mount.GetMeridianSlewLimitDegrees().Value);
                mountValues.Add(nameof(MountInfo.DualAxisTrackingEnabled), this.mount.GetDualAxisTrackingEnabled().Value);
                return mountValues;
            } catch (Exception e) {
                if (telescopeMediator.GetInfo().Connected) {
                    Notification.ShowError($"Failed to retrieve mount properties. ASA mount utilities disconnected");
                    Logger.Error("Failed while retrieving ASA mount properties", e);
                    MountInfo = DeviceInfo.CreateDefaultInstance<MountInfo>();
                }

                mountValues.Clear();
                mountValues.Add(nameof(MountInfo.Connected), false);
                return mountValues;
            }
        }

        public ICommand ResetMeridianSlewLimitCommand { get; private set; }
        public ICommand ResetSlewSettleLimitCommand { get; private set; }
        public ICommand TogglePowerCommand { get; private set; }

        private bool unattendedFlipEnabled;

        public bool UnattendedFlipEnabled {
            get => unattendedFlipEnabled;
            set {
                if (unattendedFlipEnabled != value) {
                    unattendedFlipEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        public void DisableUnattendedFlip() {
            try {
                mount.SetUnattendedFlip(false);
                UnattendedFlipEnabled = false;
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError($"Failed to disable unattended flip: {ex.Message}");
            }
        }

        public void SetDualAxisTracking(bool enabled) {
            if (!MountInfo.Connected) {
                return;
            }

            try {
                if (mount.SetDualAxisTracking(enabled)) {
                    MountInfo.DualAxisTrackingEnabled = enabled;
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError($"Failed to set dual axis tracking to {enabled}: {ex.Message}");
            }
        }

        public Task<IList<string>> Rescan() {
            throw new NotImplementedException();
        }

        public Task<bool> Connect() {
            throw new NotImplementedException();
        }

        public MountInfo GetDeviceInfo() {
            return MountInfo;
        }

        public CoordinateAngle GetMountReportedDeclination() {
            if (supportedMountConnected) {
                return mount.GetDeclination();
            }
            return null;
        }

        public AstrometricTime GetMountReportedRightAscension() {
            if (supportedMountConnected) {
                return mount.GetRightAscension();
            }
            return null;
        }

        public AstrometricTime GetMountReportedLocalSiderealTime() {
            if (supportedMountConnected) {
                return mount.GetLocalSiderealTime();
            }
            return null;
        }

        public bool SetTrackingRate(TrackingMode trackingMode) {
            if (supportedMountConnected) {
                switch (trackingMode) {
                    case TrackingMode.Sidereal:
                        mount.SetSiderealTrackingRate();
                        mount.StartTracking();
                        mount.SetDualAxisTracking(true);
                        return true;

                    case TrackingMode.Solar:
                        mount.SetSolarTrackingRate();
                        mount.StartTracking();
                        mount.SetDualAxisTracking(true);
                        return true;

                    case TrackingMode.Lunar:
                        mount.SetLunarTrackingRate();
                        mount.StartTracking();
                        mount.SetDualAxisTracking(true);
                        return true;

                    case TrackingMode.Stopped:
                        mount.StopTracking();
                        return true;

                    case TrackingMode.King:
                        Logger.Error("King rate is not supported");
                        return false;

                    case TrackingMode.Custom:
                        Logger.Error("Custom rate is not supported");
                        return false;
                }

                throw new ArgumentException($"Unknown tracking mode received: {trackingMode}");
            }
            return false;
        }

        public bool Shutdown() {
            if (MountInfo.Connected) {
                if (mount.Shutdown()) {
                    _ = Disconnect();
                    return true;
                } else {
                    Notification.ShowError("Failed to send a shutdown command to the ASA mount");
                    Logger.Error("Failed to send a shutdown command to the ASA mount");
                }
            }
            return false;
        }

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public IDevice GetDevice() {
            throw new NotImplementedException();
        }
    }
}