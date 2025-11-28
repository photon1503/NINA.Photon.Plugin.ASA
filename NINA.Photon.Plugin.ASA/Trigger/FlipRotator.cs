#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Astrometry.Interfaces;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.Trigger;
using NINA.Profile;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces;
using NINA.Photon.Plugin.ASA.SequenceItems;
using NINA.Core.Utility.WindowService;
using NINA.PlateSolving;
using NINA.WPF.Base.ViewModel.Equipment.Dome;
using NINA.Profile.Interfaces;
using NINA.Equipment.Interfaces;
using NINA.PlateSolving.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System.Windows.Media;
using System.Windows;

namespace NINA.Photon.Plugin.ASA.MLTP
{
    [ExportMetadata("Name", "Flip Rotator If Needed")]
    [ExportMetadata("Description", "Flips the rotator when reaching tracking limit")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FlipRotator : SequenceTrigger, IValidatable
    {
        private IASAOptions options;
        private readonly IMountMediator mountMediator;
        private IMount mount;
        private readonly IMountModelBuilderMediator mountModelBuilderMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly ICameraMediator cameraMediator;
        private readonly ITelescopeMediator telescopeMediator;
        protected IProfileService profileService;

        protected IApplicationStatusMediator applicationStatusMediator;

        protected IFocuserMediator focuserMediator;

        protected IGuiderMediator guiderMediator;

        protected IFilterWheelMediator filterWheelMediator;

        protected IDomeMediator domeMediator;
        protected IDomeFollower domeFollower;
        protected IRotatorMediator rotatorMediator;
        protected IPlateSolverFactory plateSolverFactory;
        protected IWindowServiceFactory windowServiceFactory;

        protected IImagingMediator imagingMediator;

        private double lastMechanicalPosition = 0;
        private bool initialized = false;

        public List<int> FlipAtChoices { get; } = new List<int> { 0, 180 };

        [ImportingConstructor]
        public FlipRotator(IProfileService profileService,
            ICameraMediator cameraMediator,
            ITelescopeMediator telescopeMediator,
            IApplicationStatusMediator applicationStatusMediator,
            IGuiderMediator guiderMediator,
            IFilterWheelMediator filterWheelMediator,
            IDomeMediator domeMediator,
            IDomeFollower domeFollower,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory,
            IImagingMediator imagingMediator,
            IRotatorMediator rotatorMediator
            )
        {
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;

            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.cameraMediator = cameraMediator;

            this.guiderMediator = guiderMediator;

            this.filterWheelMediator = filterWheelMediator;

            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;

            this.imagingMediator = imagingMediator;
            this.rotatorMediator = rotatorMediator;

            Amount = 90;
            TrackingLimit = 20;
        }

        private FlipRotator(FlipRotator cloneMe) : this(cloneMe.profileService, cloneMe.cameraMediator, cloneMe.telescopeMediator, cloneMe.applicationStatusMediator, cloneMe.guiderMediator, cloneMe.filterWheelMediator, cloneMe.domeMediator, cloneMe.domeFollower, cloneMe.plateSolverFactory, cloneMe.windowServiceFactory, cloneMe.imagingMediator, cloneMe.rotatorMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new FlipRotator(this)
            {
            };
        }

        private bool inherited;

        [JsonProperty]
        public bool Inherited
        {
            get => inherited;
            set
            {
                inherited = value;
                RaisePropertyChanged();
            }
        }

        private double trackingLimit;

        [JsonProperty]
        public double TrackingLimit
        {
            get => trackingLimit;
            set
            {
                trackingLimit = value;
                RaisePropertyChanged();
            }
        }

        private int flipAt;

        [JsonProperty]
        public int FlipAt
        {
            get => flipAt;
            set
            {
                flipAt = value;
                RaisePropertyChanged();
            }
        }

        private double mechanicalPosition;

        [JsonProperty]
        public double MechanicalPosition
        {
            get => mechanicalPosition;
            set
            {
                mechanicalPosition = value;
                RaisePropertyChanged();
            }
        }

        private double timeUntilFlip;

        public double TimeUntilFlip
        {
            get => timeUntilFlip;
            set
            {
                timeUntilFlip = value;
                RaisePropertyChanged();
            }
        }

        public override void Initialize()
        {
            if (!initialized)
            {
                try
                {
                    MechanicalPosition = Math.Round(rotatorMediator.GetInfo().MechanicalPosition, 3);
                }
                catch (Exception ex)
                {
                    MechanicalPosition = 0;
                }
                initialized = true;
            }
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            double targetPosition = rotatorMediator.GetInfo().MechanicalPosition + 180;
            if (targetPosition > 360)
            {
                targetPosition = targetPosition - 360;
            }
            Logger.Info($"Moving rotator to {targetPosition}");
            await rotatorMediator.MoveMechanical((float)targetPosition, token);
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues
        {
            get => issues;
            set
            {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private double amount;

        [JsonProperty]
        public double Amount
        {
            get => amount;
            set
            {
                amount = value;
                RaisePropertyChanged();
            }
        }

        private bool recenter = false;

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            bool _shouldTrigger = false;

            Logger.Info($"Checking rotator position. Current: {rotatorMediator.GetInfo().MechanicalPosition}, FlipAt: {FlipAt}, Limit: {TrackingLimit}");

            if (rotatorMediator.GetDevice() == null || !rotatorMediator.GetDevice().Connected)
            {
                return false;
            }

            double currentPosition = rotatorMediator.GetInfo().MechanicalPosition;
            double limit1 = FlipAt - TrackingLimit;
            double limit2 = FlipAt + TrackingLimit;

            TimeUntilFlip = Math.Round(CalculateTimeUntilFlip(currentPosition) / 60.0, 1);

            Logger.Info($"Flip will occur in approximately {timeUntilFlip:F1} seconds");

            // Normalize all angles to 0-360 range
            currentPosition = NormalizeAngle(currentPosition);
            limit1 = NormalizeAngle(limit1);
            limit2 = NormalizeAngle(limit2);

            if (limit1 <= limit2)
            {
                // Normal case: limits are within 0-360
                _shouldTrigger = currentPosition > limit1 && currentPosition < limit2;
            }
            else
            {
                // Wrap-around case: limits cross the 0/360 boundary
                _shouldTrigger = currentPosition > limit1 || currentPosition < limit2;
            }

            return _shouldTrigger;
        }

        private double CalculateTimeUntilFlip(double currentPosition)
        {
            try
            {
                // Get current mechanical position
                double currentPos = NormalizeAngle(currentPosition);

                var telescopeInfo = telescopeMediator.GetInfo();
                if (telescopeInfo == null)
                    return double.NaN;

                double alt = telescopeInfo.Altitude;
                double az = telescopeInfo.Azimuth;
                var lat = profileService.ActiveProfile.AstrometrySettings.Latitude;

                if (alt <= 5) // Increased minimum altitude to avoid numerical issues
                {
                    Logger.Warning("Target too low - cannot calculate derotator rate");
                    return double.NaN;
                }

                // Use analytical formula for rate of change of parallactic angle
                // dχ/dt = (cos(φ) * cos(Az)) / sin(Alt) [in radians per sidereal hour]
                double lat_rad = lat * Math.PI / 180.0;
                double az_rad = az * Math.PI / 180.0;
                double alt_rad = alt * Math.PI / 180.0;

                // Calculate rate in radians per sidereal HOUR
                double dchi_dt_rad_per_hour = (Math.Cos(lat_rad) * Math.Cos(az_rad)) / Math.Sin(alt_rad);

                // Convert to degrees per second:
                // 1. Convert radians to degrees: * (180/π)
                // 2. Convert per hour to per second: / 3600
                double parallacticRate = dchi_dt_rad_per_hour * (180.0 / Math.PI) / 3600.0;

                Logger.Info($"Mechanical: {currentPos}°, Parallactic rate: {parallacticRate:F6}°/s");

                // Determine which trigger boundary we're approaching
                double upperBoundary = NormalizeAngle(FlipAt + TrackingLimit); // 20°
                double lowerBoundary = NormalizeAngle(FlipAt - TrackingLimit); // 340°

                double timeToFlip;

                // For Alt-Az mount, the derotator typically moves counter-clockwise (negative rate)
                if (parallacticRate > 0) // Moving clockwise (unusual)
                {
                    if (currentPos < upperBoundary)
                    {
                        timeToFlip = (upperBoundary - currentPos) / parallacticRate;
                    }
                    else
                    {
                        timeToFlip = (upperBoundary + 360 - currentPos) / parallacticRate;
                    }
                }
                else if (parallacticRate < 0) // Moving counter-clockwise (typical)
                {
                    if (currentPos > lowerBoundary)
                    {
                        timeToFlip = (lowerBoundary - currentPos) / parallacticRate;
                    }
                    else
                    {
                        timeToFlip = (lowerBoundary - 360 - currentPos) / parallacticRate;
                    }
                }
                else
                {
                    return double.PositiveInfinity;
                }

                Logger.Info($"Time until flip: {timeToFlip:F0} seconds (≈{timeToFlip / 60:F1} minutes)");
                return timeToFlip;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating time until flip: {ex.Message}");
                return double.NaN;
            }
        }

        private (double alt, double az) ConvertRaDecToAltAz(double ra, double dec, double lat, double lon, double height, DateTime time)
        {
            // Implement RA/Dec to Alt/Az conversion using your preferred method
            // This typically involves calculating the Local Sidereal Time first
            // Here's a simplified placeholder implementation

            double lst = CalculateLocalSiderealTime(lon, time);
            double ha = lst - ra; // hour angle in degrees

            // Convert to radians for calculations
            double ha_rad = ha * Math.PI / 180.0;
            double dec_rad = dec * Math.PI / 180.0;
            double lat_rad = lat * Math.PI / 180.0;

            // Calculate altitude
            double sin_alt = Math.Sin(dec_rad) * Math.Sin(lat_rad) +
                            Math.Cos(dec_rad) * Math.Cos(lat_rad) * Math.Cos(ha_rad);
            double alt = Math.Asin(sin_alt) * 180.0 / Math.PI;

            // Calculate azimuth
            double cos_az = (Math.Sin(dec_rad) - Math.Sin(alt * Math.PI / 180.0) * Math.Sin(lat_rad)) /
                           (Math.Cos(alt * Math.PI / 180.0) * Math.Cos(lat_rad));
            double az = Math.Acos(cos_az) * 180.0 / Math.PI;

            // Adjust azimuth based on hour angle
            if (Math.Sin(ha_rad) > 0)
                az = 360 - az;

            return (alt, az);
        }

        private double CalculateParallacticAngle(double az, double alt, double lat)
        {
            // Convert to radians
            double az_rad = az * Math.PI / 180.0;
            double alt_rad = alt * Math.PI / 180.0;
            double lat_rad = lat * Math.PI / 180.0;

            // Calculate parallactic angle
            double numerator = Math.Sin(az_rad);
            double denominator = Math.Cos(az_rad) * Math.Sin(lat_rad) + Math.Tan(alt_rad) * Math.Cos(lat_rad);

            double chi_rad = Math.Atan2(numerator, denominator);
            return chi_rad * 180.0 / Math.PI;
        }

        private double CalculateLocalSiderealTime(double longitude, DateTime time)
        {
            // Simplified LST calculation - you may want to use a more precise method
            DateTime j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double daysSinceJ2000 = (time - j2000).TotalDays;

            double gmst = 280.46061837 + 360.98564736629 * daysSinceJ2000;
            double lst = gmst + longitude;

            return NormalizeAngle(lst);
        }

        private double NormalizeAngle(double angle)
        {
            // Normalize angle to 0-360 range
            angle %= 360;
            if (angle < 0)
                angle += 360;
            return angle;
        }

        public bool Validate()
        {
            var i = new List<string>();
            if (rotatorMediator.GetDevice() == null)
            {
                i.Add("Rotator is not configured");
            }
            else

            if (!rotatorMediator.GetDevice().Connected)
            {
                i.Add("Rotator is not connected");
            }

            MechanicalPosition = Math.Round(rotatorMediator.GetInfo().MechanicalPosition, 3);

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged()
        {
            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(FlipRotator)}";
        }
    }
}