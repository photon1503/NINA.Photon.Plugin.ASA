#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Model;
using NINA.Photon.Plugin.ASA.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.Interfaces {

    public class ModelBuilderOptions {
        public int NumRetries { get; set; } = 0;
        public double MaxPointRMS { get; set; } = double.PositiveInfinity;
        public bool WestToEastSorting { get; set; } = false;
        public bool MinimizeDomeMovement { get; set; } = true;
        public bool MinimizeMeridianFlips { get; set; } = true;
        public bool AllowBlindSolves { get; set; } = false;
        public int MaxConcurrency { get; set; } = 3;
        public int DomeShutterWidth_mm { get; set; } = 0;
        public int MaxFailedPoints { get; set; } = 0;
        public bool RemoveHighRMSPointsAfterBuild { get; set; } = true;
        public double PlateSolveSubframePercentage { get; set; } = 1.0d;
        public bool AlternateDirectionsBetweenIterations { get; set; } = true;
        public bool DisableRefractionCorrection { get; set; } = false;
    }

    public class PointNextUpEventArgs : EventArgs {
        public ModelPoint Point { get; set; } = null;
    }

    public interface IModelBuilder {

        Task<LoadedAlignmentModel> Build(IList<ModelPoint> modelPoints, ModelBuilderOptions options, CancellationToken ct = default, CancellationToken stopToken = default, IProgress<ApplicationStatus> overallProgress = null, IProgress<ApplicationStatus> stepProgress = null);

        event EventHandler<PointNextUpEventArgs> PointNextUp;
    }
}