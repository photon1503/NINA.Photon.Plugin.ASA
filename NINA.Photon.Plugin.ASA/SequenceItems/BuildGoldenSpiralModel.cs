#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems {

    [ExportMetadata("Name", "Build Golden Spiral Model")]
    [ExportMetadata("Description", "Builds a full sky model using a Golden Spiral")]
    [ExportMetadata("Icon", "BuildSVG")]
    [ExportMetadata("Category", "ASA")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class BuildGoldenSpiralModel : SequenceItem, IValidatable {

        [ImportingConstructor]
        public BuildGoldenSpiralModel(INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator) : this(ASAPlugin.ASAOptions, ASAPlugin.MountMediator, ASAPlugin.MountModelBuilderMediator, ASAPlugin.ModelPointGenerator, nighttimeCalculator, cameraMediator) {
        }

        public BuildGoldenSpiralModel(IASAOptions options, IMountMediator mountMediator, IMountModelBuilderMediator mountModelBuilderMediator, IModelPointGenerator modelPointGenerator, INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator) {
            this.options = options;
            this.mountMediator = mountMediator;
            this.mountModelBuilderMediator = mountModelBuilderMediator;
            this.modelPointGenerator = modelPointGenerator;
            this.nighttimeCalculator = nighttimeCalculator;
            this.cameraMediator = cameraMediator;
            this.goldenSpiralPointCount = options.GoldenSpiralStarCount;
        }

        private BuildGoldenSpiralModel(BuildGoldenSpiralModel cloneMe) : this(cloneMe.options, cloneMe.mountMediator, cloneMe.mountModelBuilderMediator, cloneMe.modelPointGenerator, cloneMe.nighttimeCalculator, cloneMe.cameraMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            var cloned = new BuildGoldenSpiralModel(this) {
                MaxFailedPoints = MaxFailedPoints,
                BuilderNumRetries = BuilderNumRetries,
                MaxPointRMS = MaxPointRMS,
                GoldenSpiralPointCount = GoldenSpiralPointCount
            };
            return cloned;
        }

        private readonly IASAOptions options;
        private readonly IMountMediator mountMediator;
        private readonly IMountModelBuilderMediator mountModelBuilderMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly ICameraMediator cameraMediator;
        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private double maxPointRMS;

        [JsonProperty]
        public double MaxPointRMS {
            get => maxPointRMS;
            set {
                if (maxPointRMS != value) {
                    maxPointRMS = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int builderNumRetries;

        [JsonProperty]
        public int BuilderNumRetries {
            get => builderNumRetries;
            set {
                if (builderNumRetries != value) {
                    builderNumRetries = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int maxFailedPoints;

        [JsonProperty]
        public int MaxFailedPoints {
            get => maxFailedPoints;
            set {
                if (maxFailedPoints != value) {
                    maxFailedPoints = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int goldenSpiralPointCount;

        [JsonProperty]
        public int GoldenSpiralPointCount {
            get => goldenSpiralPointCount;
            set {
                if (goldenSpiralPointCount != value) {
                    goldenSpiralPointCount = value;
                    RaisePropertyChanged();
                    UpdateModelPoints();
                }
            }
        }

        private int modelPointCount;

        public int ModelPointCount {
            get => modelPointCount;
            set {
                if (modelPointCount != value) {
                    modelPointCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var modelBuilderOptions = new ModelBuilderOptions() {
                WestToEastSorting = options.WestToEastSorting,
                NumRetries = BuilderNumRetries,
                MaxPointRMS = MaxPointRMS > 0 ? MaxPointRMS : double.PositiveInfinity,
                MinimizeDomeMovement = options.MinimizeDomeMovementEnabled,
                AllowBlindSolves = options.AllowBlindSolves,
                MaxConcurrency = options.MaxConcurrency,
                DomeShutterWidth_mm = options.DomeShutterWidth_mm,
                MaxFailedPoints = MaxFailedPoints,
                RemoveHighRMSPointsAfterBuild = options.RemoveHighRMSPointsAfterBuild,
                PlateSolveSubframePercentage = options.PlateSolveSubframePercentage,
                DisableRefractionCorrection = options.DisableRefractionCorrection
            };

            if (!await mountModelBuilderMediator.BuildModel(ModelPoints, modelBuilderOptions, token)) {
                throw new Exception("ASA model build failed");
            }
        }

        private ImmutableList<ModelPoint> ModelPoints = ImmutableList.Create<ModelPoint>();

        private void UpdateModelPoints() {
            try {
                ModelPoints = mountModelBuilderMediator.GenerateGoldenSpiral(this.GoldenSpiralPointCount);
                ModelPointCount = ModelPoints.Count(p => p.ModelPointState == ModelPointStateEnum.Generated);
            } catch (Exception e) {
                Notification.ShowError($"Failed to generate golden spiral points: {e.Message}");
            }
        }

        public bool Validate() {
            var i = new List<string>();
            if (!mountMediator.GetInfo().Connected) {
                i.Add("ASA mount not connected");
            }
            if (!cameraMediator.GetInfo().Connected) {
                i.Add("Camera not connected");
            }
            if (ModelPoints.Count < 3) {
                i.Add($"Model builds require at least 3 points. Only {ModelPoints.Count} points were generated");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            if (this.Parent != null) {
                UpdateModelPoints();
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(BuildGoldenSpiralModel)}, GoldenSpiralPointCount: {GoldenSpiralPointCount}, NumRetries: {BuilderNumRetries}, MaxFailedPoints: {MaxFailedPoints}, MaxPointRMS: {MaxPointRMS}";
        }
    }
}