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
using NINA.Photon.Plugin.ASA.Model;
using System;
using System.Collections.Generic;

namespace NINA.Photon.Plugin.ASA.Interfaces {

    public interface IModelPointGenerator {

        List<ModelPoint> GenerateGoldenSpiral(int numPoints, CustomHorizon horizon);

        List<ModelPoint> GenerateAutoGrid(double raSpacingDegrees, double decSpacingDegrees, CustomHorizon horizon);

        List<ModelPoint> GenerateSiderealPath(Coordinates coordinates, Angle raDelta, DateTime startTime, DateTime endTime, CustomHorizon horizon);
    }
}