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
    [ExportMetadata("Name", "Relax Slew After Time")]
    [ExportMetadata("Description", "Start Relax Slew after x Minutes")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class RelaxAfterTime : SequenceTrigger, IValidatable
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
        protected IPlateSolverFactory plateSolverFactory;
        protected IWindowServiceFactory windowServiceFactory;

        protected IImagingMediator imagingMediator;

        private GeometryGroup PlatesolveIcon = (GeometryGroup)Application.Current.Resources["PlatesolveSVG"];

        private DateTime initialTime;
        private bool initialized = false;

        [ImportingConstructor]
        public RelaxAfterTime(IProfileService profileService,
            ICameraMediator cameraMediator,
            ITelescopeMediator telescopeMediator,
            IApplicationStatusMediator applicationStatusMediator,
            IGuiderMediator guiderMediator,
            IFilterWheelMediator filterWheelMediator,
            IDomeMediator domeMediator,
            IDomeFollower domeFollower,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory,
            IImagingMediator imagingMediator
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
            this.Coordinates = new InputCoordinates();

            NINA.Sequencer.SequenceItem.Platesolving.Center c = new(profileService, telescopeMediator, imagingMediator, filterWheelMediator, guiderMediator,
               domeMediator, domeFollower, plateSolverFactory, windowServiceFactory)
            { Name = "Slew and center", Icon = PlatesolveIcon };
            TriggerRunner = new SequentialContainer();
            AddItem(TriggerRunner, c);

            Amount = 90;
        }

        private void AddItem(SequentialContainer runner, ISequenceItem item)
        {
            runner.Items.Add(item);
            item.AttachNewParent(runner);
        }

        private RelaxAfterTime(RelaxAfterTime cloneMe) : this(cloneMe.profileService, cloneMe.cameraMediator, cloneMe.telescopeMediator, cloneMe.applicationStatusMediator, cloneMe.guiderMediator, cloneMe.filterWheelMediator, cloneMe.domeMediator, cloneMe.domeFollower, cloneMe.plateSolverFactory, cloneMe.windowServiceFactory, cloneMe.imagingMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new RelaxAfterTime(this)
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

        public SequentialContainer TriggerRunner { get; protected set; }

        public override void Initialize()
        {
            initialTime = DateTime.Now;
        }

        public Coordinates GetRelaxPoint(Coordinates current, double relaxDegrees = 5.0)
        {
            // Get current topocentric coordinates to determine altitude
            var topo = current.Transform(
                latitude: Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude),
                longitude: Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude),
                elevation: profileService.ActiveProfile.AstrometrySettings.Elevation
            );
            double currentAlt = topo.Altitude.Degree;

            // Clamp Dec to avoid zenith/gimbal lock
            double safeDec = Math.Max(Math.Min(current.Dec, 85), -85);

            double siteLat = profileService.ActiveProfile.AstrometrySettings.Latitude;

            // Decide direction: towards zenith if below 45°, away if above 45°
            double newDec;
            if (currentAlt < 45)
            {
                // Move Dec towards zenith
                newDec = safeDec + Math.Sign(siteLat) * relaxDegrees;
            }
            else
            {
                // Move Dec away from zenith
                newDec = safeDec - Math.Sign(siteLat) * relaxDegrees;
            }
            newDec = Math.Max(Math.Min(newDec, 85), -85);
            newDec = Math.Max(Math.Min(newDec, 85), -85);

            // Get current LST (local sidereal time) and hour angle
            double lst = telescopeMediator.GetInfo().SiderealTime; // in hours
            double ha = lst - current.RA; // in hours
            if (ha < -12) ha += 24;
            if (ha > 12) ha -= 24;

            // Use user-configurable relax amount if available
            double relaxRAHours = RArelaxDegrees / 15.0;

            // Move HA further from 0, but clamp to not cross ±12h
            double newHA;
            if (ha >= 0)
                newHA = Math.Min(ha + relaxRAHours, 12.0 - 1e-6); // move west, but not past +12h
            else
                newHA = Math.Max(ha - relaxRAHours, -12.0 + 1e-6); // move east, but not past -12h

            // Convert new HA back to RA
            double newRA = lst - newHA;
            if (newRA < 0) newRA += 24;
            if (newRA >= 24) newRA -= 24;

            // Create new coordinates
            var relaxCoords = new Coordinates(Angle.ByHours(newRA), Angle.ByDegree(newDec), Epoch.JNOW);

            // Check horizon (altitude) using your horizon model or a fixed minimum
            topo = relaxCoords.Transform(
               latitude: Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude),
               longitude: Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude),
               elevation: profileService.ActiveProfile.AstrometrySettings.Elevation
           );
            if (topo.Altitude.Degree < 5) // 5° above horizon
            {
                Logger.Warning("Relax slew would go below safe horizon. Skipping.");
                return current; // Return current coordinates if below horizon
            }

            return relaxCoords;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            initialTime = DateTime.Now;

            Coordinates.Coordinates = telescopeMediator.GetCurrentPosition();

            Coordinates relax = GetRelaxPoint(Coordinates.Coordinates, 5.0); // Relax by 5 degrees

            Logger.Debug($"Relax Slew start at {Coordinates.Coordinates}");

            // make a releax slew to +5 degrees in RA and +5 degrees in DEC
            await telescopeMediator.SlewToCoordinatesAsync(relax, token);

            // slew back to original coordinates
            Logger.Debug($"Relax Slew back to {relax}");
            await telescopeMediator.SlewToCoordinatesAsync(Coordinates.Coordinates, token);

            if (Recenter)
            {
                try
                {
                    await TriggerRunner.Run(progress, token);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error during RelaxAfterTime execution: {ex.Message}");
                }
            }
        }

        [JsonProperty]
        public InputCoordinates Coordinates { get; set; }

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

        private double elapsed;

        public double Elapsed
        {
            get => elapsed;
            private set
            {
                elapsed = value;
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

        private double derelaxDegrees = 5;

        [JsonProperty]
        public double DErelaxDegrees
        {
            get => derelaxDegrees;
            set
            {
                derelaxDegrees = value;
                RaisePropertyChanged();
            }
        }

        private double rarelaxDegrees = 5;

        [JsonProperty]
        public double RArelaxDegrees
        {
            get => rarelaxDegrees;
            set
            {
                rarelaxDegrees = value;
                RaisePropertyChanged();
            }
        }

        private bool recenter = true;

        [JsonProperty]
        public bool Recenter
        {
            get => recenter;
            set
            {
                recenter = value;
                RaisePropertyChanged();
            }
        }

        public override void SequenceBlockInitialize()
        {
            initialTime = DateTime.Now;

            if (!initialized)
            {
                initialized = true;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            TimeSpan ts = DateTime.Now - initialTime;
            Elapsed = Math.Round(ts.TotalMinutes, 2);

            return Elapsed >= Amount;
        }

        private ImmutableList<ModelPoint> ModelPoints = ImmutableList.Create<ModelPoint>();

        public bool Validate()
        {
            var i = new List<string>();

            if (!telescopeMediator.GetInfo().Connected)
            {
                i.Add("Mount not connected");
            }

            if (!cameraMediator.GetInfo().Connected)
            {
                i.Add("Camera not connected");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged()
        {
            var contextCoordinates = ItemUtility.RetrieveContextCoordinates(this.Parent);
            if (contextCoordinates != null)
            {
                Coordinates.Coordinates = contextCoordinates.Coordinates;

                Inherited = true;
            }
            else
            {
                Inherited = false;
            }

            foreach (ISequenceItem item in TriggerRunner.Items)
            {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);

            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(RelaxAfterTime)}, Coordinates: {Coordinates?.Coordinates}, Inherited: {Inherited}";
        }
    }
}