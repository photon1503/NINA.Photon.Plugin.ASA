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

/*
    [ExportMetadata("Name", "Save Model")]
    [ExportMetadata("Description", "Saves the pointing model currently loaded on the mount")]
    [ExportMetadata("Icon", "SaveSVG")]
    [ExportMetadata("Category", "ASA")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SaveModel : SequenceItem, IValidatable
    {
        [ImportingConstructor]
        public SaveModel() : this(ASAPlugin.MountModelMediator)
        {
        }

        public SaveModel(IMountModelMediator mountModelMediator)
        {
            this.mountModelMediator = mountModelMediator;
        }

        private SaveModel(SaveModel cloneMe) : this(cloneMe.mountModelMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new SaveModel(this)
            {
                ModelName = ModelName
            };
        }

        private string modelName;
        private IMountModelMediator mountModelMediator;
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

        public string ModelName
        {
            get => modelName;
            set
            {
                if (modelName != value)
                {
                    modelName = value;
                    RaisePropertyChanged();
                }
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (!mountModelMediator.SaveModel(ModelName))
            {
                throw new Exception($"Failed to save ASA model {ModelName}");
            }
            return Task.CompletedTask;
        }

        public bool Validate()
        {
            var i = new List<string>();
            if (!mountModelMediator.GetInfo().Connected)
            {
                i.Add("ASA mount not connected");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged()
        {
            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(SaveModel)}, ModelName: {ModelName}";
        }
    }
    */
}