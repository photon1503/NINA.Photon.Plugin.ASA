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
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems {

    [ExportMetadata("Name", "Shutdown Mount")]
    [ExportMetadata("Description", "Powers off the ASA mount by safely shutting it down")]
    [ExportMetadata("Icon", "PowerSVG")]
    [ExportMetadata("Category", "ASA")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Shutdown : SequenceItem, IValidatable {

        [ImportingConstructor]
        public Shutdown() : this(ASAPlugin.MountMediator) {
        }

        public Shutdown(IMountMediator mountMediator) {
            this.mountMediator = mountMediator;
        }

        private Shutdown(Shutdown cloneMe) : this(cloneMe.mountMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new Shutdown(this) { };
        }

        private IMountMediator mountMediator;
        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (mountMediator.GetInfo().Connected) {
                if (!mountMediator.Shutdown()) {
                    throw new Exception("Failed to shutdown the ASA mount");
                }
            }
            return Task.CompletedTask;
        }

        public bool Validate() {
            var i = new List<string>();
            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Shutdown)}";
        }
    }
}