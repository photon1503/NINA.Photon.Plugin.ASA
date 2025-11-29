#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyGuider;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.SequenceItems;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using NINA.ViewModel.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.MLTP
{
    [ExportMetadata("Name", "Mount Dither After")]
    [ExportMetadata("Description", "Mount Dither After x Exposures")]
    [ExportMetadata("Icon", "DitherSVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class MountDitherAfter : SequenceTrigger, IValidatable
    {
        private IGuiderMediator guiderMediator;
        private IImageHistoryVM history;
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public MountDitherAfter(IImageHistoryVM history, IProfileService profileService, ITelescopeMediator telescopeMediator, IGuiderMediator guiderMediator) : base()
        {
            this.history = history;
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.guiderMediator = guiderMediator;
            AfterExposures = 1;
        }

        private MountDitherAfter(MountDitherAfter cloneMe) : this(cloneMe.history, cloneMe.profileService, cloneMe.telescopeMediator, cloneMe.guiderMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new MountDitherAfter(this)
            {
                AfterExposures = AfterExposures,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
        }

        private int lastTriggerId = 0;
        private int afterExposures;

        [JsonProperty]
        public int AfterExposures
        {
            get => afterExposures;
            set
            {
                afterExposures = value;
                RaisePropertyChanged();
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues
        {
            get => issues;
            set
            {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        public int ProgressExposures => AfterExposures > 0 ? history.ImageHistory.Count % AfterExposures : 0;

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (AfterExposures > 0)
            {
                lastTriggerId = history.ImageHistory.Count;

                var directGuider = new DirectGuider(profileService, telescopeMediator);
                double ditherPixels = profileService.ActiveProfile.GuiderSettings.DitherPixels;
                double ditherSettleTime = profileService.ActiveProfile.GuiderSettings.SettleTime;
                bool ditherRAOnly = profileService.ActiveProfile.GuiderSettings.DitherRAOnly;

                TimeSpan timeSpan = TimeSpan.FromSeconds(0);

                if (double.IsNaN(ditherSettleTime) || ditherSettleTime < 0)
                {
                    timeSpan = TimeSpan.FromSeconds(0);
                }
                else
                {
                    try
                    {
                        timeSpan = TimeSpan.FromSeconds(ditherSettleTime);
                    }
                    catch
                    {
                        timeSpan = TimeSpan.FromSeconds(0);
                    }
                }

                await directGuider.Dither(ditherPixels, timeSpan, ditherRAOnly, progress, token);

                while (telescopeMediator.GetInfo().IsPulseGuiding || telescopeMediator.GetInfo().Slewing)
                {
                    await CoreUtil.Delay(TimeSpan.FromMilliseconds(100), token);
                }
            }
            else
            {
                return;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            if (nextItem == null) { return false; }
            if (!(nextItem is IExposureItem exposureItem)) { return false; }
            if (exposureItem.ImageType != "LIGHT") { return false; }

            RaisePropertyChanged(nameof(ProgressExposures));
            if (lastTriggerId > history.ImageHistory.Count)
            {
                // The image history was most likely cleared
                lastTriggerId = 0;
            }
            var shouldTrigger = lastTriggerId < history.ImageHistory.Count && history.ImageHistory.Count > 0 && ProgressExposures == 0;

            return shouldTrigger;
        }

        public override string ToString()
        {
            return $"Trigger: {nameof(MountDitherAfter)}, After Exposures: {AfterExposures}";
        }

        public bool Validate()
        {
            var i = new List<string>();
            var info = telescopeMediator.GetInfo();

            if (AfterExposures > 0 && !info.Connected)
            {
                i.Add(Loc.Instance["LblGuiderNotConnected"]);
            }

            Issues = i;
            return i.Count == 0;
        }
    }
}