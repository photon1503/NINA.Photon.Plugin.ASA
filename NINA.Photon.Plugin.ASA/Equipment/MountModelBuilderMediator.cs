#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Sequencer.Utility.DateTimeProvider;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.Equipment {

    public class MountModelBuilderMediator : Mediator<IMountModelBuilderVM>, IMountModelBuilderMediator {
        public CustomHorizon CustomHorizon => handler.CustomHorizon;

        public Task<bool> BuildModel(IList<ModelPoint> modelPoints, ModelBuilderOptions options, CancellationToken ct) {
            return handler.BuildModel(modelPoints, options, ct);
        }

        public ImmutableList<ModelPoint> GenerateSiderealPath(InputCoordinates coordinates, Angle raDelta, IDateTimeProvider startTimeProvider, IDateTimeProvider endTimeProvider, int startOffsetMinutes, int endOffsetMinutes) {
            return handler.GenerateSiderealPath(coordinates, raDelta, startTimeProvider, endTimeProvider, startOffsetMinutes, endOffsetMinutes);
        }

        public ImmutableList<ModelPoint> GenerateGoldenSpiral(int goldenSpiralStarCount) {
            return handler.GenerateGoldenSpiral(goldenSpiralStarCount);
        }

        public ImmutableList<ModelPoint> GenerateAutoGrid(double raSpacingDegrees, double decSpacingDegrees) {
            return handler.GenerateAutoGrid(raSpacingDegrees, decSpacingDegrees);
        }
    }
}