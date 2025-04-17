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
using NINA.Photon.Plugin.ASA.SequenceItems;

namespace NINA.Photon.Plugin.ASA.MLTP
{
    [ExportMetadata("Name", "On Error")]
    [ExportMetadata("Description", "Trigger on any mount error")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class OnPositionError : SequenceTrigger, IValidatable
    {
        private IASAOptions options;
        private readonly IMountMediator mountMediator;
        private IMount mount;
        private readonly IMountModelBuilderMediator mountModelBuilderMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly ICameraMediator cameraMediator;

        private DateTime initialTime;
        private bool initialized = false;

        [ImportingConstructor]
        public OnPositionError(INighttimeCalculator nighttimeCalculator, ICameraMediator cameraMediator) :
            this(ASAPlugin.ASAOptions, ASAPlugin.MountMediator, ASAPlugin.Mount,
                ASAPlugin.MountModelBuilderMediator, ASAPlugin.ModelPointGenerator,
                nighttimeCalculator, cameraMediator)
        {
        }

        public OnPositionError(IASAOptions options, IMountMediator mountMediator, IMount mount,
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
        }

        private OnPositionError(OnPositionError cloneMe) : this(cloneMe.nighttimeCalculator, cloneMe.cameraMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            var cloned = new OnPositionError(this)
            {
                // Copy all scalar properties
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
                SiderealPathStartDateTimeProviders = this.SiderealPathStartDateTimeProviders,
                SiderealPathEndDateTimeProviders = this.SiderealPathEndDateTimeProviders,
                Amount = Amount
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

        /*
        public override void SequenceBlockTeardown()
        {
            initialized = false;
            initialTime = DateTime.MinValue;
            base.SequenceBlockTeardown();
        }
        */

        public override void Initialize()
        {
            initialTime = DateTime.Now;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            // delete old model
            // mount.MLTPStop();
        }

        [JsonProperty]
        public InputCoordinates Coordinates { get; set; }

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
        }

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
            bool shouldTrigger = false;

            if (nextItem == null) { return false; }

            string errorString = mount.ErrorString();
            var json = JsonConvert.DeserializeObject<MountErrorResponse>(errorString);
            if (json != null)
            {
                if (json.ErrornumberAxis1 != 0 || json.ErrornumberAxis2 != 0)
                {
                    /*      ComportCouldNotOpen		1
                            NoResponseDuringInit		2
                            OneTimeOutDuringReading		3
                            OneTimeOutDuringWriting		4
                            FatalReadingError		5
                            FatalWriteError		6
                            MotorPositionError		7
                            MotorOverCurrentError		8
                            CanInit_ConverterNotFound		9
                            CanInit_NoCommunication		10
                            MotorHitLimit		11
                            MotorOtherError		12
                            ComportLost		13
                            EncoderError		14
                            VoltageLow		15
                            VoltageHigh		16
                            PosMin		17
                            PosMax		18
                            HallError		19
                            OverTemperature		20
                            VelocityExceeded		21
                            WatchDogError		22
                    */

                    Logger.Error($"Mount error: {json.ErrornumberAxis1}, {json.ErrornumberAxis2}");
                    shouldTrigger = true;
                }
            }

            return shouldTrigger;
        }

        private ImmutableList<ModelPoint> ModelPoints = ImmutableList.Create<ModelPoint>();

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

        public override void AfterParentChanged()
        {
            var contextCoordinates = ItemUtility.RetrieveContextCoordinates(this.Parent);
            if (contextCoordinates != null)
            {
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
            return $"Category: {Category}, Item: {nameof(OnPositionError)}, Coordinates: {Coordinates?.Coordinates}, Inherited: {Inherited}, RADelta: {SiderealTrackRADeltaDegrees}, Start: {SelectedSiderealPathStartDateTimeProvider?.Name} ({SiderealTrackStartOffsetMinutes} minutes), Start: {SelectedSiderealPathEndDateTimeProvider?.Name} ({SiderealTrackEndOffsetMinutes} minutes), NumRetries: {BuilderNumRetries}, MaxFailedPoints: {MaxFailedPoints}, MaxPointRMS: {MaxPointRMS}";
        }
    }

    public class MountErrorResponse
    {
        public double JulianDate { get; set; }
        public double RigthAscension { get; set; }
        public double Declination { get; set; }
        public int Status { get; set; }
        public int ErrornumberAxis1 { get; set; }
        public int ErrornumberAxis2 { get; set; }
    }
}