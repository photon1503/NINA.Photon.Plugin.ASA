#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Converters;
using NINA.Photon.Plugin.ASA.Model;
using System;
using System.ComponentModel;
using System.Globalization;

namespace NINA.Photon.Plugin.ASA.Model
{
    [TypeConverter(typeof(EnumStaticDescriptionTypeConverter))]
    public enum ModelPointStateEnum
    {
        [Description("Generated")]
        Generated = 0,

        [Description("Next")]
        UpNext = 1,

        [Description("Exposing Image")]
        Exposing = 2,

        [Description("Processing")]
        Processing = 3,

        [Description("Added to Model")]
        AddedToModel = 4,

        [Description("Failed")]
        Failed = 97,

        [Description("High RMS")]
        FailedRMS = 98,

        [Description("Outside Altitude Bounds")]
        OutsideAltitudeBounds = 99,

        [Description("Outside Azimuth Bounds")]
        OutsideAzimuthBounds = 100,

        [Description("Below Horizon")]
        BelowHorizon = 101,
    }

    public class ModelPoint : BaseINPC
    {
        private readonly ITelescopeMediator telescopeMediator;

        public ModelPoint(ITelescopeMediator telescopeMediator)
        {
            this.telescopeMediator = telescopeMediator;
        }

        private int modelIndex;

