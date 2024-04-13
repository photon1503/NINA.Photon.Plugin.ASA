#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems {

/*
    [ExportMetadata("Name", "Set Tracking Rate")]
    [ExportMetadata("Description", "Sets a standard tracking rate")]
    [ExportMetadata("Icon", "SpeedometerSVG")]
    [ExportMetadata("Category", "ASA")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SetTrackingRate : SequenceItem, IValidatable {
        private static readonly IList<TrackingMode> trackingModeChoices;

        static SetTrackingRate() {
            var trackingModeChoicesBuilder = ImmutableList.CreateBuilder<TrackingMode>();
            trackingModeChoicesBuilder.Add(TrackingMode.Sidereal);
            trackingModeChoicesBuilder.Add(TrackingMode.Solar);
            trackingModeChoicesBuilder.Add(TrackingMode.Lunar);
            trackingModeChoicesBuilder.Add(TrackingMode.Stopped);
            trackingModeChoices = trackingModeChoicesBuilder.ToImmutable();
        }

        [ImportingConstructor]
        public SetTrackingRate(ITelescopeMediator telescopeMediator) : this(ASAPlugin.MountMediator, telescopeMediator) {
        }

        public SetTrackingRate(IMountMediator mountMediator, ITelescopeMediator telescopeMediator) {
            this.mountMediator = mountMediator;
            this.telescopeMediator = telescopeMediator;
        }

        private SetTrackingRate(SetTrackingRate cloneMe) : this(cloneMe.mountMediator, cloneMe.telescopeMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new SetTrackingRate(this) {
                TrackingMode = TrackingMode
            };
        }

        private readonly ITelescopeMediator telescopeMediator;
        private readonly IMountMediator mountMediator;
        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private TrackingMode trackingMode = TrackingMode.Sidereal;

        [JsonProperty]
        public TrackingMode TrackingMode {
            get => trackingMode;
            set {
                trackingMode = value;
                RaisePropertyChanged();
            }
        }

        public IList<TrackingMode> TrackingModeChoices {
            get {
                return trackingModeChoices;
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (telescopeMediator.GetInfo().AtPark) {
                throw new Exception("Mount is parked");
            }

            if (!mountMediator.SetTrackingRate(TrackingMode)) {
                throw new Exception("Setting tracking rate failed");
            }
            return Task.CompletedTask;
        }

        public bool Validate() {
            var i = new List<string>();
            if (!mountMediator.GetInfo().Connected) {
                i.Add("ASA mount not connected");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SetTrackingRate)}, TrackingMode: {TrackingMode}";
        }
        */
}