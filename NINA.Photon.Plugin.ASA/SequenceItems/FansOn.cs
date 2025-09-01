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

namespace NINA.Photon.Plugin.ASA.FansOn
{
    [ExportMetadata("Name", "Fans On")]
    [ExportMetadata("Description", "Start the ASA fans (via Autoslew, not ACC)")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FansOn : SequenceItem
    {
        [ImportingConstructor]
        public FansOn() : this(ASAPlugin.MountMediator, ASAPlugin.ASAOptions, ASAPlugin.Mount)
        {
        }

        public FansOn(IMountMediator mountMediator, IASAOptions options, IMount mount)
        {
            this.mountMediator = mountMediator;
            this.mount = mount;
            FanSpeedOptions = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            FanSpeed = 9;
        }

        private FansOn(FansOn cloneMe) : this(cloneMe.mountMediator, cloneMe.options, cloneMe.mount)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new FansOn(this)
            {
                FanSpeed = FanSpeed
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

        private int fanSpeed;

        [JsonProperty]
        public int FanSpeed
        {
            get => fanSpeed;
            set
            {
                fanSpeed = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            //var fanSpeed = Utilities.Utilities.ResolveTokens(FanSpeed, this, metadata);

            if (FanSpeed < 1 || FanSpeed > 9)
                return;

            if (!mount.FansOn(fanSpeed))
            {
                throw new Exception("Failed to power on the ASA mount");
            }
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(FansOn)}";
        }
    }
}