#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.IO;
using NINA.Core.Utility;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.ModelManagement;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System.Windows.Input;
using Microsoft.Win32;

namespace NINA.Photon.Plugin.ASA
{
    public class ASAOptions : BaseINPC, IASAOptions
    {
        private const double DEFAULT_SYNC_EVERY_HA_MINUTES = 90.0d;
        private const double DEFAULT_SYNC_ALTITUDE_DEGREES = 60.0d;
        private const double DEFAULT_REF_ALTITUDE_DEGREES = 30.0d;
        private const double DEFAULT_SYNC_EAST_AZIMUTH_DEGREES = 90.0d;
        private const double DEFAULT_SYNC_WEST_AZIMUTH_DEGREES = 270.0d;
        private const double DEFAULT_REF_EAST_AZIMUTH_DEGREES = 90.0d;
        private const double DEFAULT_REF_WEST_AZIMUTH_DEGREES = 270.0d;

        private readonly PluginOptionsAccessor optionsAccessor;
        private readonly IProfileService profileService;

        public ASAOptions(IProfileService profileService)
        {
            this.profileService = profileService;
            var guid = PluginOptionsAccessor.GetAssemblyGuid(typeof(ASAOptions));
            if (guid == null)
            {
                throw new Exception($"Guid not found in assembly metadata");
            }

            this.optionsAccessor = new PluginOptionsAccessor(profileService, guid.Value);
            InitializeOptions();

            this.ShowSelectPOXOutputDirectoryDialogCommand = new RelayCommand(ShowSelectPOXOutputDirectoryDialog);
        }

        private void InitializeOptions()
        {
            goldenSpiralStarCount = optionsAccessor.GetValueInt32("GoldenSpiralStarCount", 9);
            autoGridRASpacingDegrees = optionsAccessor.GetValueDouble("AutoGridRASpacingDegrees", 10.0d);
            autoGridDecSpacingDegrees = optionsAccessor.GetValueDouble("AutoGridDecSpacingDegrees", 10.0d);
            autoGridInputMode = optionsAccessor.GetValueEnum("AutoGridInputMode", AutoGridInputModeEnum.Spacing);
            autoGridPathOrderingMode = optionsAccessor.GetValueEnum("AutoGridPathOrderingMode", AutoGridPathOrderingModeEnum.ASABandPath);
            if (!Enum.IsDefined(typeof(AutoGridPathOrderingModeEnum), autoGridPathOrderingMode))
            {
                autoGridPathOrderingMode = AutoGridPathOrderingModeEnum.ASABandPath;
                optionsAccessor.SetValueEnum("AutoGridPathOrderingMode", autoGridPathOrderingMode);
            }
            autoGridDesiredPointCount = optionsAccessor.GetValueInt32("AutoGridDesiredPointCount", 50);
            startAtHorizon = optionsAccessor.GetValueBoolean("StartAtHorizon", false);
            balanceMeridianZone = optionsAccessor.GetValueBoolean("BalanceMeridianZone", false);
            siderealTrackStartOffsetMinutes = optionsAccessor.GetValueInt32("SiderealTrackStartOffsetMinutes", 0);
            siderealTrackEndOffsetMinutes = optionsAccessor.GetValueInt32("SiderealTrackEndOffsetMinutes", 0);
            siderealTrackPathOffsetMinutes = optionsAccessor.GetValueInt32("SiderealTrackPathOffsetMinutes", 0);
            siderealTrackRADeltaDegrees = optionsAccessor.GetValueDouble("SiderealTrackRADeltaDegrees", 1.5d);
            siderealTrackPreBalanceFarEndSlew = optionsAccessor.GetValueBoolean("SiderealTrackPreBalanceFarEndSlew", true);
            domeShutterWidth_mm = optionsAccessor.GetValueInt32("DomeShutterWidth_mm", 0);
            minimizeDomeMovementEnabled = optionsAccessor.GetValueBoolean("MinimizeDomeMovementEnabled", true);
            minimizeMeridianFlipsEnabled = optionsAccessor.GetValueBoolean("MinimizeMeridianFlipsEnabled", true);
            modelPointGenerationType = optionsAccessor.GetValueEnum("ModelPointGenerationType", ModelPointGenerationTypeEnum.AutoGrid);
            builderNumRetries = optionsAccessor.GetValueInt32("BuilderNumRetries", 0);
            westToEastSorting = optionsAccessor.GetValueBoolean("WestToEastSorting", false);
            maxPointRMS = optionsAccessor.GetValueDouble("MaxPointRMS", double.NaN);
            logCommands = optionsAccessor.GetValueBoolean("LogCommands", false);
            maxConcurrency = optionsAccessor.GetValueInt32("MaxConcurrency", 3);
            allowBlindSolves = optionsAccessor.GetValueBoolean("AllowBlindSolves", false);
            minPointAltitude = optionsAccessor.GetValueInt32("MinPointAltitude", 0);
            maxPointAltitude = optionsAccessor.GetValueInt32("MaxPointAltitude", 90);
            showRemovedPoints = optionsAccessor.GetValueBoolean("ShowRemovedPoints", true);
            maxFailedPoints = optionsAccessor.GetValueInt32("MaxFailedPoints", 0);
            siderealTrackStartTimeProvider = optionsAccessor.GetValueString("SiderealTrackStartTimeProvider", "Now");
            siderealTrackEndTimeProvider = optionsAccessor.GetValueString("SiderealTrackEndTimeProvider", "Now");
            removeHighRMSPointsAfterBuild = optionsAccessor.GetValueBoolean("RemoveHighRMSPointsAfterBuild", true);
            plateSolveSubframePercentage = optionsAccessor.GetValueDouble("PlateSolveSubframePercentage", 1.0d);
            syncBeforeModelBuild = optionsAccessor.GetValueBoolean(nameof(SyncBeforeModelBuild), GetLegacySyncBeforeModelBuildDefault());
            useDedicatedFullSkyPlateSolveSettings = optionsAccessor.GetValueBoolean(nameof(UseDedicatedFullSkyPlateSolveSettings), false);
            fullSkyPlateSolveExposureTime = optionsAccessor.GetValueDouble(nameof(FullSkyPlateSolveExposureTime), GetDefaultProfilePlateSolveExposureTime());
            fullSkyPlateSolveBinning = optionsAccessor.GetValueInt32(nameof(FullSkyPlateSolveBinning), GetDefaultProfilePlateSolveBinning());
            fullSkyPlateSolveGain = optionsAccessor.GetValueInt32(nameof(FullSkyPlateSolveGain), GetDefaultProfilePlateSolveGain());
            fullSkyPlateSolveOffset = optionsAccessor.GetValueInt32(nameof(FullSkyPlateSolveOffset), GetDefaultProfilePlateSolveOffset());
            useDedicatedMLPTPlateSolveSettings = optionsAccessor.GetValueBoolean(nameof(UseDedicatedMLPTPlateSolveSettings), false);
            mlptPlateSolveExposureTime = optionsAccessor.GetValueDouble(nameof(MLPTPlateSolveExposureTime), GetDefaultProfilePlateSolveExposureTime());
            mlptPlateSolveBinning = optionsAccessor.GetValueInt32(nameof(MLPTPlateSolveBinning), GetDefaultProfilePlateSolveBinning());
            mlptPlateSolveGain = optionsAccessor.GetValueInt32(nameof(MLPTPlateSolveGain), GetDefaultProfilePlateSolveGain());
            mlptPlateSolveOffset = optionsAccessor.GetValueInt32(nameof(MLPTPlateSolveOffset), GetDefaultProfilePlateSolveOffset());
            alternateDirectionsBetweenIterations = optionsAccessor.GetValueBoolean("AlternateDirectionsBetweenIterations", true);
            minPointAzimuth = optionsAccessor.GetValueDouble("MinPointAzimuth", 0.5d);
            maxPointAzimuth = optionsAccessor.GetValueDouble("MaxPointAzimuth", 359.5d);
            minDistanceToHorizonDegrees = optionsAccessor.GetValueDouble("MinDistanceToHorizonDegrees", 0.0d);
            disableRefractionCorrection = false; // optionsAccessor.GetValueBoolean("DisableRefractionCorrection", false);
            //ipAddress = optionsAccessor.GetValueString("IPAddress", "");
            //macAddress = optionsAccessor.GetValueString("MACAddress", "");
            //wolBroadcastIP = optionsAccessor.GetValueString("WolBroadcastIP", "");
            //port = optionsAccessor.GetValueInt32("Port", 3490);
            driverID = optionsAccessor.GetValueString("DriverID", "");
            decJitterSigmaDegrees = optionsAccessor.GetValueDouble(nameof(DecJitterSigmaDegrees), 1.0d);
            isLegacyDDM = optionsAccessor.GetValueBoolean("IsLegacyDDM", true);
            domeControlNINA = optionsAccessor.GetValueBoolean("DomeControlNINA", false);
            enableMLPTDebugSimulator = optionsAccessor.GetValueBoolean(nameof(EnableMLPTDebugSimulator), false);
            lastMLPT = optionsAccessor.GetValueDateTime("LastMLPT", DateTime.MinValue);
            activeMLPTDurationSeconds = optionsAccessor.GetValueDouble(nameof(ActiveMLPTDurationSeconds), 0.0d);
            activeMLPTPointCount = optionsAccessor.GetValueInt32(nameof(ActiveMLPTPointCount), 0);
            highAltitudeStars = optionsAccessor.GetValueInt32("HighAltitudeStars", 10);
            highAltitudeMin = optionsAccessor.GetValueInt32("HighAltitudeMin", 70);
            highAltitudeMax = optionsAccessor.GetValueInt32("HighAltitudeMax", 89);
            useSync = optionsAccessor.GetValueBoolean("UseSync", false);
            syncEveryHA = optionsAccessor.GetValueDouble("SyncEveryHA", DEFAULT_SYNC_EVERY_HA_MINUTES);
            syncEastAltitude = optionsAccessor.GetValueDouble("SyncEastAltitude", DEFAULT_SYNC_ALTITUDE_DEGREES);
            syncWestAltitude = optionsAccessor.GetValueDouble("SyncWestAltitude", DEFAULT_SYNC_ALTITUDE_DEGREES);
            refEastAltitude = optionsAccessor.GetValueDouble("RefEastAltitude", DEFAULT_REF_ALTITUDE_DEGREES);
            refWestAltitude = optionsAccessor.GetValueDouble("RefWestAltitude", DEFAULT_REF_ALTITUDE_DEGREES);
            syncEastAzimuth = optionsAccessor.GetValueDouble("SyncEastAzimuth", DEFAULT_SYNC_EAST_AZIMUTH_DEGREES);
            syncWestAzimuth = optionsAccessor.GetValueDouble("SyncWestAzimuth", DEFAULT_SYNC_WEST_AZIMUTH_DEGREES);
            refEastAzimuth = optionsAccessor.GetValueDouble("RefEastAzimuth", DEFAULT_REF_EAST_AZIMUTH_DEGREES);
            refWestAzimuth = optionsAccessor.GetValueDouble("RefWestAzimuth", DEFAULT_REF_WEST_AZIMUTH_DEGREES);
            ApplyLegacyZeroSyncReferenceDefaults();
            poxOutputDirectory = optionsAccessor.GetValueString("POXOutputDirectory", DefaultASAPointingPicsPath());
            chartPointSize = optionsAccessor.GetValueDouble("ChartPointSize", 2.8d);
            showHorizon = optionsAccessor.GetValueBoolean("ShowHorizon", true);
            showCardinalLabels = optionsAccessor.GetValueBoolean("ShowCardinalLabels", false);
            showCelestialPole = optionsAccessor.GetValueBoolean("ShowCelestialPole", true);
            showMeridianLimitsInCharts = optionsAccessor.GetValueBoolean("ShowMeridianLimitsInCharts", true);
            horizonTransparencyPercent = optionsAccessor.GetValueInt32("HorizonTransparencyPercent", 65);
        }

