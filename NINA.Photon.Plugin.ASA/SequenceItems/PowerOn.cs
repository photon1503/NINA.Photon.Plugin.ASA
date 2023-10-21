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

    [ExportMetadata("Name", "Power On Mount")]
    [ExportMetadata("Description", "Powers on the ASA mount and connects")]
    [ExportMetadata("Icon", "PowerSVG")]
    [ExportMetadata("Category", "ASA")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class PowerOn : SequenceItem, IValidatable {

        [ImportingConstructor]
        public PowerOn() : this(ASAPlugin.MountMediator, ASAPlugin.ASAOptions) {
        }

        public PowerOn(IMountMediator mountMediator, IASAOptions options) {
            this.mountMediator = mountMediator;
            this.options = options;
        }

        private PowerOn(PowerOn cloneMe) : this(cloneMe.mountMediator, cloneMe.options) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new PowerOn(this) { };
        }

        private IMountMediator mountMediator;
        private IASAOptions options;
        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (!await mountMediator.PowerOn(token)) {
                throw new Exception("Failed to power on the ASA mount");
            }
        }

        public bool Validate() {
            var i = new List<string>();
            if (string.IsNullOrEmpty(options.DriverID)) {
                i.Add("Connect at least once to the ASA mount to initialize it");
            } else if (string.IsNullOrEmpty(options.IPAddress) || string.IsNullOrEmpty(options.MACAddress)) {
                i.Add("No IP address is set. If you connect via IP, connect at least once to initialize settings");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(PowerOn)}";
        }
    }
}