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

        public override void Initialize()
        {
            if (!initialized)
            {
                try
                {
                    MechanicalPosition = rotatorMediator.GetInfo().MechanicalPosition;
                }
                catch
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

            if (rotatorMediator.GetDevice() == null || !rotatorMediator.GetDevice().Connected)
            {
                return false;
            }

            double currentPosition = rotatorMediator.GetInfo().MechanicalPosition;
            double limit1 = FlipAt - TrackingLimit;
            double limit2 = FlipAt + TrackingLimit;

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

            MechanicalPosition = rotatorMediator.GetInfo().Position;

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