#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Tools;
using Grpc.Core;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.FansOff
{
    [ExportMetadata("Name", "Fans Off")]
    [ExportMetadata("Description", "Stop the ASA fans (via Autoslew, not ACC)")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FansOff : SequenceItem
    {
        [ImportingConstructor]
        public FansOff() : this(ASAPlugin.MountMediator, ASAPlugin.ASAOptions, ASAPlugin.Mount)
        {
        }

        public FansOff(IMountMediator mountMediator, IASAOptions options, IMount mount)
        {
            this.mountMediator = mountMediator;
            this.mount = mount;
        }

        private FansOff(FansOff cloneMe) : this(cloneMe.mountMediator, cloneMe.options, cloneMe.mount)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new FansOff(this)
            {
            };
        }

        private IMountMediator mountMediator;
        private IMount mount;

        public ObservableCollection<int> FanSpeedOptions { get; set; }

        private ASAOptions options;
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

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            //var fanSpeed = Utilities.Utilities.ResolveTokens(FanSpeed, this, metadata);

            if (!mount.FansOff())
            {
                throw new Exception("Failed to power on the ASA mount");
            }
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(FansOff)}";
        }
    }
}