        public void ResetDefaults()
        {
            GoldenSpiralStarCount = 9;
            AutoGridRASpacingDegrees = 10.0d;
            AutoGridDecSpacingDegrees = 10.0d;
            AutoGridInputMode = AutoGridInputModeEnum.Spacing;
            AutoGridPathOrderingMode = AutoGridPathOrderingModeEnum.ASABandPath;
            AutoGridDesiredPointCount = 50;
            StartAtHorizon = false;
            BalanceMeridianZone = false;
            SiderealTrackStartOffsetMinutes = 0;
            SiderealTrackEndOffsetMinutes = 0;
            SiderealTrackPathOffsetMinutes = 0;
            SiderealTrackRADeltaDegrees = 1.5d;
            SiderealTrackPreBalanceFarEndSlew = false;
            DomeShutterWidth_mm = 0;
            MinimizeDomeMovementEnabled = true;
            MinimizeMeridianFlipsEnabled = true;
            ModelPointGenerationType = ModelPointGenerationTypeEnum.AutoGrid;
            BuilderNumRetries = 0;
            WestToEastSorting = false;
            MaxPointRMS = double.NaN;
            LogCommands = false;
            MaxConcurrency = 3;
            AllowBlindSolves = false;
            MinPointAltitude = 0;
            MaxPointAltitude = 90;
            ShowRemovedPoints = true;
            MaxFailedPoints = 0;
            SiderealTrackStartTimeProvider = "Now";
            SiderealTrackEndTimeProvider = "Now";
            RemoveHighRMSPointsAfterBuild = true;
            PlateSolveSubframePercentage = 1.0d;
            SyncBeforeModelBuild = false;
            UseDedicatedFullSkyPlateSolveSettings = false;
            FullSkyPlateSolveExposureTime = GetDefaultProfilePlateSolveExposureTime();
            FullSkyPlateSolveBinning = GetDefaultProfilePlateSolveBinning();
            FullSkyPlateSolveGain = GetDefaultProfilePlateSolveGain();
            FullSkyPlateSolveOffset = GetDefaultProfilePlateSolveOffset();
            UseDedicatedMLPTPlateSolveSettings = false;
            MLPTPlateSolveExposureTime = GetDefaultProfilePlateSolveExposureTime();
            MLPTPlateSolveBinning = GetDefaultProfilePlateSolveBinning();
            MLPTPlateSolveGain = GetDefaultProfilePlateSolveGain();
            MLPTPlateSolveOffset = GetDefaultProfilePlateSolveOffset();
            AlternateDirectionsBetweenIterations = true;
            MinPointAzimuth = 0.5d;
            MaxPointAzimuth = 359.5d;
            MinDistanceToHorizonDegrees = 0.0d;
            DisableRefractionCorrection = false;
            IsLegacyDDM = true;
            DomeControlNINA = false;
            EnableMLPTDebugSimulator = false;
            LastMLPT = DateTime.MinValue;
            ActiveMLPTDurationSeconds = 0.0d;
            ActiveMLPTPointCount = 0;
            MACAddress = "";
            IPAddress = "";
            WolBroadcastIP = "";
            Port = 3490;
            DriverID = "";
            DecJitterSigmaDegrees = 1.0d;
            HighAltitudeStars = 10;
            UseSync = false;
            HighAltitudeMin = 60;
            HighAltitudeMax = 89;
            SyncEveryHA = DEFAULT_SYNC_EVERY_HA_MINUTES;
            SyncEastAltitude = DEFAULT_SYNC_ALTITUDE_DEGREES;
            SyncWestAltitude = DEFAULT_SYNC_ALTITUDE_DEGREES;
            SyncEastAzimuth = DEFAULT_SYNC_EAST_AZIMUTH_DEGREES;
            SyncWestAzimuth = DEFAULT_SYNC_WEST_AZIMUTH_DEGREES;
            RefEastAltitude = DEFAULT_REF_ALTITUDE_DEGREES;
            RefWestAltitude = DEFAULT_REF_ALTITUDE_DEGREES;
            RefEastAzimuth = DEFAULT_REF_EAST_AZIMUTH_DEGREES;
            RefWestAzimuth = DEFAULT_REF_WEST_AZIMUTH_DEGREES;
            ChartPointSize = 2.8d;
            ShowHorizon = true;
            ShowCardinalLabels = false;
            ShowCelestialPole = true;
            ShowMeridianLimitsInCharts = true;
            HorizonTransparencyPercent = 65;

            POXOutputDirectory = DefaultASAPointingPicsPath();
        }

