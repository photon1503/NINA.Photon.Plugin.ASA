#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace NINA.Photon.Plugin.ASA.Equipment
{
    public class ModelAccessor : IModelAccessor
    {
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IMountModelMediator mountModelMediator;
        private readonly ICustomDateTime dateTime;

        public ModelAccessor(ITelescopeMediator telescopeMediator, IMountModelMediator mountModelMediator, ICustomDateTime dateTime)
        {
            this.telescopeMediator = telescopeMediator;
            this.mountModelMediator = mountModelMediator;
            this.dateTime = dateTime;
        }

        public LoadedAlignmentModel LoadActiveModel(string modelName = null, IProgress<ApplicationStatus> progress = null, CancellationToken ct = default)
        {
            var alignmentModel = new LoadedAlignmentModel();
            LoadActiveModelInto(alignmentModel, modelName, progress, ct);
            return alignmentModel;
        }

        public void LoadActiveModelInto(LoadedAlignmentModel alignmentModel, string modelName = null, IProgress<ApplicationStatus> progress = null, CancellationToken ct = default)
        {
        }
    }
}