        public int ModelIndex
        {
            get => modelIndex;
            set
            {
                if (modelIndex != value)
                {
                    modelIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridBandIndex = -1;

        public int AutoGridBandIndex
        {
            get => autoGridBandIndex;
            set
            {
                if (autoGridBandIndex != value)
                {
                    autoGridBandIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridBandSequence = -1;

        public int AutoGridBandSequence
        {
            get => autoGridBandSequence;
            set
            {
                if (autoGridBandSequence != value)
                {
                    autoGridBandSequence = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridBandPointCount = -1;

        public int AutoGridBandPointCount
        {
            get => autoGridBandPointCount;
            set
            {
                if (autoGridBandPointCount != value)
                {
                    autoGridBandPointCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridBandEastToWestOrder = -1;

        public int AutoGridBandEastToWestOrder
        {
            get => autoGridBandEastToWestOrder;
            set
            {
                if (autoGridBandEastToWestOrder != value)
                {
                    autoGridBandEastToWestOrder = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridRadialBandIndex = -1;

        public int AutoGridRadialBandIndex
        {
            get => autoGridRadialBandIndex;
            set
            {
                if (autoGridRadialBandIndex != value)
                {
                    autoGridRadialBandIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int autoGridRadialBandEastToWestOrder = -1;

        public int AutoGridRadialBandEastToWestOrder
        {
            get => autoGridRadialBandEastToWestOrder;
            set
            {
                if (autoGridRadialBandEastToWestOrder != value)
                {
                    autoGridRadialBandEastToWestOrder = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool isSyncPoint = false;

        public bool IsSyncPoint
        {
            get => isSyncPoint;
            set
            {
                if (isSyncPoint != value)
                {
                    isSyncPoint = value;
                    RaisePropertyChanged();
                }
            }
        }

        private PierSide expectedDomeSideOfPier = PierSide.pierUnknown;

        public PierSide ExpectedDomeSideOfPier
        {
            get => expectedDomeSideOfPier;
            set
            {
                if (expectedDomeSideOfPier != value)
                {
                    expectedDomeSideOfPier = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double InvertedAltitude => 90.0 - Altitude;

        private double altitude;

        public double Altitude
        {
            get => altitude;
            set
            {
                if (altitude != value)
                {
                    altitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double azimuth;

        public double Azimuth
        {
            get => azimuth;
            set
            {
                if (azimuth != value)
                {
                    azimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double minDomeAzimuth = double.NaN;

        public double MinDomeAzimuth
        {
            get => minDomeAzimuth;
            set
            {
                if (minDomeAzimuth != value)
                {
                    minDomeAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double domeAzimuth = double.NaN;

        public double DomeAzimuth
        {
            get => domeAzimuth;
            set
            {
                if (domeAzimuth != value)
                {
                    domeAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double domeAltitude = double.NaN;

        public double DomeAltitude
        {
            get => domeAltitude;
            set
            {
                if (domeAltitude != value)
                {
                    domeAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double maxDomeAzimuth = double.NaN;

        public double MaxDomeAzimuth
        {
            get => maxDomeAzimuth;
            set
            {
                if (maxDomeAzimuth != value)
                {
                    maxDomeAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ModelPointStateEnum modelPointState;

        public ModelPointStateEnum ModelPointState
        {
            get => modelPointState;
            set
            {
                if (modelPointState != value)
                {
                    modelPointState = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ModelPointStateString));
                }
            }
        }

        public string ModelPointStateString
        {
            get
            {
                var fi = typeof(ModelPointStateEnum).GetField(ModelPointState.ToString());
                var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                return attributes[0].Description;
            }
        }

        private double mountReportedLocalSiderealTime = double.NaN;

        public double MountReportedLocalSiderealTime
        {
            get => mountReportedLocalSiderealTime;
            set
            {
                mountReportedLocalSiderealTime = value;
                RaisePropertyChanged();
            }
        }

        private double mountReportedRightAscension = double.NaN;

        public double MountReportedRightAscension
        {
            get => mountReportedRightAscension;
            set
            {
                mountReportedRightAscension = value;
                RaisePropertyChanged();
            }
        }

        private double mountReportedDeclination = double.NaN;

        public double MountReportedDeclination
        {
            get => mountReportedDeclination;
            set
            {
                mountReportedDeclination = value;
                RaisePropertyChanged();
            }
        }

        private PierSide mountReportedSideOfPier = PierSide.pierUnknown;

        public PierSide MountReportedSideOfPier
        {
            get => mountReportedSideOfPier;
            set
            {
                if (mountReportedSideOfPier != value)
                {
                    mountReportedSideOfPier = value;
                    RaisePropertyChanged();
                }
            }
        }

        private Coordinates plateSolvedCoordinates;

        public Coordinates PlateSolvedCoordinates
        {
            get => plateSolvedCoordinates;
            set
            {
                plateSolvedCoordinates = value?.Transform(Epoch.JNOW);
                RaisePropertyChanged();
            }
        }

        private double plateSolvedRightAscension = double.NaN;

        public double PlateSolvedRightAscension
        {
            get => plateSolvedRightAscension;
            set
            {
                plateSolvedRightAscension = value;
                RaisePropertyChanged();
            }
        }

        private double plateSolvedDeclination = double.NaN;

        public double PlateSolvedDeclination
        {
            get => plateSolvedDeclination;
            set
            {
                plateSolvedDeclination = value;
                RaisePropertyChanged();
            }
        }

        private double rmsError = double.NaN;

        public double RMSError
        {
            get => rmsError;
            set
            {
                if (rmsError != value)
                {
                    rmsError = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(RMSErrorString));
                }
            }
        }

        public string RMSErrorString
        {
            get
            {
                if (double.IsNaN(rmsError))
                {
                    return "-";
                }
                return rmsError.ToString("0.0##", CultureInfo.CurrentUICulture);
            }
        }

        private DateTime captureTime = DateTime.MinValue;

        public DateTime CaptureTime
        {
            get => captureTime;
            set
            {
                if (captureTime != value)
                {
                    captureTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TopocentricCoordinates ToTopocentric(ICustomDateTime dateTime)
        {
            var telescopeInfo = telescopeMediator.GetInfo();
            return new TopocentricCoordinates(
                azimuth: Angle.ByDegree(azimuth),
                altitude: Angle.ByDegree(altitude),
                latitude: Angle.ByDegree(telescopeInfo.SiteLatitude),
                longitude: Angle.ByDegree(telescopeInfo.SiteLongitude),
                elevation: telescopeInfo.SiteElevation,
                dateTime: dateTime);
        }

        public Coordinates ToCelestial(double pressurehPa, double tempCelcius, double relativeHumidity, double wavelength, ICustomDateTime dateTime)
        {
            return ToTopocentric(dateTime).Transform(Epoch.JNOW, pressurehPa: pressurehPa, tempCelcius: tempCelcius, relativeHumidity: relativeHumidity, wavelength: wavelength);
        }

        public override string ToString()
        {
            return $"Alt={Altitude}, Az={Azimuth}, State={ModelPointState}, RMSError={RMSError}, ModelIndex={ModelIndex}, AutoGridBandIndex={AutoGridBandIndex}, AutoGridBandSequence={AutoGridBandSequence}, AutoGridBandPointCount={AutoGridBandPointCount}, AutoGridBandEastToWestOrder={AutoGridBandEastToWestOrder}, AutoGridRadialBandIndex={AutoGridRadialBandIndex}, AutoGridRadialBandEastToWestOrder={AutoGridRadialBandEastToWestOrder}, MountRA={MountReportedRightAscension}, MountDEC={MountReportedDeclination}, MountLST={MountReportedLocalSiderealTime}, MountPier={MountReportedSideOfPier}, SolvedCoordinates={PlateSolvedCoordinates}, CaptureTime={CaptureTime}, ExpectedDomeSideOfPier={ExpectedDomeSideOfPier}";
        }

        public ModelPoint Clone()
        {
            return new ModelPoint(telescopeMediator)
            {
                ModelIndex = ModelIndex,
                AutoGridBandIndex = AutoGridBandIndex,
                AutoGridBandSequence = AutoGridBandSequence,
                AutoGridBandPointCount = AutoGridBandPointCount,
                AutoGridBandEastToWestOrder = AutoGridBandEastToWestOrder,
                AutoGridRadialBandIndex = AutoGridRadialBandIndex,
                AutoGridRadialBandEastToWestOrder = AutoGridRadialBandEastToWestOrder,
                ExpectedDomeSideOfPier = ExpectedDomeSideOfPier,
                Altitude = Altitude,
                Azimuth = Azimuth,
                MinDomeAzimuth = MinDomeAzimuth,
                MaxDomeAzimuth = MaxDomeAzimuth,
                DomeAzimuth = DomeAzimuth,
                DomeAltitude = DomeAltitude,
                ModelPointState = ModelPointState,
                MountReportedLocalSiderealTime = MountReportedLocalSiderealTime,
                MountReportedRightAscension = MountReportedRightAscension,
                MountReportedDeclination = MountReportedDeclination,
                MountReportedSideOfPier = MountReportedSideOfPier,
                PlateSolvedCoordinates = PlateSolvedCoordinates?.Clone(),
                PlateSolvedRightAscension = PlateSolvedRightAscension,
                PlateSolvedDeclination = PlateSolvedDeclination,
                RMSError = RMSError,
                CaptureTime = CaptureTime,
                IsSyncPoint = IsSyncPoint
            };
        }

        public void CopyFrom(ModelPoint p)
        {
            ModelIndex = p.ModelIndex;
            AutoGridBandIndex = p.AutoGridBandIndex;
            AutoGridBandSequence = p.AutoGridBandSequence;
            AutoGridBandPointCount = p.AutoGridBandPointCount;
            AutoGridBandEastToWestOrder = p.AutoGridBandEastToWestOrder;
            AutoGridRadialBandIndex = p.AutoGridRadialBandIndex;
            AutoGridRadialBandEastToWestOrder = p.AutoGridRadialBandEastToWestOrder;
            ExpectedDomeSideOfPier = p.ExpectedDomeSideOfPier;
            Altitude = p.Altitude;
            Azimuth = p.Azimuth;
            MinDomeAzimuth = p.MinDomeAzimuth;
            MaxDomeAzimuth = p.MaxDomeAzimuth;
            DomeAzimuth = p.DomeAzimuth;
            DomeAltitude = p.DomeAltitude;
            ModelPointState = p.ModelPointState;
            MountReportedLocalSiderealTime = p.MountReportedLocalSiderealTime;
            MountReportedRightAscension = p.MountReportedRightAscension;
            MountReportedDeclination = p.MountReportedDeclination;
            MountReportedSideOfPier = p.MountReportedSideOfPier;
            PlateSolvedCoordinates = p.PlateSolvedCoordinates?.Clone();
            PlateSolvedRightAscension = p.PlateSolvedRightAscension;
            PlateSolvedDeclination = p.PlateSolvedDeclination;
            RMSError = p.RMSError;
            CaptureTime = p.CaptureTime;
            IsSyncPoint = p.IsSyncPoint;
        }
    }
}