        private string DefaultASAPointingPicsPath()
        {
            var programdata = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            var filePath = System.IO.Path.Combine(programdata, "ASA", "Sequence", "PointingPics");
            return filePath;
        }

        private bool GetLegacySyncBeforeModelBuildDefault()
        {
            return optionsAccessor.GetValueBoolean("SlewToCorrectPierSideBeforeStart", false)
                || optionsAccessor.GetValueBoolean("PlateSolveAndSyncBeforeStart", false)
                || optionsAccessor.GetValueBoolean("EnableNINACoordinateSyncDuringBuild", false);
        }

        private void ApplyLegacyZeroSyncReferenceDefaults()
        {
            // Some older installs persisted all sync/reference values as zeros.
            // Migrate that full-zero state to current defaults so the UI is pre-populated.
            var hasLegacyAllZeroSyncReferenceValues =
                syncEveryHA == 0.0d &&
                syncEastAltitude == 0.0d &&
                syncWestAltitude == 0.0d &&
                syncEastAzimuth == 0.0d &&
                syncWestAzimuth == 0.0d &&
                refEastAltitude == 0.0d &&
                refWestAltitude == 0.0d &&
                refEastAzimuth == 0.0d &&
                refWestAzimuth == 0.0d;

            if (!hasLegacyAllZeroSyncReferenceValues)
            {
                return;
            }

            syncEveryHA = DEFAULT_SYNC_EVERY_HA_MINUTES;
            syncEastAltitude = DEFAULT_SYNC_ALTITUDE_DEGREES;
            syncWestAltitude = DEFAULT_SYNC_ALTITUDE_DEGREES;
            syncEastAzimuth = DEFAULT_SYNC_EAST_AZIMUTH_DEGREES;
            syncWestAzimuth = DEFAULT_SYNC_WEST_AZIMUTH_DEGREES;
            refEastAltitude = DEFAULT_REF_ALTITUDE_DEGREES;
            refWestAltitude = DEFAULT_REF_ALTITUDE_DEGREES;
            refEastAzimuth = DEFAULT_REF_EAST_AZIMUTH_DEGREES;
            refWestAzimuth = DEFAULT_REF_WEST_AZIMUTH_DEGREES;

            optionsAccessor.SetValueDouble("SyncEveryHA", syncEveryHA);
            optionsAccessor.SetValueDouble("SyncEastAltitude", syncEastAltitude);
            optionsAccessor.SetValueDouble("SyncWestAltitude", syncWestAltitude);
            optionsAccessor.SetValueDouble("SyncEastAzimuth", syncEastAzimuth);
            optionsAccessor.SetValueDouble("SyncWestAzimuth", syncWestAzimuth);
            optionsAccessor.SetValueDouble("RefEastAltitude", refEastAltitude);
            optionsAccessor.SetValueDouble("RefWestAltitude", refWestAltitude);
            optionsAccessor.SetValueDouble("RefEastAzimuth", refEastAzimuth);
            optionsAccessor.SetValueDouble("RefWestAzimuth", refWestAzimuth);
        }

        public ICommand ShowSelectPOXOutputDirectoryDialogCommand { get; private set; }

        private int minPointAltitude;

