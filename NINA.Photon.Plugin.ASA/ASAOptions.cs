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
        private readonly PluginOptionsAccessor optionsAccessor;

        public ASAOptions(IProfileService profileService)
        {
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
            autoGridPathOrderingMode = optionsAccessor.GetValueEnum("AutoGridPathOrderingMode", AutoGridPathOrderingModeEnum.LegacyAzimuthSweep);
            autoGridDesiredPointCount = optionsAccessor.GetValueInt32("AutoGridDesiredPointCount", 195);
            siderealTrackStartOffsetMinutes = optionsAccessor.GetValueInt32("SiderealTrackStartOffsetMinutes", 0);
            siderealTrackEndOffsetMinutes = optionsAccessor.GetValueInt32("SiderealTrackEndOffsetMinutes", 0);
            siderealTrackRADeltaDegrees = optionsAccessor.GetValueDouble("SiderealTrackRADeltaDegrees", 1.5d);
            domeShutterWidth_mm = optionsAccessor.GetValueInt32("DomeShutterWidth_mm", 0);
            minimizeDomeMovementEnabled = optionsAccessor.GetValueBoolean("MinimizeDomeMovementEnabled", true);
            minimizeMeridianFlipsEnabled = optionsAccessor.GetValueBoolean("MinimizeMeridianFlipsEnabled", true);
            modelPointGenerationType = optionsAccessor.GetValueEnum("ModelPointGenerationType", ModelPointGenerationTypeEnum.GoldenSpiral);
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
            lastMLPT = optionsAccessor.GetValueDateTime("LastMLPT", DateTime.MinValue);
            highAltitudeStars = optionsAccessor.GetValueInt32("HighAltitudeStars", 10);
            highAltitudeMin = optionsAccessor.GetValueInt32("HighAltitudeMin", 70);
            highAltitudeMax = optionsAccessor.GetValueInt32("HighAltitudeMax", 89);
            useSync = optionsAccessor.GetValueBoolean("UseSync", false);
            syncEveryHA = optionsAccessor.GetValueDouble("SyncEveryHA", 30.0d);
            syncEastAltitude = optionsAccessor.GetValueDouble("SyncEastAltitude", 65.0d);
            syncWestAltitude = optionsAccessor.GetValueDouble("SyncWestAltitude", 65.0d);
            refEastAltitude = optionsAccessor.GetValueDouble("RefEastAltitude", 35.0d);
            refWestAltitude = optionsAccessor.GetValueDouble("RefWestAltitude", 35.0d);
            syncEastAzimuth = optionsAccessor.GetValueDouble("SyncEastAzimuth", 90.0d);
            syncWestAzimuth = optionsAccessor.GetValueDouble("SyncWestAzimuth", 270.0d);
            refEastAzimuth = optionsAccessor.GetValueDouble("RefEastAzimuth", 90.0d);
            refWestAzimuth = optionsAccessor.GetValueDouble("RefWestAzimuth", 270.0d);
            poxOutputDirectory = optionsAccessor.GetValueString("POXOutputDirectory", DefaultASAPointingPicsPath());
            chartPointSize = optionsAccessor.GetValueDouble("ChartPointSize", 2.8d);
            showHorizon = optionsAccessor.GetValueBoolean("ShowHorizon", true);
            showCardinalLabels = optionsAccessor.GetValueBoolean("ShowCardinalLabels", false);
            showCelestialPole = optionsAccessor.GetValueBoolean("ShowCelestialPole", true);
            horizonTransparencyPercent = optionsAccessor.GetValueInt32("HorizonTransparencyPercent", 65);
        }

        public void ResetDefaults()
        {
            GoldenSpiralStarCount = 9;
            AutoGridRASpacingDegrees = 10.0d;
            AutoGridDecSpacingDegrees = 10.0d;
            AutoGridInputMode = AutoGridInputModeEnum.Spacing;
            AutoGridPathOrderingMode = AutoGridPathOrderingModeEnum.LegacyAzimuthSweep;
            AutoGridDesiredPointCount = 195;
            SiderealTrackStartOffsetMinutes = 0;
            SiderealTrackEndOffsetMinutes = 0;
            SiderealTrackRADeltaDegrees = 1.5d;
            DomeShutterWidth_mm = 0;
            MinimizeDomeMovementEnabled = true;
            MinimizeMeridianFlipsEnabled = true;
            ModelPointGenerationType = ModelPointGenerationTypeEnum.GoldenSpiral;
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
            AlternateDirectionsBetweenIterations = true;
            MinPointAzimuth = 0.5d;
            MaxPointAzimuth = 359.5d;
            MinDistanceToHorizonDegrees = 0.0d;
            DisableRefractionCorrection = false;
            IsLegacyDDM = true;
            DomeControlNINA = false;
            LastMLPT = DateTime.MinValue;
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
            SyncEveryHA = 30.0d;
            SyncEastAltitude = 65.0d;
            SyncWestAltitude = 65.0d;
            SyncEastAzimuth = 90.0d;
            SyncWestAzimuth = 270.0d;
            RefEastAltitude = 35.0d;
            RefWestAltitude = 35.0d;
            RefEastAzimuth = 90.0d;
            RefWestAzimuth = 270.0d;
            ChartPointSize = 2.8d;
            ShowHorizon = true;
            ShowCardinalLabels = false;
            ShowCelestialPole = true;
            HorizonTransparencyPercent = 65;

            POXOutputDirectory = DefaultASAPointingPicsPath();
        }

        private string DefaultASAPointingPicsPath()
        {
            var programdata = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            var filePath = System.IO.Path.Combine(programdata, "ASA", "Sequence", "PointingPics");

            return filePath;
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