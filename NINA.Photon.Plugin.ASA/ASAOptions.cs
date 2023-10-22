#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.ModelManagement;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;

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
        }

        private void InitializeOptions()
        {
            goldenSpiralStarCount = optionsAccessor.GetValueInt32("GoldenSpiralStarCount", 9);
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
            disableRefractionCorrection = optionsAccessor.GetValueBoolean("DisableRefractionCorrection", false);
            //ipAddress = optionsAccessor.GetValueString("IPAddress", "");
            //macAddress = optionsAccessor.GetValueString("MACAddress", "");
            //wolBroadcastIP = optionsAccessor.GetValueString("WolBroadcastIP", "");
            //port = optionsAccessor.GetValueInt32("Port", 3490);
            driverID = optionsAccessor.GetValueString("DriverID", "");
            decJitterSigmaDegrees = optionsAccessor.GetValueDouble(nameof(DecJitterSigmaDegrees), 1.0d);
            isLegacyDDM = optionsAccessor.GetValueBoolean("IsLegacyDDM", true);
        }

        public void ResetDefaults()
        {
            GoldenSpiralStarCount = 9;
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
            DisableRefractionCorrection = false;
            IsLegacyDDM = true;
            MACAddress = "";
            IPAddress = "";
            WolBroadcastIP = "";
            Port = 3490;
            DriverID = "";
            DecJitterSigmaDegrees = 1.0d;
        }

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

        private bool isLegacyDDM;

        public bool IsLegacyDDM
        {
            get => isLegacyDDM;
            set
            {
                if (isLegacyDDM != value)
                {
                    isLegacyDDM = value;
                    optionsAccessor.SetValueBoolean("DisableRefractionCorrection", isLegacyDDM);
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
    }
}