        public int MinPointAltitude
        {
            get => minPointAltitude;
            set
            {
                if (minPointAltitude != value)
                {
                    if (value < 0 || value > 90)
                    {
                        throw new ArgumentException("MinPointAltitude must be between 0 and 90, inclusive", "MinPointAltitude");
                    }
                    minPointAltitude = value;
                    optionsAccessor.SetValueInt32("MinPointAltitude", minPointAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private int maxPointAltitude;

        public int MaxPointAltitude
        {
            get => maxPointAltitude;
            set
            {
                if (maxPointAltitude != value)
                {
                    if (value < 0 || value > 90)
                    {
                        throw new ArgumentException("MaxPointAltitude must be between 0 and 90, inclusive", "MaxPointAltitude");
                    }
                    maxPointAltitude = value;
                    optionsAccessor.SetValueInt32("MaxPointAltitude", maxPointAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private int goldenSpiralStarCount;

        public int GoldenSpiralStarCount
        {
            get => goldenSpiralStarCount;
            set
            {
                if (goldenSpiralStarCount != value)
                {
                    if (value < 3 || value > ModelPointGenerator.MAX_POINTS)
                    {
                        throw new ArgumentException($"GoldenSpiralStarCount must be between 3 and {ModelPointGenerator.MAX_POINTS}, inclusive", "GoldenSpiralStarCount");
                    }
                    goldenSpiralStarCount = value;
                    optionsAccessor.SetValueInt32("GoldenSpiralStarCount", goldenSpiralStarCount);
                    RaisePropertyChanged();
                }
            }
        }

        private double autoGridRASpacingDegrees;

        public double AutoGridRASpacingDegrees
        {
            get => autoGridRASpacingDegrees;
            set
            {
                if (Math.Abs(autoGridRASpacingDegrees - value) > double.Epsilon)
                {
                    if (value <= 0.0d || value > 360.0d)
                    {
                        throw new ArgumentException("AutoGridRASpacingDegrees must be between 0 and 360, exclusive-inclusive", "AutoGridRASpacingDegrees");
                    }
                    autoGridRASpacingDegrees = value;
                    optionsAccessor.SetValueDouble("AutoGridRASpacingDegrees", autoGridRASpacingDegrees);
                    RaisePropertyChanged();
                }
            }
        }

        private double autoGridDecSpacingDegrees;

        public double AutoGridDecSpacingDegrees
        {
            get => autoGridDecSpacingDegrees;
            set
            {
                if (Math.Abs(autoGridDecSpacingDegrees - value) > double.Epsilon)
                {
                    if (value <= 0.0d || value > 180.0d)
                    {
                        throw new ArgumentException("AutoGridDecSpacingDegrees must be between 0 and 180, exclusive-inclusive", "AutoGridDecSpacingDegrees");
                    }
                    autoGridDecSpacingDegrees = value;
                    optionsAccessor.SetValueDouble("AutoGridDecSpacingDegrees", autoGridDecSpacingDegrees);
                    RaisePropertyChanged();
                }
            }
        }

        private AutoGridInputModeEnum autoGridInputMode;

        public AutoGridInputModeEnum AutoGridInputMode
        {
            get => autoGridInputMode;
            set
            {
                if (autoGridInputMode != value)
                {
                    autoGridInputMode = value;
                    optionsAccessor.SetValueEnum("AutoGridInputMode", autoGridInputMode);
                    RaisePropertyChanged();
                }
            }
        }

        private AutoGridPathOrderingModeEnum autoGridPathOrderingMode;

        public AutoGridPathOrderingModeEnum AutoGridPathOrderingMode
        {
            get => autoGridPathOrderingMode;
            set
            {
                if (!Enum.IsDefined(typeof(AutoGridPathOrderingModeEnum), value))
                {
                    value = AutoGridPathOrderingModeEnum.ASABandPath;
                }

                if (autoGridPathOrderingMode != value)
                {
                    autoGridPathOrderingMode = value;
                    optionsAccessor.SetValueEnum("AutoGridPathOrderingMode", autoGridPathOrderingMode);
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridDesiredPointCount;

        public int AutoGridDesiredPointCount
        {
            get => autoGridDesiredPointCount;
            set
            {
                if (autoGridDesiredPointCount != value)
                {
                    if (value < 3 || value > ModelPointGenerator.MAX_POINTS)
                    {
                        throw new ArgumentException($"AutoGridDesiredPointCount must be between 3 and {ModelPointGenerator.MAX_POINTS}, inclusive", "AutoGridDesiredPointCount");
                    }

                    autoGridDesiredPointCount = value;
                    optionsAccessor.SetValueInt32("AutoGridDesiredPointCount", autoGridDesiredPointCount);
                    RaisePropertyChanged();
                }
            }
        }

        private bool startAtHorizon;

        public bool StartAtHorizon
        {
            get => startAtHorizon;
            set
            {
                if (startAtHorizon != value)
                {
                    startAtHorizon = value;
                    optionsAccessor.SetValueBoolean("StartAtHorizon", startAtHorizon);

                    if (startAtHorizon && balanceMeridianZone)
                    {
                        balanceMeridianZone = false;
                        optionsAccessor.SetValueBoolean("BalanceMeridianZone", balanceMeridianZone);
                        RaisePropertyChanged(nameof(BalanceMeridianZone));
                    }

                    RaisePropertyChanged();
                }
            }
        }

        private bool balanceMeridianZone;

        public bool BalanceMeridianZone
        {
            get => balanceMeridianZone;
            set
            {
                if (balanceMeridianZone != value)
                {
                    balanceMeridianZone = value;
                    optionsAccessor.SetValueBoolean("BalanceMeridianZone", balanceMeridianZone);

                    if (balanceMeridianZone && startAtHorizon)
                    {
                        startAtHorizon = false;
                        optionsAccessor.SetValueBoolean("StartAtHorizon", startAtHorizon);
                        RaisePropertyChanged(nameof(StartAtHorizon));
                    }

                    RaisePropertyChanged();
                }
            }
        }

        private int siderealTrackStartOffsetMinutes;

        public int SiderealTrackStartOffsetMinutes
        {
            get => siderealTrackStartOffsetMinutes;
            set
            {
                if (siderealTrackStartOffsetMinutes != value)
                {
                    siderealTrackStartOffsetMinutes = value;
                    optionsAccessor.SetValueInt32("SiderealTrackStartOffsetMinutes", siderealTrackStartOffsetMinutes);
                    RaisePropertyChanged();
                }
            }
        }

        private int siderealTrackEndOffsetMinutes;

        public int SiderealTrackEndOffsetMinutes
        {
            get => siderealTrackEndOffsetMinutes;
            set
            {
                if (siderealTrackEndOffsetMinutes != value)
                {
                    siderealTrackEndOffsetMinutes = value;
                    optionsAccessor.SetValueInt32("SiderealTrackEndOffsetMinutes", siderealTrackEndOffsetMinutes);
                    RaisePropertyChanged();
                }
            }
        }

        private int siderealTrackPathOffsetMinutes;

        public int SiderealTrackPathOffsetMinutes
        {
            get => siderealTrackPathOffsetMinutes;
            set
            {
                if (siderealTrackPathOffsetMinutes != value)
                {
                    siderealTrackPathOffsetMinutes = value;
                    optionsAccessor.SetValueInt32("SiderealTrackPathOffsetMinutes", siderealTrackPathOffsetMinutes);
                    RaisePropertyChanged();
                }
            }
        }

        private double siderealTrackRADeltaDegrees;

        public double SiderealTrackRADeltaDegrees
        {
            get => siderealTrackRADeltaDegrees;
            set
            {
                if (siderealTrackRADeltaDegrees != value)
                {
                    if (value <= 0.0d)
                    {
                        throw new ArgumentException("SiderealTrackRADeltaDegrees must be positive", "SiderealTrackRADeltaDegrees");
                    }
                    siderealTrackRADeltaDegrees = value;
                    optionsAccessor.SetValueDouble("SiderealTrackRADeltaDegrees", siderealTrackRADeltaDegrees);
                    RaisePropertyChanged();
                }
            }
        }

        private bool siderealTrackPreBalanceFarEndSlew;

        public bool SiderealTrackPreBalanceFarEndSlew
        {
            get => siderealTrackPreBalanceFarEndSlew;
            set
            {
                if (siderealTrackPreBalanceFarEndSlew != value)
                {
                    siderealTrackPreBalanceFarEndSlew = value;
                    optionsAccessor.SetValueBoolean("SiderealTrackPreBalanceFarEndSlew", siderealTrackPreBalanceFarEndSlew);
                    RaisePropertyChanged();
                }
            }
        }

        private int domeShutterWidth_mm;

        // TODO: Restore after this works properly
        public int DomeShutterWidth_mm
        {
            get => 0; // domeShutterWidth_mm;
            set
            {
                if (domeShutterWidth_mm != value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentException("DomeShutterWidth_mm must be non-negative", "DomeShutterWidth_mm");
                    }
                    domeShutterWidth_mm = value;
                    optionsAccessor.SetValueInt32("DomeShutterWidth_mm", domeShutterWidth_mm);
                    RaisePropertyChanged();
                }
            }
        }

        private bool minimizeDomeMovementEnabled;

        public bool MinimizeDomeMovementEnabled
        {
            get => minimizeDomeMovementEnabled;
            set
            {
                if (minimizeDomeMovementEnabled != value)
                {
                    minimizeDomeMovementEnabled = value;
                    optionsAccessor.SetValueBoolean("MinimizeDomeMovementEnabled", minimizeDomeMovementEnabled);
                    RaisePropertyChanged();
                }
            }
        }

        private bool minimizeMeridianFlipsEnabled;

        public bool MinimizeMeridianFlipsEnabled
        {
            get => minimizeMeridianFlipsEnabled;
            set
            {
                if (minimizeMeridianFlipsEnabled != value)
                {
                    minimizeMeridianFlipsEnabled = value;
                    optionsAccessor.SetValueBoolean("MinimizeMeridianFlipsEnabled", minimizeMeridianFlipsEnabled);
                    RaisePropertyChanged();
                }
            }
        }

        private ModelPointGenerationTypeEnum modelPointGenerationType;

        public ModelPointGenerationTypeEnum ModelPointGenerationType
        {
            get => modelPointGenerationType;
            set
            {
                if (modelPointGenerationType != value)
                {
                    modelPointGenerationType = value;
                    optionsAccessor.SetValueEnum("ModelPointGenerationType", modelPointGenerationType);
                    RaisePropertyChanged();
                }
            }
        }

        private bool westToEastSorting;

        public bool WestToEastSorting
        {
            get => westToEastSorting;
            set
            {
                if (westToEastSorting != value)
                {
                    westToEastSorting = value;
                    optionsAccessor.SetValueBoolean("WestToEastSorting", westToEastSorting);
                    RaisePropertyChanged();
                }
            }
        }

        private int highAltitudeStars;

        public int HighAltitudeStars
        {
            get => highAltitudeStars;
            set
            {
                if (highAltitudeStars != value)
                {
                    highAltitudeStars = value;
                    optionsAccessor.SetValueInt32("HighAltitudeStars", highAltitudeStars);
                    RaisePropertyChanged();
                }
            }
        }

        private int highAltitudeMin;

        public int HighAltitudeMin
        {
            get => highAltitudeMin;
            set
            {
                if (highAltitudeMin != value)
                {
                    highAltitudeMin = value;
                    optionsAccessor.SetValueInt32("HighAltitudeMin", highAltitudeMin);
                    RaisePropertyChanged();
                }
            }
        }

        private int highAltitudeMax;

        public int HighAltitudeMax
        {
            get => highAltitudeMax;
            set
            {
                if (highAltitudeMax != value)
                {
                    highAltitudeMax = value;
                    optionsAccessor.SetValueInt32("HighAltitudeMax", highAltitudeMax);
                    RaisePropertyChanged();
                }
            }
        }

        private double syncEveryHA;

        public double SyncEveryHA
        {
            get => syncEveryHA;
            set
            {
                if (syncEveryHA != value)
                {
                    syncEveryHA = value;
                    optionsAccessor.SetValueDouble("SyncEveryHA", syncEveryHA);
                    RaisePropertyChanged();
                }
            }
        }

        private bool useSync;

        public bool UseSync
        {
            get => useSync;
            set
            {
                if (useSync != value)
                {
                    useSync = value;
                    optionsAccessor.SetValueBoolean("UseSync", useSync);
                    RaisePropertyChanged();
                }
            }
        }

        private bool syncBeforeModelBuild;

        public bool SyncBeforeModelBuild
        {
            get => syncBeforeModelBuild;
            set
            {
                if (syncBeforeModelBuild != value)
                {
                    syncBeforeModelBuild = value;
                    optionsAccessor.SetValueBoolean(nameof(SyncBeforeModelBuild), syncBeforeModelBuild);
                    RaisePropertyChanged();
                }
            }
        }

        private bool useDedicatedFullSkyPlateSolveSettings;

        public bool UseDedicatedFullSkyPlateSolveSettings
        {
            get => useDedicatedFullSkyPlateSolveSettings;
            set
            {
                if (useDedicatedFullSkyPlateSolveSettings != value)
                {
                    useDedicatedFullSkyPlateSolveSettings = value;
                    optionsAccessor.SetValueBoolean(nameof(UseDedicatedFullSkyPlateSolveSettings), useDedicatedFullSkyPlateSolveSettings);
                    RaisePropertyChanged();
                }
            }
        }

        private double fullSkyPlateSolveExposureTime;

        public double FullSkyPlateSolveExposureTime
        {
            get => fullSkyPlateSolveExposureTime;
            set
            {
                if (Math.Abs(fullSkyPlateSolveExposureTime - value) > double.Epsilon)
                {
                    fullSkyPlateSolveExposureTime = value;
                    optionsAccessor.SetValueDouble(nameof(FullSkyPlateSolveExposureTime), fullSkyPlateSolveExposureTime);
                    RaisePropertyChanged();
                }
            }
        }

        private int fullSkyPlateSolveBinning;

        public int FullSkyPlateSolveBinning
        {
            get => fullSkyPlateSolveBinning;
            set
            {
                if (fullSkyPlateSolveBinning != value)
                {
                    fullSkyPlateSolveBinning = value;
                    optionsAccessor.SetValueInt32(nameof(FullSkyPlateSolveBinning), fullSkyPlateSolveBinning);
                    RaisePropertyChanged();
                }
            }
        }

        private int fullSkyPlateSolveGain;

        public int FullSkyPlateSolveGain
        {
            get => fullSkyPlateSolveGain;
            set
            {
                if (fullSkyPlateSolveGain != value)
                {
                    fullSkyPlateSolveGain = value;
                    optionsAccessor.SetValueInt32(nameof(FullSkyPlateSolveGain), fullSkyPlateSolveGain);
                    RaisePropertyChanged();
                }
            }
        }

        private int fullSkyPlateSolveOffset;

        public int FullSkyPlateSolveOffset
        {
            get => fullSkyPlateSolveOffset;
            set
            {
                if (fullSkyPlateSolveOffset != value)
                {
                    fullSkyPlateSolveOffset = value;
                    optionsAccessor.SetValueInt32(nameof(FullSkyPlateSolveOffset), fullSkyPlateSolveOffset);
                    RaisePropertyChanged();
                }
            }
        }

        private bool useDedicatedMLPTPlateSolveSettings;

        public bool UseDedicatedMLPTPlateSolveSettings
        {
            get => useDedicatedMLPTPlateSolveSettings;
            set
            {
                if (useDedicatedMLPTPlateSolveSettings != value)
                {
                    useDedicatedMLPTPlateSolveSettings = value;
                    optionsAccessor.SetValueBoolean(nameof(UseDedicatedMLPTPlateSolveSettings), useDedicatedMLPTPlateSolveSettings);
                    RaisePropertyChanged();
                }
            }
        }

        private double mlptPlateSolveExposureTime;

        public double MLPTPlateSolveExposureTime
        {
            get => mlptPlateSolveExposureTime;
            set
            {
                if (Math.Abs(mlptPlateSolveExposureTime - value) > double.Epsilon)
                {
                    mlptPlateSolveExposureTime = value;
                    optionsAccessor.SetValueDouble(nameof(MLPTPlateSolveExposureTime), mlptPlateSolveExposureTime);
                    RaisePropertyChanged();
                }
            }
        }

        private int mlptPlateSolveBinning;

        public int MLPTPlateSolveBinning
        {
            get => mlptPlateSolveBinning;
            set
            {
                if (mlptPlateSolveBinning != value)
                {
                    mlptPlateSolveBinning = value;
                    optionsAccessor.SetValueInt32(nameof(MLPTPlateSolveBinning), mlptPlateSolveBinning);
                    RaisePropertyChanged();
                }
            }
        }

        private int mlptPlateSolveGain;

        public int MLPTPlateSolveGain
        {
            get => mlptPlateSolveGain;
            set
            {
                if (mlptPlateSolveGain != value)
                {
                    mlptPlateSolveGain = value;
                    optionsAccessor.SetValueInt32(nameof(MLPTPlateSolveGain), mlptPlateSolveGain);
                    RaisePropertyChanged();
                }
            }
        }

        private int mlptPlateSolveOffset;

        public int MLPTPlateSolveOffset
        {
            get => mlptPlateSolveOffset;
            set
            {
                if (mlptPlateSolveOffset != value)
                {
                    mlptPlateSolveOffset = value;
                    optionsAccessor.SetValueInt32(nameof(MLPTPlateSolveOffset), mlptPlateSolveOffset);
                    RaisePropertyChanged();
                }
            }
        }

        private double GetDefaultProfilePlateSolveExposureTime() => profileService?.ActiveProfile?.PlateSolveSettings?.ExposureTime ?? 1.0d;

        private int GetDefaultProfilePlateSolveBinning() => profileService?.ActiveProfile?.PlateSolveSettings?.Binning ?? 1;

        private int GetDefaultProfilePlateSolveGain() => profileService?.ActiveProfile?.PlateSolveSettings?.Gain ?? -1;

        private int GetDefaultProfilePlateSolveOffset() => profileService?.ActiveProfile?.CameraSettings?.Offset ?? -1;

        private double syncEastAltitude;

        public double SyncEastAltitude
        {
            get => syncEastAltitude;
            set
            {
                if (syncEastAltitude != value)
                {
                    syncEastAltitude = value;
                    optionsAccessor.SetValueDouble("SyncEastAltitude", syncEastAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private double syncWestAltitude;

        public double SyncWestAltitude
        {
            get => syncWestAltitude;
            set
            {
                if (syncWestAltitude != value)
                {
                    syncWestAltitude = value;
                    optionsAccessor.SetValueDouble("SyncWestAltitude", syncWestAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private double syncEastAzimuth;

        public double SyncEastAzimuth
        {
            get => syncEastAzimuth;
            set
            {
                if (syncEastAzimuth != value)
                {
                    syncEastAzimuth = value;
                    optionsAccessor.SetValueDouble("SyncEastAzimuth", syncEastAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private double syncWestAzimuth;

        public double SyncWestAzimuth
        {
            get => syncWestAzimuth;
            set
            {
                if (syncWestAzimuth != value)
                {
                    syncWestAzimuth = value;
                    optionsAccessor.SetValueDouble("SyncWestAzimuth", syncWestAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private double refEastAltitude;

        public double RefEastAltitude
        {
            get => refEastAltitude;
            set
            {
                if (refEastAltitude != value)
                {
                    refEastAltitude = value;
                    optionsAccessor.SetValueDouble("RefEastAltitude", refEastAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private double refWestAltitude;

        public double RefWestAltitude
        {
            get => refWestAltitude;
            set
            {
                if (refWestAltitude != value)
                {
                    refWestAltitude = value;
                    optionsAccessor.SetValueDouble("RefWestAltitude", refWestAltitude);
                    RaisePropertyChanged();
                }
            }
        }

        private double refEastAzimuth;

        public double RefEastAzimuth
        {
            get => refEastAzimuth;
            set
            {
                if (refEastAzimuth != value)
                {
                    refEastAzimuth = value;
                    optionsAccessor.SetValueDouble("RefEastAzimuth", refEastAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private double refWestAzimuth;

        public double RefWestAzimuth
        {
            get => refWestAzimuth;
            set
            {
                if (refWestAzimuth != value)
                {
                    refWestAzimuth = value;
                    optionsAccessor.SetValueDouble("RefWestAzimuth", refWestAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private int builderNumRetries;

        public int BuilderNumRetries
        {
            get => builderNumRetries;
            set
            {
                if (builderNumRetries != value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentException("BuilderNumRetries must be non-negative", "BuilderNumRetries");
                    }
                    builderNumRetries = value;
                    optionsAccessor.SetValueInt32("BuilderNumRetries", builderNumRetries);
                    RaisePropertyChanged();
                }
            }
        }

        private double maxPointRMS;

        public double MaxPointRMS
        {
            get => maxPointRMS;
            set
            {
                if (maxPointRMS != value)
                {
                    if (value <= 0.0d || double.IsNaN(value))
                    {
                        maxPointRMS = double.NaN;
                    }
                    else
                    {
                        maxPointRMS = value;
                    }
                    optionsAccessor.SetValueDouble("MaxPointRMS", maxPointRMS);
                    RaisePropertyChanged();
                }
            }
        }

        private bool logCommands;

        public bool LogCommands
        {
            get => logCommands;
            set
            {
                if (logCommands != value)
                {
                    logCommands = value;
                    optionsAccessor.SetValueBoolean("LogCommands", logCommands);
                    RaisePropertyChanged();
                }
            }
        }

        private bool allowBlindSolves;

        public bool AllowBlindSolves
        {
            get => allowBlindSolves;
            set
            {
                if (allowBlindSolves != value)
                {
                    allowBlindSolves = value;
                    optionsAccessor.SetValueBoolean("AllowBlindSolves", allowBlindSolves);
                    RaisePropertyChanged();
                }
            }
        }

        private int maxConcurrency;

        public int MaxConcurrency
        {
            get => maxConcurrency;
            set
            {
                if (maxConcurrency != value)
                {
                    if (maxConcurrency < 0)
                    {
                        throw new ArgumentException("MaxConcurrency must be non-negative", "MaxConcurrency");
                    }
                    maxConcurrency = value;
                    optionsAccessor.SetValueInt32("MaxConcurrency", maxConcurrency);
                    RaisePropertyChanged();
                }
            }
        }

        private bool showRemovedPoints;

        public bool ShowRemovedPoints
        {
            get => showRemovedPoints;
            set
            {
                if (showRemovedPoints != value)
                {
                    showRemovedPoints = value;
                    optionsAccessor.SetValueBoolean("ShowRemovedPoints", showRemovedPoints);
                    RaisePropertyChanged();
                }
            }
        }

        private double chartPointSize;

        public double ChartPointSize
        {
            get => chartPointSize;
            set
            {
                if (Math.Abs(chartPointSize - value) > double.Epsilon)
                {
                    if (value <= 0.0d || value > 20.0d)
                    {
                        throw new ArgumentException("ChartPointSize must be greater than 0 and no more than 20", nameof(ChartPointSize));
                    }

                    chartPointSize = value;
                    optionsAccessor.SetValueDouble("ChartPointSize", chartPointSize);
                    RaisePropertyChanged();
                }
            }
        }

        private bool showHorizon;

        public bool ShowHorizon
        {
            get => showHorizon;
            set
            {
                if (showHorizon != value)
                {
                    showHorizon = value;
                    optionsAccessor.SetValueBoolean("ShowHorizon", showHorizon);
                    RaisePropertyChanged();
                }
            }
        }

        private bool showCardinalLabels;

        public bool ShowCardinalLabels
        {
            get => showCardinalLabels;
            set
            {
                if (showCardinalLabels != value)
                {
                    showCardinalLabels = value;
                    optionsAccessor.SetValueBoolean("ShowCardinalLabels", showCardinalLabels);
                    RaisePropertyChanged();
                }
            }
        }

        private bool showCelestialPole;

        public bool ShowCelestialPole
        {
            get => showCelestialPole;
            set
            {
                if (showCelestialPole != value)
                {
                    showCelestialPole = value;
                    optionsAccessor.SetValueBoolean("ShowCelestialPole", showCelestialPole);
                    RaisePropertyChanged();
                }
            }
        }

        private bool showMeridianLimitsInCharts;

        public bool ShowMeridianLimitsInCharts
        {
            get => showMeridianLimitsInCharts;
            set
            {
                if (showMeridianLimitsInCharts != value)
                {
                    showMeridianLimitsInCharts = value;
                    optionsAccessor.SetValueBoolean("ShowMeridianLimitsInCharts", showMeridianLimitsInCharts);
                    RaisePropertyChanged();
                }
            }
        }

        private int horizonTransparencyPercent;

        public int HorizonTransparencyPercent
        {
            get => horizonTransparencyPercent;
            set
            {
                if (horizonTransparencyPercent != value)
                {
                    if (value < 0 || value > 100)
                    {
                        throw new ArgumentException("HorizonTransparencyPercent must be between 0 and 100", nameof(HorizonTransparencyPercent));
                    }

                    horizonTransparencyPercent = value;
                    optionsAccessor.SetValueInt32("HorizonTransparencyPercent", horizonTransparencyPercent);
                    RaisePropertyChanged();
                }
            }
        }

        private int maxFailedPoints;

        public int MaxFailedPoints
        {
            get => maxFailedPoints;
            set
            {
                if (maxFailedPoints != value)
                {
                    if (maxFailedPoints < 0)
                    {
                        throw new ArgumentException("MaxFailedPoints must be non-negative", "MaxFailedPoints");
                    }
                    maxFailedPoints = value;
                    optionsAccessor.SetValueInt32("MaxFailedPoints", maxFailedPoints);
                    RaisePropertyChanged();
                }
            }
        }

        private string siderealTrackStartTimeProvider;

        public string SiderealTrackStartTimeProvider
        {
            get => siderealTrackStartTimeProvider;
            set
            {
                if (siderealTrackStartTimeProvider != value)
                {
                    siderealTrackStartTimeProvider = value;
                    optionsAccessor.SetValueString("SiderealTrackStartTimeProvider", siderealTrackStartTimeProvider);
                    RaisePropertyChanged();
                }
            }
        }

        private string siderealTrackEndTimeProvider;

        public string SiderealTrackEndTimeProvider
        {
            get => siderealTrackEndTimeProvider;
            set
            {
                if (siderealTrackEndTimeProvider != value)
                {
                    siderealTrackEndTimeProvider = value;
                    optionsAccessor.SetValueString("SiderealTrackEndTimeProvider", siderealTrackEndTimeProvider);
                    RaisePropertyChanged();
                }
            }
        }

        private bool removeHighRMSPointsAfterBuild;

        public bool RemoveHighRMSPointsAfterBuild
        {
            get => removeHighRMSPointsAfterBuild;
            set
            {
                if (removeHighRMSPointsAfterBuild != value)
                {
                    removeHighRMSPointsAfterBuild = value;
                    optionsAccessor.SetValueBoolean("RemoveHighRMSPointsAfterBuild", removeHighRMSPointsAfterBuild);
                    RaisePropertyChanged();
                }
            }
        }

        private double plateSolveSubframePercentage;

        public double PlateSolveSubframePercentage
        {
            get => plateSolveSubframePercentage;
            set
            {
                if (plateSolveSubframePercentage != value)
                {
                    if (value <= 0.0d || value > 1.0d)
                    {
                        throw new ArgumentException($"PlateSolveSubframePercentage must be within (0, 1]", "PlateSolveSubframePercentage");
                    }

                    plateSolveSubframePercentage = value;
                    optionsAccessor.SetValueDouble("PlateSolveSubframePercentage", plateSolveSubframePercentage);
                    RaisePropertyChanged();
                }
            }
        }

        private bool alternateDirectionsBetweenIterations;

        public bool AlternateDirectionsBetweenIterations
        {
            get => alternateDirectionsBetweenIterations;
            set
            {
                if (alternateDirectionsBetweenIterations != value)
                {
                    alternateDirectionsBetweenIterations = value;
                    optionsAccessor.SetValueBoolean("AlternateDirectionsBetweenIterations", alternateDirectionsBetweenIterations);
                    RaisePropertyChanged();
                }
            }
        }

        private double minPointAzimuth;

        public double MinPointAzimuth
        {
            get => minPointAzimuth;
            set
            {
                if (minPointAzimuth != value)
                {
                    if (value <= 0.0d || double.IsNaN(value))
                    {
                        minPointAzimuth = 0.0d;
                    }
                    else if (value >= 360.0d)
                    {
                        minPointAzimuth = 360.0d;
                    }
                    else
                    {
                        minPointAzimuth = value;
                    }

                    optionsAccessor.SetValueDouble("MinPointAzimuth", minPointAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private double maxPointAzimuth;

        public double MaxPointAzimuth
        {
            get => maxPointAzimuth;
            set
            {
                if (maxPointAzimuth != value)
                {
                    if (value <= 0.0d || double.IsNaN(value))
                    {
                        maxPointAzimuth = 0.0d;
                    }
                    else if (value >= 360.0d)
                    {
                        maxPointAzimuth = 360.0d;
                    }
                    else
                    {
                        maxPointAzimuth = value;
                    }

                    optionsAccessor.SetValueDouble("MaxPointAzimuth", maxPointAzimuth);
                    RaisePropertyChanged();
                }
            }
        }

        private double minDistanceToHorizonDegrees;

        public double MinDistanceToHorizonDegrees
        {
            get => minDistanceToHorizonDegrees;
            set
            {
                if (Math.Abs(minDistanceToHorizonDegrees - value) > double.Epsilon)
                {
                    if (value < 0.0d || value > 90.0d)
                    {
                        throw new ArgumentException("MinDistanceToHorizonDegrees must be between 0 and 90, inclusive", nameof(MinDistanceToHorizonDegrees));
                    }

                    minDistanceToHorizonDegrees = value;
                    optionsAccessor.SetValueDouble("MinDistanceToHorizonDegrees", minDistanceToHorizonDegrees);
                    RaisePropertyChanged();
                }
            }
        }

        private DateTime lastMLPT;

        private bool enableMLPTDebugSimulator;

        public bool EnableMLPTDebugSimulator
        {
            get => enableMLPTDebugSimulator;
            set
            {
                if (enableMLPTDebugSimulator != value)
                {
                    enableMLPTDebugSimulator = value;
                    optionsAccessor.SetValueBoolean(nameof(EnableMLPTDebugSimulator), enableMLPTDebugSimulator);
                    RaisePropertyChanged();
                }
            }
        }

        public DateTime LastMLPT
        {
            get => lastMLPT;
            set
            {
                if (lastMLPT != value)
                {
                    lastMLPT = value;
                    optionsAccessor.SetValueDateTime("LastMLPT", lastMLPT);
                    RaisePropertyChanged();
                }
            }
        }

        private double activeMLPTDurationSeconds;

        public double ActiveMLPTDurationSeconds
        {
            get => activeMLPTDurationSeconds;
            set
            {
                if (Math.Abs(activeMLPTDurationSeconds - value) > double.Epsilon)
                {
                    activeMLPTDurationSeconds = value;
                    optionsAccessor.SetValueDouble(nameof(ActiveMLPTDurationSeconds), activeMLPTDurationSeconds);
                    RaisePropertyChanged();
                }
            }
        }

        private int activeMLPTPointCount;

        public int ActiveMLPTPointCount
        {
            get => activeMLPTPointCount;
            set
            {
                if (activeMLPTPointCount != value)
                {
                    activeMLPTPointCount = value;
                    optionsAccessor.SetValueInt32(nameof(ActiveMLPTPointCount), activeMLPTPointCount);
                    RaisePropertyChanged();
                }
            }
        }

        private bool isLegacyDDM;

        public bool IsLegacyDDM
        {
            get => isLegacyDDM;
            set
            {
                if (isLegacyDDM != value)
                {
                    isLegacyDDM = value;
                    optionsAccessor.SetValueBoolean("IsLegacyDDM", isLegacyDDM);
                    RaisePropertyChanged();
                }
            }
        }

        private bool domeControlNINA;

        public bool DomeControlNINA
        {
            get => domeControlNINA;
            set
            {
                if (domeControlNINA != value)
                {
                    domeControlNINA = value;
                    optionsAccessor.SetValueBoolean("DomeControlNINA", domeControlNINA);
                    RaisePropertyChanged();
                }
            }
        }

        private bool disableRefractionCorrection;

        public bool DisableRefractionCorrection
        {
            get => disableRefractionCorrection;
            set
            {
                if (disableRefractionCorrection != value)
                {
                    disableRefractionCorrection = value;
                    optionsAccessor.SetValueBoolean("DisableRefractionCorrection", disableRefractionCorrection);
                    RaisePropertyChanged();
                }
            }
        }

        private string ipAddress;

        public string IPAddress
        {
            get => ipAddress;
            set
            {
                if (ipAddress != value)
                {
                    ipAddress = value;
                    optionsAccessor.SetValueString("IPAddress", ipAddress);
                    RaisePropertyChanged();
                }
            }
        }

        private string macAddress;

        public string MACAddress
        {
            get => macAddress;
            set
            {
                if (macAddress != value)
                {
                    macAddress = value;
                    optionsAccessor.SetValueString("MACAddress", macAddress);
                    RaisePropertyChanged();
                }
            }
        }

        private int port;

        public int Port
        {
            get => port;
            set
            {
                if (port != value)
                {
                    if (value < 0 || value > short.MaxValue)
                    {
                        throw new ArgumentException($"Port must be between (0, {short.MaxValue})", "Port");
                    }
                    port = value;
                    optionsAccessor.SetValueInt32("Port", port);
                    RaisePropertyChanged();
                }
            }
        }

        private string wolBroadcastIP;

        public string WolBroadcastIP
        {
            get => wolBroadcastIP;
            set
            {
                if (wolBroadcastIP != value)
                {
                    wolBroadcastIP = value;
                    optionsAccessor.SetValueString("WolBroadcastIP", wolBroadcastIP);
                    RaisePropertyChanged();
                }
            }
        }

        private string driverID;

        public string DriverID
        {
            get => driverID;
            set
            {
                if (driverID != value)
                {
                    driverID = value;
                    optionsAccessor.SetValueString("DriverID", driverID);
                    RaisePropertyChanged();
                }
            }
        }

        private double decJitterSigmaDegrees;

        public double DecJitterSigmaDegrees
        {
            get => decJitterSigmaDegrees;
            set
            {
                if (decJitterSigmaDegrees != value)
                {
                    if (value < 0.0d || double.IsNaN(value))
                    {
                        decJitterSigmaDegrees = 0.0d;
                    }
                    else if (value >= 10.0d)
                    {
                        decJitterSigmaDegrees = 10.0d;
                    }
                    else
                    {
                        decJitterSigmaDegrees = value;
                    }

                    optionsAccessor.SetValueDouble(nameof(DecJitterSigmaDegrees), decJitterSigmaDegrees);
                    RaisePropertyChanged();
                }
            }
        }

        private string poxOutputDirectory;



        public string POXOutputDirectory {
            get => poxOutputDirectory;
            set 
            {
                if (poxOutputDirectory != value)
                {
                    // Validate that the directory exists


                    if (value == this.DefaultASAPointingPicsPath())
                    {
                        // Intentionally left blank:
                        // Always allow the default path, will be created during first save operation.
                        // Otherwise we would get an exception when resetting the options to its default.
                    } else if (!Directory.Exists(value))
                    {
                        throw new DirectoryNotFoundException($"The specified POX output directory does not exist: {value}");
                    }

                    poxOutputDirectory = value;
                    optionsAccessor.SetValueString("POXOutputDirectory", poxOutputDirectory);
                    RaisePropertyChanged();
                }
            }
        }

        private void ShowSelectPOXOutputDirectoryDialog()
        {
            var diag = new OpenFolderDialog();
            diag.InitialDirectory = POXOutputDirectory;
            if (diag.ShowDialog() == true)
            {
                POXOutputDirectory = diag.FolderName;
            }

        }

    }
}