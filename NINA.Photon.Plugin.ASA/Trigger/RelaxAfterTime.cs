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

        private DateTime initialTime;
        private bool initialized = false;

        [ImportingConstructor]
        public RelaxAfterTime(INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator) :
            this(ASAPlugin.ASAOptions, ASAPlugin.MountMediator, ASAPlugin.Mount,
                ASAPlugin.MountModelBuilderMediator, ASAPlugin.ModelPointGenerator,
                nighttimeCalculator, cameraMediator, telescopeMediator)
        {
        }

        public RelaxAfterTime(IASAOptions options, IMountMediator mountMediator, IMount mount,
            IMountModelBuilderMediator mountModelBuilderMediator, IModelPointGenerator modelPointGenerator,
            INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator)
        {
            this.options = options;
            this.mount = mount;
            this.mountMediator = mountMediator;
            this.mountModelBuilderMediator = mountModelBuilderMediator;
            this.modelPointGenerator = modelPointGenerator;
            this.nighttimeCalculator = nighttimeCalculator;
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            this.Coordinates = new InputCoordinates();

            Amount = 90;
        }

        private RelaxAfterTime(RelaxAfterTime cloneMe) : this(cloneMe.nighttimeCalculator, cloneMe.cameraMediator, cloneMe.telescopeMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            var cloned = new RelaxAfterTime(this)
            {
            };

            return cloned;
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

        public override void Initialize()
        {
            initialTime = DateTime.Now;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            Coordinates.Coordinates = telescopeMediator.GetCurrentPosition();
            Logger.Debug($"MLPTafterTime: Coordinates not set, using telescope coordinates: {Coordinates.Coordinates}");

            initialTime = DateTime.Now;
            options.LastMLPT = DateTime.Now;
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
            if (nextItem == null) { return false; }
            if (!(nextItem is IExposureItem exposureItem)) { return false; }
            if (exposureItem.ImageType != "LIGHT") { return false; }

            bool shouldTrigger = false;

            if (options.LastMLPT == DateTime.MinValue)
            {
                Logger.Debug("MLPTstopAfterTime: LastMLPT is not set, skipping trigger check.");
                options.LastMLPT = DateTime.Now;
                return false;
            }

            Elapsed = Math.Round((DateTime.Now - options.LastMLPT).TotalMinutes, 2);
            bool timeConditionMet = (DateTime.Now - options.LastMLPT) >= TimeSpan.FromMinutes(Amount);
            Logger.Debug($"MLPTafterTime: Elapsed={Elapsed}min, Required={Amount}min, TimeConditionMet={timeConditionMet}");

            shouldTrigger = timeConditionMet;

            return shouldTrigger;
        }

        private ImmutableList<ModelPoint> ModelPoints = ImmutableList.Create<ModelPoint>();

        public bool Validate()
        {
            var i = new List<string>();

            if (telescopeMediator.GetDevice().Connected)
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

            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(RelaxAfterTime)}, Coordinates: {Coordinates?.Coordinates}, Inherited: {Inherited}";
        }
    }
}