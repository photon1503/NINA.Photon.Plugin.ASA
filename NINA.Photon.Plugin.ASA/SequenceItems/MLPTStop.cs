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
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems
{
    [ExportMetadata("Name", "MLPT Stop")]
    [ExportMetadata("Description", "Stop MLPT")]
    [ExportMetadata("Icon", "ASAMLPTSVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class MLTPStop : SequenceItem, IValidatable
    {
        [ImportingConstructor]
        public MLTPStop() : this(ASAPlugin.MountMediator, ASAPlugin.ASAOptions, ASAPlugin.Mount)
        {
        }

        public MLTPStop(IMountMediator mountMediator, IASAOptions options, IMount mount)
        {
            this.mountMediator = mountMediator;
            this.mount = mount;
        }

        private MLTPStop(MLTPStop cloneMe) : this(cloneMe.mountMediator, cloneMe.options, cloneMe.mount)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new MLTPStop(this) { };
        }

        private IMountMediator mountMediator;
        private IMount mount;
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

        public bool Validate()
        {
            var i = new List<string>();
            if (mountMediator?.GetInfo()?.Connected == true)
            {
                try
                {
                    var version = mount.AutoslewVersion();

                    // check if version is older then 7.1.4.4
                    if (VersionHelper.IsOlderVersion(version, "7.1.4.4"))
                    {
                        i.Add("Autoslew Version not supported");
                    }
                }
                catch (Exception ex)
                {
                    i.Add($"Autoslew not connected");
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (!mount.MLTPStop())
            {
                throw new Exception("Failed to stop MLPT");
            }
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(PowerOn)}";
        }
    }
}