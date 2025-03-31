﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Astrometry.Interfaces;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.Trigger;
using NINA.Profile;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces;

namespace NINA.Photon.Plugin.ASA.MLTP
{
    [ExportMetadata("Name", "MLPT After Time")]
    [ExportMetadata("Description", "Start MLPT after x Minutes")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class MLPTafterTime : SequenceTrigger, IValidatable
    {
        private DateTime initialTime;
        private bool initialized = false;

        [ImportingConstructor]
        public MLPTafterTime(INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator) :
            this(ASAPlugin.ASAOptions, ASAPlugin.MountMediator, ASAPlugin.Mount,
                ASAPlugin.MountModelBuilderMediator, ASAPlugin.ModelPointGenerator,
                nighttimeCalculator, cameraMediator)
        {
        }

        public MLPTafterTime(IASAOptions options, IMountMediator mountMediator, IMount mount,
            IMountModelBuilderMediator mountModelBuilderMediator, IModelPointGenerator modelPointGenerator,
            INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator)
        {
            this.options = options;
            this.mount = mount;
            this.mountMediator = mountMediator;
            this.mountModelBuilderMediator = mountModelBuilderMediator;
            this.modelPointGenerator = modelPointGenerator;
            this.nighttimeCalculator = nighttimeCalculator;
            this.cameraMediator = cameraMediator;
            this.Coordinates = new InputCoordinates();

            var nowProvider = new NowDateTimeProvider(new SystemDateTime());
            this.SiderealPathStartDateTimeProviders = ImmutableList.Create<IDateTimeProvider>(
                new NauticalDuskProvider(nighttimeCalculator),
                nowProvider,
                new SunsetProvider(nighttimeCalculator),
                new DuskProvider(nighttimeCalculator));
            this.SelectedSiderealPathStartDateTimeProviderName = this.SiderealPathStartDateTimeProviders.First().Name;
            this.SiderealPathEndDateTimeProviders = ImmutableList.Create<IDateTimeProvider>(
                new NauticalDawnProvider(nighttimeCalculator),
                nowProvider,
                new SunriseProvider(nighttimeCalculator),
                new DawnProvider(nighttimeCalculator));
            this.SelectedSiderealPathEndDateTimeProviderName = this.SiderealPathEndDateTimeProviders.First().Name;
            SiderealTrackRADeltaDegrees = 5;
            Amount = 90;
            TriggerRunner.Add(new MLPTStart(options, mountMediator, mount, mountModelBuilderMediator, modelPointGenerator, nighttimeCalculator, cameraMediator));
        }

        private MLPTafterTime(MLPTafterTime cloneMe) : this(cloneMe.options, cloneMe.mountMediator, cloneMe.mount, cloneMe.mountModelBuilderMediator, cloneMe.modelPointGenerator, cloneMe.nighttimeCalculator, cloneMe.cameraMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            var cloned = new MLPTafterTime(this)
            {
                Coordinates = Coordinates?.Clone(),
                Inherited = Inherited,
                SiderealTrackStartOffsetMinutes = SiderealTrackStartOffsetMinutes,
                SiderealTrackEndOffsetMinutes = SiderealTrackEndOffsetMinutes,
                SiderealTrackRADeltaDegrees = SiderealTrackRADeltaDegrees,
                MaxFailedPoints = MaxFailedPoints,
                BuilderNumRetries = BuilderNumRetries,
                MaxPointRMS = MaxPointRMS,
                SelectedSiderealPathStartDateTimeProviderName = SelectedSiderealPathStartDateTimeProviderName,
                SelectedSiderealPathEndDateTimeProviderName = SelectedSiderealPathEndDateTimeProviderName,
                Amount = Amount,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
            return cloned;
        }

        private bool inherited;

        [JsonProperty]
        public bool Inherited
        {
            get => inherited;
            set
            {
                inherited = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public InputCoordinates Coordinates { get; set; }

        private IASAOptions options;
        private readonly IMountMediator mountMediator;
        private IMount mount;
        private readonly IMountModelBuilderMediator mountModelBuilderMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly ICameraMediator cameraMediator;
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

        private IList<IDateTimeProvider> siderealPathStartDateTimeProviders;

        public IList<IDateTimeProvider> SiderealPathStartDateTimeProviders
        {
            get => siderealPathStartDateTimeProviders;
            private set
            {
                siderealPathStartDateTimeProviders = value;
                RaisePropertyChanged();
            }
        }

        private IDateTimeProvider selectedSiderealPathStartDateTimeProvider;

        public IDateTimeProvider SelectedSiderealPathStartDateTimeProvider
        {
            get => selectedSiderealPathStartDateTimeProvider;
            set
            {
                if (!object.ReferenceEquals(selectedSiderealPathStartDateTimeProvider, value) && value != null)
                {
                    selectedSiderealPathStartDateTimeProvider = value;
                    RaisePropertyChanged();
                    SelectedSiderealPathStartDateTimeProviderName = value.Name;
                    UpdateStartTime();
                }
            }
        }

        private string selectedSiderealPathStartDateTimeProviderName;

        [JsonProperty]
        public string SelectedSiderealPathStartDateTimeProviderName
        {
            get => selectedSiderealPathStartDateTimeProviderName;
            set
            {
                selectedSiderealPathStartDateTimeProviderName = value;
                SelectedSiderealPathStartDateTimeProvider = SiderealPathStartDateTimeProviders.FirstOrDefault(p => p.Name == selectedSiderealPathStartDateTimeProviderName);
                RaisePropertyChanged();
            }
        }

        private string selectedSiderealPathEndDateTimeProviderName;

        [JsonProperty]
        public string SelectedSiderealPathEndDateTimeProviderName
        {
            get => selectedSiderealPathEndDateTimeProviderName;
            set
            {
                selectedSiderealPathEndDateTimeProviderName = value;
                SelectedSiderealPathEndDateTimeProvider = SiderealPathEndDateTimeProviders.FirstOrDefault(p => p.Name == selectedSiderealPathEndDateTimeProviderName);
                RaisePropertyChanged();
            }
        }

        private IList<IDateTimeProvider> siderealPathEndDateTimeProviders;

        public IList<IDateTimeProvider> SiderealPathEndDateTimeProviders
        {
            get => siderealPathEndDateTimeProviders;
            private set
            {
                siderealPathEndDateTimeProviders = value;
                RaisePropertyChanged();
            }
        }

        private IDateTimeProvider selectedSiderealPathEndDateTimeProvider;

        public IDateTimeProvider SelectedSiderealPathEndDateTimeProvider
        {
            get => selectedSiderealPathEndDateTimeProvider;
            set
            {
                if (!object.ReferenceEquals(selectedSiderealPathEndDateTimeProvider, value) && value != null)
                {
                    selectedSiderealPathEndDateTimeProvider = value;
                    RaisePropertyChanged();
                    SelectedSiderealPathEndDateTimeProviderName = value.Name;
                    UpdateEndTime();
                }
            }
        }

        private int siderealTrackStartOffsetMinutes;

        [JsonProperty]
        public int SiderealTrackStartOffsetMinutes
        {
            get => siderealTrackStartOffsetMinutes;
            set
            {
                if (siderealTrackStartOffsetMinutes != value)
                {
                    siderealTrackStartOffsetMinutes = value;
                    RaisePropertyChanged();
                    UpdateStartTime();
                }
            }
        }

        private int siderealTrackEndOffsetMinutes;

        [JsonProperty]
        public int SiderealTrackEndOffsetMinutes
        {
            get => siderealTrackEndOffsetMinutes;
            set
            {
                if (siderealTrackEndOffsetMinutes != value)
                {
                    siderealTrackEndOffsetMinutes = value;
                    RaisePropertyChanged();
                    UpdateEndTime();
                }
            }
        }

        private double siderealTrackRADeltaDegrees;

        [JsonProperty]
        public double SiderealTrackRADeltaDegrees
        {
            get => siderealTrackRADeltaDegrees;
            set
            {
                if (siderealTrackRADeltaDegrees != value)
                {
                    siderealTrackRADeltaDegrees = value;
                    RaisePropertyChanged();
                    UpdateModelPoints();
                }
            }
        }

        private double maxPointRMS;

        [JsonProperty]
        public double MaxPointRMS
        {
            get => maxPointRMS;
            set
            {
                if (maxPointRMS != value)
                {
                    maxPointRMS = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int builderNumRetries;

        [JsonProperty]
        public int BuilderNumRetries
        {
            get => builderNumRetries;
            set
            {
                if (builderNumRetries != value)
                {
                    builderNumRetries = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int maxFailedPoints;

        [JsonProperty]
        public int MaxFailedPoints
        {
            get => maxFailedPoints;
            set
            {
                if (maxFailedPoints != value)
                {
                    maxFailedPoints = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double elapsed;

        public double Elapsed
        {
            get => elapsed;
            private set
            {
                elapsed = value;
                RaisePropertyChanged();
            }
        }

        private double amount;

        [JsonProperty]
        public double Amount
        {
            get => amount;
            set
            {
                amount = value;
                RaisePropertyChanged();
            }
        }

        private int modelPointCount;

        public int ModelPointCount
        {
            get => modelPointCount;
            set
            {
                if (modelPointCount != value)
                {
                    modelPointCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int startHours;

        public int StartHours
        {
            get => startHours;
            set
            {
                startHours = value;
                RaisePropertyChanged();
            }
        }

        private int startMinutes;

        public int StartMinutes
        {
            get => startMinutes;
            set
            {
                startMinutes = value;
                RaisePropertyChanged();
            }
        }

        private int startSeconds;

        public int StartSeconds
        {
            get => startSeconds;
            set
            {
                startSeconds = value;
                RaisePropertyChanged();
            }
        }

        private int endHours;

        public int EndHours
        {
            get => endHours;
            set
            {
                endHours = value;
                RaisePropertyChanged();
            }
        }

        private int endMinutes;

        public int EndMinutes
        {
            get => endMinutes;
            set
            {
                endMinutes = value;
                RaisePropertyChanged();
            }
        }

        private int endSeconds;

        public int EndSeconds
        {
            get => endSeconds;
            set
            {
                endSeconds = value;
                RaisePropertyChanged();
            }
        }

        private void UpdateStartTime()
        {
            if (SelectedSiderealPathStartDateTimeProvider != null)
            {
                var t = SelectedSiderealPathStartDateTimeProvider.GetDateTime(this) + TimeSpan.FromMinutes(SiderealTrackStartOffsetMinutes);
                StartHours = t.Hour;
                StartMinutes = t.Minute;
                StartSeconds = t.Second;
            }

            UpdateModelPoints();
        }

        private void UpdateEndTime()
        {
            if (SelectedSiderealPathEndDateTimeProvider != null)
            {
                var t = SelectedSiderealPathEndDateTimeProvider.GetDateTime(this) + TimeSpan.FromMinutes(SiderealTrackEndOffsetMinutes);
                EndHours = t.Hour;
                EndMinutes = t.Minute;
                EndSeconds = t.Second;
            }

            UpdateModelPoints();
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            await TriggerRunner.Run(progress, token);
        }

        /*
        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            var modelBuilderOptions = new ModelBuilderOptions()
            {
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
                DisableRefractionCorrection = options.DisableRefractionCorrection,
                ModelPointGenerationType = ModelPointGenerationTypeEnum.SiderealPath
            };

            if (!await mountModelBuilderMediator.BuildModel(ModelPoints, modelBuilderOptions, token))
            {
                throw new Exception("ASA MLPT model build failed");
            }
        }*/

        public override void SequenceBlockInitialize()
        {
            if (!initialized)
            {
                initialTime = DateTime.Now;
                initialized = true;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            if (nextItem == null) { return false; }
            if (!(nextItem is IExposureItem exposureItem)) { return false; }
            if (exposureItem.ImageType != "LIGHT") { return false; }

            bool shouldTrigger = false;

            Elapsed = Math.Round((DateTime.Now - initialTime).TotalMinutes, 2);
            shouldTrigger = (DateTime.Now - initialTime) >= TimeSpan.FromMinutes(Amount);

            if (shouldTrigger)
            {
                if (ItemUtility.IsTooCloseToMeridianFlip(Parent, TriggerRunner.GetItemsSnapshot().First().GetEstimatedDuration() + nextItem?.GetEstimatedDuration() ?? TimeSpan.Zero))
                {
                    Logger.Warning("MLPT should be triggered, however the meridian flip is too close to be executed");
                    shouldTrigger = false;
                }
            }
            return shouldTrigger;
        }

        private ImmutableList<ModelPoint> ModelPoints = ImmutableList.Create<ModelPoint>();

        private void UpdateModelPoints()
        {
            if (SelectedSiderealPathStartDateTimeProvider == null || SelectedSiderealPathEndDateTimeProvider == null || Coordinates?.Coordinates == null || SiderealTrackRADeltaDegrees <= 0)
            {
                return;
            }

            try
            {
                ModelPoints = mountModelBuilderMediator.GenerateSiderealPath(
                    Coordinates,
                    Angle.ByDegree(SiderealTrackRADeltaDegrees),
                    SelectedSiderealPathStartDateTimeProvider,
                    SelectedSiderealPathEndDateTimeProvider,
                    SiderealTrackStartOffsetMinutes,
                    SiderealTrackEndOffsetMinutes);
                ModelPointCount = ModelPoints.Count(p => p.ModelPointState == ModelPointStateEnum.Generated);
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to generate MLTP model: {e.Message}");
            }
        }

        public bool Validate()
        {
            var i = new List<string>();
            /*if (!mountMediator.GetInfo().Connected)             // TODO CRASH

            {
                i.Add("ASA mount not connected");
            }*/
            if (!cameraMediator.GetInfo().Connected)
            {
                i.Add("Camera not connected");
            }
            if (!Inherited)
            {
                i.Add("Not within a container that has a target");
            }
            if (ModelPoints.Count < 3)
            {
                i.Add($"Model builds require at least 3 points. Only {ModelPoints.Count} points were generated");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged()
        {
            var contextCoordinates = ItemUtility.RetrieveContextCoordinates(this.Parent);
            if (contextCoordinates != null)
            {
                Coordinates.Coordinates = contextCoordinates.Coordinates;
                UpdateModelPoints();
                Inherited = true;
            }
            else
            {
                Inherited = false;
            }
            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(MLPTafterTime)}, Coordinates: {Coordinates?.Coordinates}, Inherited: {Inherited}, RADelta: {SiderealTrackRADeltaDegrees}, Start: {SelectedSiderealPathStartDateTimeProvider?.Name} ({SiderealTrackStartOffsetMinutes} minutes), Start: {SelectedSiderealPathEndDateTimeProvider?.Name} ({SiderealTrackEndOffsetMinutes} minutes), NumRetries: {BuilderNumRetries}, MaxFailedPoints: {MaxFailedPoints}, MaxPointRMS: {MaxPointRMS}";
        }
    }
}