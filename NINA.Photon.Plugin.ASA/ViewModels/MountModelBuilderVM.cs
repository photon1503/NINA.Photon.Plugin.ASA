#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment;
using NINA.Equipment.Equipment.MyDome;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Input;
using System.Windows.Threading;

using NINA.Image.Interfaces;
using NINA.PlateSolving;
using static System.Windows.Forms.AxHost;
using NINA.PlateSolving.Interfaces;

namespace NINA.Photon.Plugin.ASA.ViewModels
{
    [Export(typeof(IDockableVM))]
    public class MountModelBuilderVM : DockableVM, IMountModelBuilderVM, ITelescopeConsumer, IMountConsumer, IDomeConsumer
    {
        private static readonly CustomHorizon EMPTY_HORIZON = GetEmptyHorizon();
        private readonly IMountMediator mountMediator;
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly IModelBuilder modelBuilder;
        private readonly IFramingAssistantVM framingAssistant;

        private readonly IASAOptions modelBuilderOptions;
        private IProgress<ApplicationStatus> progress;
        private IProgress<ApplicationStatus> stepProgress;
        private bool disposed = false;
        private CancellationTokenSource disconnectCts;

        private readonly SynchronizationContext synchronizationContext =
        Application.Current?.Dispatcher != null
        ? new DispatcherSynchronizationContext(Application.Current.Dispatcher)
        : null;

        private bool hasValidGeneratedSiderealPath;

        [ImportingConstructor]
        public MountModelBuilderVM(IProfileService profileService, IApplicationStatusMediator applicationStatusMediator, ITelescopeMediator telescopeMediator, IDomeMediator domeMediator, IFramingAssistantVM framingAssistant, INighttimeCalculator nighttimeCalculator) :
            this(profileService,
                ASAPlugin.MountModelBuilderMediator,
                ASAPlugin.ASAOptions,
                telescopeMediator,
                domeMediator,
                framingAssistant,
                applicationStatusMediator,
                ASAPlugin.MountMediator,
                ASAPlugin.ModelPointGenerator,
                ASAPlugin.ModelBuilder,
                nighttimeCalculator)
        {
        }

        public MountModelBuilderVM(
            IProfileService profileService,
            IMountModelBuilderMediator mountModelBuilderMediator,
            IASAOptions modelBuilderOptions,
            ITelescopeMediator telescopeMediator,
            IDomeMediator domeMediator,
            IFramingAssistantVM framingAssistant,
            IApplicationStatusMediator applicationStatusMediator,
            IMountMediator mountMediator,
            IModelPointGenerator modelPointGenerator,
            IModelBuilder modelBuilder,
            INighttimeCalculator nighttimeCalculator) : base(profileService)
        {
            this.Title = "ASA Tools";

            var dict = new ResourceDictionary();
            dict.Source = new Uri("NINA.Photon.Plugin.ASA;component/Resources/SVGDataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ASASVG"];
            ImageGeometry.Freeze();

            mountModelBuilderMediator.RegisterHandler(this);

            this.modelBuilderOptions = modelBuilderOptions;
            this.applicationStatusMediator = applicationStatusMediator;
            this.mountMediator = mountMediator;
            this.telescopeMediator = telescopeMediator;
            this.domeMediator = domeMediator;
            this.framingAssistant = framingAssistant;
            this.modelPointGenerator = modelPointGenerator;
            this.modelBuilder = modelBuilder;
            this.modelBuilder.PointNextUp += ModelBuilder_PointNextUp;
            this.modelBuilderOptions.PropertyChanged += ModelBuilderOptions_PropertyChanged;

            this.SiderealPathStartDateTimeProviders = ImmutableList.Create<IDateTimeProvider>(
                new NowDateTimeProvider(new SystemDateTime()),
                new NauticalDuskProvider(nighttimeCalculator),
                new SunsetProvider(nighttimeCalculator),
                new DuskProvider(nighttimeCalculator));
            this.SelectedSiderealPathStartDateTimeProvider = this.SiderealPathStartDateTimeProviders.FirstOrDefault(p => p.Name == modelBuilderOptions.SiderealTrackStartTimeProvider);
            this.SiderealPathEndDateTimeProviders = ImmutableList.Create<IDateTimeProvider>(
                new NowDateTimeProvider(new SystemDateTime()),
                new NauticalDawnProvider(nighttimeCalculator),
                new SunriseProvider(nighttimeCalculator),
                new DawnProvider(nighttimeCalculator));
            this.SelectedSiderealPathEndDateTimeProvider = this.SiderealPathEndDateTimeProviders.FirstOrDefault(p => p.Name == modelBuilderOptions.SiderealTrackEndTimeProvider);

            this.disconnectCts = new CancellationTokenSource();

            this.telescopeMediator.RegisterConsumer(this);
            this.domeMediator.RegisterConsumer(this);
            this.mountMediator.RegisterConsumer(this);

            this.profileService.ProfileChanged += ProfileService_ProfileChanged;
            this.profileService.ActiveProfile.AstrometrySettings.PropertyChanged += AstrometrySettings_PropertyChanged;
            this.profileService.ActiveProfile.DomeSettings.PropertyChanged += DomeSettings_PropertyChanged;
            this.LoadHorizon();

            this.GeneratePointsCommand = new AsyncRelayCommand(GeneratePoints);
            this.ClearPointsCommand = new AsyncRelayCommand(ClearPoints);
            this.AddHighAltitudeCommand = new AsyncRelayCommand(AddHighAltitude);

            this.BuildCommand = new AsyncRelayCommand(BuildModel);
            this.CancelBuildCommand = new AsyncRelayCommand(CancelBuildModel);
            this.StopBuildCommand = new AsyncRelayCommand(StopBuildModel);
            this.CoordsFromFramingCommand = new AsyncRelayCommand(CoordsFromFraming);
            this.CoordsFromScopeCommand = new AsyncRelayCommand(CoordsFromScope);
            this.ImportCommand = new AsyncRelayCommand(ImportPoints);
            this.ExportCommand = new AsyncRelayCommand(ExportPoints);

            this.ModelPointGenerationType = ModelPointGenerationTypeEnum.GoldenSpiral;

            // progress

            if (SynchronizationContext.Current == synchronizationContext)
            {
                this.progress = new Progress<ApplicationStatus>(p =>
                {
                    p.Source = this.Title;
                    this.applicationStatusMediator.StatusUpdate(p);
                });
            }
            else
            {
                synchronizationContext.Send(_ =>
                {
                    this.progress = new Progress<ApplicationStatus>(p =>
                    {
                        p.Source = this.Title;
                        this.applicationStatusMediator.StatusUpdate(p);
                    });
                }, null);
            }

            SubscribeDisplayModelPoints(displayModelPoints);
            RefreshMlptErrorCharts();
        }

        private void SubscribeDisplayModelPoints(AsyncObservableCollection<ModelPoint> points)
        {
            if (points == null)
            {
                return;
            }

            points.CollectionChanged += DisplayModelPoints_CollectionChanged;
            foreach (var point in points)
            {
                point.PropertyChanged += DisplayModelPoint_PropertyChanged;
            }
        }

        private void UnsubscribeDisplayModelPoints(AsyncObservableCollection<ModelPoint> points)
        {
            if (points == null)
            {
                return;
            }

            points.CollectionChanged -= DisplayModelPoints_CollectionChanged;
            foreach (var point in points)
            {
                point.PropertyChanged -= DisplayModelPoint_PropertyChanged;
            }
        }

        private void DisplayModelPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ModelPoint oldPoint in e.OldItems)
                {
                    oldPoint.PropertyChanged -= DisplayModelPoint_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ModelPoint newPoint in e.NewItems)
                {
                    newPoint.PropertyChanged += DisplayModelPoint_PropertyChanged;
                }
            }

            RefreshMlptPlannedImageCount();
            RefreshMlptErrorCharts();
            RefreshDisplayPathPoints();
        }

        private void DisplayModelPoint_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName)
                || e.PropertyName == nameof(ModelPoint.Azimuth)
                || e.PropertyName == nameof(ModelPoint.Altitude)
                || e.PropertyName == nameof(ModelPoint.ModelPointState))
            {
                RefreshDisplayPathPoints();
            }

            if (string.IsNullOrWhiteSpace(e.PropertyName)
                || e.PropertyName == nameof(ModelPoint.MountReportedRightAscension)
                || e.PropertyName == nameof(ModelPoint.PlateSolvedRightAscension)
                || e.PropertyName == nameof(ModelPoint.MountReportedDeclination)
                || e.PropertyName == nameof(ModelPoint.PlateSolvedDeclination)
                || e.PropertyName == nameof(ModelPoint.CaptureTime)
                || e.PropertyName == nameof(ModelPoint.ModelIndex)
                || e.PropertyName == nameof(ModelPoint.ModelPointState))
            {
                RefreshMlptErrorCharts();
            }
        }

        private static double NormalizeSignedDifference(double value, double period)
        {
            var normalized = value % period;
            if (normalized > period / 2.0)
            {
                normalized -= period;
            }
            else if (normalized < -period / 2.0)
            {
                normalized += period;
            }

            return normalized;
        }

        private void RefreshMlptErrorCharts()
        {
            var pointsWithSolveData = DisplayModelPoints
                .Where(point => !double.IsNaN(point.MountReportedRightAscension)
                             && !double.IsNaN(point.PlateSolvedRightAscension)
                             && !double.IsNaN(point.MountReportedDeclination)
                             && !double.IsNaN(point.PlateSolvedDeclination)
                             && point.ModelPointState == ModelPointStateEnum.AddedToModel)
                .OrderBy(point => point.CaptureTime == DateTime.MinValue ? DateTime.MaxValue : point.CaptureTime)
                .ThenBy(point => point.ModelIndex)
                .ToList();

            var raPoints = new List<DataPoint>();
            var dePoints = new List<DataPoint>();
            double maxRaErrorArcsec = 0.0;
            double maxDeErrorArcsec = 0.0;

            for (int pointIndex = 0; pointIndex < pointsWithSolveData.Count; pointIndex++)
            {
                var point = pointsWithSolveData[pointIndex];

                var raDifferenceHours = NormalizeSignedDifference(
                    point.PlateSolvedRightAscension - point.MountReportedRightAscension,
                    24.0);
                var raErrorArcsec = raDifferenceHours * 15.0 * 3600.0;

                var deDifferenceDegrees = NormalizeSignedDifference(
                    point.PlateSolvedDeclination - point.MountReportedDeclination,
                    360.0);
                var deErrorArcsec = deDifferenceDegrees * 3600.0;

                var imageNumber = pointIndex + 1;
                raPoints.Add(new DataPoint(imageNumber, raErrorArcsec));
                dePoints.Add(new DataPoint(imageNumber, deErrorArcsec));

                maxRaErrorArcsec = Math.Max(maxRaErrorArcsec, Math.Abs(raErrorArcsec));
                maxDeErrorArcsec = Math.Max(maxDeErrorArcsec, Math.Abs(deErrorArcsec));
            }

            MlptRaErrorPoints = new AsyncObservableCollection<DataPoint>(raPoints);
            MlptDeErrorPoints = new AsyncObservableCollection<DataPoint>(dePoints);

            MaxMlptRaErrorArcsec = maxRaErrorArcsec;
            MaxMlptDeErrorArcsec = maxDeErrorArcsec;

            MlptRaErrorAxisLimitArcsec = Math.Max(1.0, Math.Ceiling(maxRaErrorArcsec));
            MlptDeErrorAxisLimitArcsec = Math.Max(1.0, Math.Ceiling(maxDeErrorArcsec));
        }

        private void RefreshMlptPlannedImageCount()
        {
            if (this.ModelPointGenerationType == ModelPointGenerationTypeEnum.SiderealPath && !hasValidGeneratedSiderealPath)
            {
                MlptPlannedImageCount = 3;
                return;
            }

            var currentCount = DisplayModelPoints?.Count ?? 0;
            MlptPlannedImageCount = Math.Max(1, currentCount);
        }

        private void ModelBuilderOptions_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(modelBuilderOptions.ShowRemovedPoints))
            {
                UpdateDisplayModelPoints();
            }
        }

        private void UpdateDisplayModelPoints()
        {
            if (!this.modelBuilderOptions.ShowRemovedPoints)
            {
                var localModelPoints = this.ModelPoints.Where(mp => mp.ModelPointState == ModelPointStateEnum.Generated);
                this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(localModelPoints);
            }
            else
            {
                this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(this.ModelPoints);
            }
        }

        private void RefreshDisplayPathPoints()
        {
            if (!ShowDisplayPath || DisplayModelPoints == null || DisplayModelPoints.Count == 0)
            {
                DisplayPathPoints = new AsyncObservableCollection<DataPoint>();
                DisplayPathPointsPolar = new AsyncObservableCollection<DataPoint>();
                return;
            }

            var previewOptions = new ModelBuilderOptions()
            {
                WestToEastSorting = modelBuilderOptions.WestToEastSorting,
                NumRetries = BuilderNumRetries,
                MaxPointRMS = MaxPointRMS,
                MinimizeDomeMovement = modelBuilderOptions.MinimizeDomeMovementEnabled,
                MinimizeMeridianFlips = modelBuilderOptions.MinimizeMeridianFlipsEnabled,
                AllowBlindSolves = modelBuilderOptions.AllowBlindSolves,
                MaxConcurrency = modelBuilderOptions.MaxConcurrency,
                DomeShutterWidth_mm = modelBuilderOptions.DomeShutterWidth_mm,
                MaxFailedPoints = MaxFailedPoints,
                RemoveHighRMSPointsAfterBuild = modelBuilderOptions.RemoveHighRMSPointsAfterBuild,
                PlateSolveSubframePercentage = modelBuilderOptions.PlateSolveSubframePercentage,
                UseSync = modelBuilderOptions.UseSync,
                SyncEastAltitude = modelBuilderOptions.SyncEastAltitude,
                SyncWestAltitude = modelBuilderOptions.SyncWestAltitude,
                SyncEastAzimuth = modelBuilderOptions.SyncEastAzimuth,
                SyncWestAzimuth = modelBuilderOptions.SyncWestAzimuth,
                SyncEveryHA = modelBuilderOptions.SyncEveryHA,
                RefEastAltitude = modelBuilderOptions.RefEastAltitude,
                RefWestAltitude = modelBuilderOptions.RefWestAltitude,
                RefEastAzimuth = modelBuilderOptions.RefEastAzimuth,
                RefWestAzimuth = modelBuilderOptions.RefWestAzimuth,
                ModelPointGenerationType = modelBuilderOptions.ModelPointGenerationType,
                AutoGridPathOrderingMode = modelBuilderOptions.AutoGridPathOrderingMode,
                DomeControlNINA = modelBuilderOptions.DomeControlNINA,
            };

            var orderedPoints = modelBuilder.GetPreviewOrder(ModelPoints.ToList(), previewOptions);
            var points = orderedPoints
                .Where(p => !double.IsNaN(p.Azimuth) && !double.IsNaN(p.Altitude))
                .Select(p => new DataPoint(p.Azimuth, p.Altitude));

            var pointsPolar = orderedPoints
                .Where(p => !double.IsNaN(p.Azimuth) && !double.IsNaN(p.Altitude))
                .Select(p => new DataPoint(p.InvertedAltitude, p.Azimuth));

            DisplayPathPoints = new AsyncObservableCollection<DataPoint>(points);
            DisplayPathPointsPolar = new AsyncObservableCollection<DataPoint>(pointsPolar);
        }

        private void ModelBuilder_PointNextUp(object sender, PointNextUpEventArgs e)
        {
            if (e.Point == null || double.IsNaN(e.Point.DomeAzimuth))
            {
                this.NextUpDomePosition = new DataPoint();
                this.ShowNextUpDomePosition = false;
            }
            else
            {
                this.NextUpDomePosition = new DataPoint(e.Point.DomeAzimuth, e.Point.DomeAltitude);
                this.ShowNextUpDomePosition = true;
            }
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e)
        {
            this.profileService.ActiveProfile.AstrometrySettings.PropertyChanged += AstrometrySettings_PropertyChanged;
            this.profileService.ActiveProfile.DomeSettings.PropertyChanged += DomeSettings_PropertyChanged;
            this.LoadHorizon();
        }

        private void DomeSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.profileService.ActiveProfile.DomeSettings.AzimuthTolerance_degrees))
            {
                _ = CalculateDomeShutterOpening(disconnectCts.Token);
            }
        }

        private void AstrometrySettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.profileService.ActiveProfile.AstrometrySettings.Horizon))
            {
                this.LoadHorizon();
            }
        }

        private void LoadHorizon()
        {
            this.CustomHorizon = this.profileService.ActiveProfile.AstrometrySettings.Horizon;
            if (this.CustomHorizon == null)
            {
                this.HorizonDataPoints.Clear();
            }
            else
            {
                var dataPoints = new List<DataPoint>();
                for (double azimuth = 0.0; azimuth <= 360.0; azimuth += 1.0)
                {
                    var horizonAltitude = CustomHorizon.GetAltitude(azimuth);
                    dataPoints.Add(new DataPoint(azimuth, horizonAltitude));
                }
                this.HorizonDataPoints = new AsyncObservableCollection<DataPoint>(dataPoints);
            }
        }

        public override bool IsTool { get; } = true;

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.telescopeMediator.RemoveConsumer(this);
                this.mountMediator.RemoveConsumer(this);
                this.disposed = true;
            }
        }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo)
        {
            this.TelescopeInfo = deviceInfo;

            TelescopePosition = new DataPoint(deviceInfo.Azimuth, deviceInfo.Altitude);
            RaisePropertyChanged(nameof(TelescopeInfo2));
        }

        private static readonly Angle DomeShutterOpeningRefreshTolerance = Angle.ByDegree(1.0);

        public void UpdateDeviceInfo(DomeInfo deviceInfo)
        {
            this.DomeInfo = deviceInfo;
            this.DomeControlEnabled = this.DomeInfo.Connected && this.DomeInfo.CanSetAzimuth;
            if (this.DomeControlEnabled)
            {
                var currentAzimuth = Angle.ByDegree(this.DomeInfo.Azimuth);
                if (domeShutterAzimuthForOpening == null || !currentAzimuth.Equals(domeShutterAzimuthForOpening, DomeShutterOpeningRefreshTolerance))
                {
                    // Asynchronously update the dome shutter opening after the dome azimuth changes beyond the threshold it was last calculated for
                    _ = CalculateDomeShutterOpening(disconnectCts.Token);
                    domeShutterAzimuthForOpening = currentAzimuth;
                }
            }
        }

        public void UpdateDeviceInfo(MountInfo deviceInfo)
        {
            this.MountInfo = deviceInfo;
            if (this.MountInfo.Connected)
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }

        private MountInfo mountInfo = DeviceInfo.CreateDefaultInstance<MountInfo>();

        public MountInfo MountInfo
        {
            get => mountInfo;
            private set
            {
                mountInfo = value;
                RaisePropertyChanged();
            }
        }

        public TelescopeInfo TelescopeInfo2 { get; private set; }

        private TelescopeInfo telescopeInfo = DeviceInfo.CreateDefaultInstance<TelescopeInfo>();

        public TelescopeInfo TelescopeInfo
        {
            get => telescopeInfo;
            private set
            {
                telescopeInfo = value;
                RaisePropertyChanged();
                if (Connected)
                {
                    ScopePosition = new DataPoint(telescopeInfo.Azimuth, telescopeInfo.Altitude);
                }
            }
        }

        private DataPoint telescopePosition;

        public DataPoint TelescopePosition
        {
            get => telescopePosition;
            set
            {
                if (telescopePosition.X != value.X || telescopePosition.Y != value.Y)
                {
                    telescopePosition = value;
                    TelescopePositionInverted = new DataPoint(value.X, 90 - value.Y);
                    RaisePropertyChanged();
                }
            }
        }

        private DataPoint telescopePositionInverted;

        public DataPoint TelescopePositionInverted
        {
            get => telescopePositionInverted;
            set
            {
                telescopePositionInverted = value;
                RaisePropertyChanged();
            }
        }

        private DomeInfo domeInfo = DeviceInfo.CreateDefaultInstance<DomeInfo>();

        public DomeInfo DomeInfo
        {
            get => domeInfo;
            private set
            {
                domeInfo = value;
                RaisePropertyChanged();
            }
        }

        private bool connected;

        public bool Connected
        {
            get => connected;
            private set
            {
                if (connected != value)
                {
                    connected = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void Connect()
        {
            if (Connected)
            {
                return;
            }

            if (this.progress == null)
            {
                this.progress = new Progress<ApplicationStatus>(p =>
                {
                    p.Source = this.Title;
                    this.applicationStatusMediator.StatusUpdate(p);
                });
            }
            if (this.stepProgress == null)
            {
                this.stepProgress = new Progress<ApplicationStatus>(p =>
                {
                    p.Source = "ASA Build Step";
                    this.applicationStatusMediator.StatusUpdate(p);
                });
            }

            this.disconnectCts?.Cancel();
            this.disconnectCts = new CancellationTokenSource();
            this.domeShutterAzimuthForOpening = null;
            this.DomeShutterOpeningDataPoints.Clear();
            this.DomeShutterOpeningDataPoints2.Clear();
            this.BuildInProgress = false;
            this.TelescopeInfo = telescopeMediator.GetInfo();
            Connected = true;
        }

        private void Disconnect()
        {
            if (!Connected)
            {
                return;
            }

            this.disconnectCts?.Cancel();
            Connected = false;
        }

        private Task<bool> GeneratePoints()
        {
            try
            {
                if (this.ModelPointGenerationType == ModelPointGenerationTypeEnum.GoldenSpiral)
                {
                    return Task.FromResult(GenerateGoldenSpiral(this.GoldenSpiralStarCount, true));
                }
                else if (this.ModelPointGenerationType == ModelPointGenerationTypeEnum.AutoGrid)
                {
                    return Task.FromResult(GenerateAutoGrid(true));
                }
                else if (this.ModelPointGenerationType == ModelPointGenerationTypeEnum.SiderealPath)
                {
                    return Task.FromResult(GenerateSiderealPath(false));
                }
                else
                {
                    throw new ArgumentException($"Unexpected Model Point Generation Type {this.ModelPointGenerationType}");
                }
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to generate points. {e.Message}");
                Logger.Error($"Failed to generate points", e);
                return Task.FromResult(false);
            }
        }

        private Task<bool> AddHighAltitude()
        {
            // Define altitude range and number of stars
            double Altmin = this.HighAltitudeMin;
            double Altmax = this.HighAltitudeMax;
            int stars = this.HighAltitudeStars;

            var newPoints = new List<ModelPoint>();

            // Golden angle in radians
            double goldenAngle = Math.PI * (3 - Math.Sqrt(5));
            double azimuthStep = 360.0 / stars;
            double azimuthOffset = azimuthStep / 2.0; // Offset to ensure equal distance to 0 and 360

            // Loop to create model points
            for (int i = 0; i < stars; i++)
            {
                // Calculate latitude and longitude using the golden angle method
                double latitude = Math.Asin(-1.0 + 2.0 * (i + 0.5) / stars);
                double longitude = goldenAngle * i;

                // Convert latitude to altitude within the specified range
                double normalizedLatitude = (latitude + Math.PI / 2) / Math.PI; // Normalize to [0, 1]
                double Alt = Altmin + normalizedLatitude * (Altmax - Altmin); // Map to [Altmin, Altmax]

                // Convert longitude to azimuth and apply offset
                double Az = ((longitude * 180 / Math.PI) + azimuthOffset) % 360; // Convert to degrees, apply offset, and wrap around

                // Create and add model point
                newPoints.Add(new ModelPoint(telescopeMediator)
                {
                    Altitude = Alt,
                    Azimuth = Az,
                    ModelPointState = ModelPointStateEnum.Generated
                });
            }

            // Retrieve existing points
            var existingModelPoints = this.ModelPoints.ToList();
            var existingDisplayModelPoints = this.DisplayModelPoints.ToList();

            // Add new points to existing points
            existingModelPoints.AddRange(newPoints);
            existingDisplayModelPoints.AddRange(newPoints);

            // Update ModelPoints and DisplayModelPoints
            this.ModelPoints = existingModelPoints.ToImmutableList();
            this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(existingDisplayModelPoints);

            return Task.FromResult(true);
        }

        private Task<bool> ClearPoints()
        {
            this.ModelPoints.Clear();
            this.DisplayModelPoints.Clear();
            hasValidGeneratedSiderealPath = false;
            RefreshMlptPlannedImageCount();
            return Task.FromResult(true);
        }

        private bool GenerateGoldenSpiral(int goldenSpiralStarCount, bool showNotifications)
        {
            hasValidGeneratedSiderealPath = false;
            var localModelPoints = this.modelPointGenerator.GenerateGoldenSpiral(goldenSpiralStarCount, this.CustomHorizon);
            this.ModelPoints = ImmutableList.ToImmutableList(localModelPoints);
            if (!this.modelBuilderOptions.ShowRemovedPoints)
            {
                localModelPoints = localModelPoints.Where(mp => mp.ModelPointState == ModelPointStateEnum.Generated).ToList();
            }
            if (showNotifications)
            {
                var numPoints = localModelPoints.Count(mp => mp.ModelPointState == ModelPointStateEnum.Generated);
                Notification.ShowInformation($"Generated {numPoints} points");
            }
            this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(localModelPoints);
            return true;
        }

        public ImmutableList<ModelPoint> GenerateGoldenSpiral(int goldenSpiralStarCount)
        {
            ModelPointGenerationType = ModelPointGenerationTypeEnum.GoldenSpiral;
            if (!GenerateGoldenSpiral(goldenSpiralStarCount, false))
            {
                throw new Exception("Failed to generate golden spiral");
            }
            return this.ModelPoints;
        }

        private bool GenerateAutoGrid(bool showNotifications)
        {
            hasValidGeneratedSiderealPath = false;
            List<ModelPoint> localModelPoints;
            if (this.AutoGridInputMode == AutoGridInputModeEnum.DesiredPoints)
            {
                localModelPoints = this.modelPointGenerator.GenerateAutoGridByPointCount(this.AutoGridDesiredPointCount, this.CustomHorizon);
            }
            else
            {
                localModelPoints = this.modelPointGenerator.GenerateAutoGrid(this.AutoGridRASpacingDegrees, this.AutoGridDecSpacingDegrees, this.CustomHorizon);
            }

            this.ModelPoints = ImmutableList.ToImmutableList(localModelPoints);
            if (!this.modelBuilderOptions.ShowRemovedPoints)
            {
                localModelPoints = localModelPoints.Where(mp => mp.ModelPointState == ModelPointStateEnum.Generated).ToList();
            }
            if (showNotifications)
            {
                var numPoints = localModelPoints.Count(mp => mp.ModelPointState == ModelPointStateEnum.Generated);
                Notification.ShowInformation($"Generated {numPoints} points");
            }
            this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(localModelPoints);
            return true;
        }

        public ImmutableList<ModelPoint> GenerateAutoGrid(double raSpacingDegrees, double decSpacingDegrees)
        {
            AutoGridRASpacingDegrees = raSpacingDegrees;
            AutoGridDecSpacingDegrees = decSpacingDegrees;
            AutoGridInputMode = AutoGridInputModeEnum.Spacing;
            ModelPointGenerationType = ModelPointGenerationTypeEnum.AutoGrid;
            if (!GenerateAutoGrid(false))
            {
                throw new Exception("Failed to generate auto grid");
            }
            return this.ModelPoints;
        }

        public ImmutableList<ModelPoint> GenerateAutoGrid(int desiredPointCount)
        {
            AutoGridDesiredPointCount = desiredPointCount;
            AutoGridInputMode = AutoGridInputModeEnum.DesiredPoints;
            ModelPointGenerationType = ModelPointGenerationTypeEnum.AutoGrid;
            if (!GenerateAutoGrid(false))
            {
                throw new Exception("Failed to generate auto grid");
            }
            return this.ModelPoints;
        }

        public ImmutableList<ModelPoint> GenerateSiderealPath(InputCoordinates coordinates, Angle raDelta, IDateTimeProvider startTimeProvider, IDateTimeProvider endTimeProvider, int startOffsetMinutes, int endOffsetMinutes)
        {
            SiderealPathObjectCoordinates = coordinates;
            SiderealTrackRADeltaDegrees = raDelta.Degree;
            SelectedSiderealPathStartDateTimeProvider = SiderealPathStartDateTimeProviders.FirstOrDefault(p => p.Name == startTimeProvider.Name);
            SelectedSiderealPathEndDateTimeProvider = SiderealPathEndDateTimeProviders.FirstOrDefault(p => p.Name == endTimeProvider.Name);
            SiderealTrackStartOffsetMinutes = startOffsetMinutes;
            SiderealTrackEndOffsetMinutes = endOffsetMinutes;
            ModelPointGenerationType = ModelPointGenerationTypeEnum.SiderealPath;

            if (!GenerateSiderealPath(false))
            {
                //if (Connected)
                throw new Exception("Failed to generate MLPT path");
            }

            return this.ModelPoints;
        }

        private bool GenerateSiderealPath(bool showNotifications)
        {
            hasValidGeneratedSiderealPath = false;

            //     if (Connected == false)
            //     { return false; }

            if (SiderealPathObjectCoordinates == null)
            {
                if (showNotifications)
                {
                    Notification.ShowError("No object selected");
                }
                return false;
            }
            if (SelectedSiderealPathStartDateTimeProvider == null)
            {
                if (showNotifications)
                {
                    Notification.ShowError("No start time provider selected");
                }
                return false;
            }
            if (SelectedSiderealPathEndDateTimeProvider == null)
            {
                if (showNotifications)
                {
                    Notification.ShowError("No start time provider selected");
                }
                return false;
            }
            var startTime = SelectedSiderealPathStartDateTimeProvider.GetDateTime(null);
            var endTime = SelectedSiderealPathEndDateTimeProvider.GetDateTime(null);
            if (endTime < startTime)
            {
                endTime += TimeSpan.FromDays(1);
            }

            startTime += TimeSpan.FromMinutes(SiderealTrackStartOffsetMinutes);
            endTime += TimeSpan.FromMinutes(SiderealTrackEndOffsetMinutes);
            if (endTime < startTime)
            {
                endTime += TimeSpan.FromDays(1);
            }

            Logger.Info($"Generating MLTP path. Coordinates={SiderealPathObjectCoordinates.Coordinates}, RADelta={SiderealTrackRADeltaDegrees}, StartTime={startTime}, EndTime={endTime}");
            try
            {
                var localModelPoints = this.modelPointGenerator.GenerateSiderealPath(SiderealPathObjectCoordinates.Coordinates, Angle.ByDegree(SiderealTrackRADeltaDegrees), startTime, endTime, CustomHorizon);
                this.ModelPoints = ImmutableList.ToImmutableList(localModelPoints);
                hasValidGeneratedSiderealPath = this.ModelPoints.Count >= 2;
                if (!this.modelBuilderOptions.ShowRemovedPoints)
                {
                    localModelPoints = localModelPoints.Where(mp => mp.ModelPointState == ModelPointStateEnum.Generated).ToList();
                }
                var numPoints = localModelPoints.Count(mp => mp.ModelPointState == ModelPointStateEnum.Generated);
                if (showNotifications)
                {
                    Notification.ShowInformation($"Generated {numPoints} points");
                }

                this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(localModelPoints);
                return true;
            }
            catch (Exception e)
            {
                if (showNotifications)
                {
                    Notification.ShowError($"Failed to generate MLPT. {e.Message}");
                }
                Logger.Error($"Failed to generate MLPT path. Coordinates={SiderealPathObjectCoordinates?.Coordinates}, RADelta={SiderealTrackRADeltaDegrees}, StartTime={startTime}, EndTime={endTime}", e);
                return false;
            }
        }

        private CancellationTokenSource modelBuildCts;
        private CancellationTokenSource modelBuildStopCts;
        private Task<LoadedAlignmentModel> modelBuildTask;

        public Task<bool> BuildModel(IList<ModelPoint> modelPoints, ModelBuilderOptions options, CancellationToken ct)
        {
            if (modelBuildCts != null)
            {
                throw new Exception("Model build already in progress");
            }

            this.ModelPoints = ImmutableList.ToImmutableList(modelPoints);
            UpdateDisplayModelPoints();

            // Update VM and options to reflect requested build settings
            modelBuilderOptions.WestToEastSorting = options.WestToEastSorting;
            BuilderNumRetries = options.NumRetries;
            MaxPointRMS = options.MaxPointRMS;
            modelBuilderOptions.MinimizeDomeMovementEnabled = options.MinimizeDomeMovement;
            modelBuilderOptions.MinimizeMeridianFlipsEnabled = options.MinimizeMeridianFlips;
            modelBuilderOptions.AllowBlindSolves = options.AllowBlindSolves;
            modelBuilderOptions.MaxConcurrency = options.MaxConcurrency;
            modelBuilderOptions.DomeShutterWidth_mm = options.DomeShutterWidth_mm;
            MaxFailedPoints = options.MaxFailedPoints;
            modelBuilderOptions.RemoveHighRMSPointsAfterBuild = options.RemoveHighRMSPointsAfterBuild;
            modelBuilderOptions.PlateSolveSubframePercentage = options.PlateSolveSubframePercentage;
            modelBuilderOptions.DisableRefractionCorrection = options.DisableRefractionCorrection;
            modelBuilderOptions.UseSync = options.UseSync;
            modelBuilderOptions.SyncEastAltitude = options.SyncEastAltitude;
            modelBuilderOptions.SyncWestAltitude = options.SyncWestAltitude;
            modelBuilderOptions.SyncEastAzimuth = options.SyncEastAzimuth;
            modelBuilderOptions.SyncWestAzimuth = options.SyncWestAzimuth;
            modelBuilderOptions.RefEastAltitude = options.RefEastAltitude;
            modelBuilderOptions.RefWestAltitude = options.RefWestAltitude;
            modelBuilderOptions.RefEastAzimuth = options.RefEastAzimuth;
            modelBuilderOptions.RefWestAzimuth = options.RefWestAzimuth;
            modelBuilderOptions.SyncEveryHA = options.SyncEveryHA;
            modelBuilderOptions.AutoGridPathOrderingMode = options.AutoGridPathOrderingMode;
            return DoBuildModel(modelPoints, options, ct);
        }

        private async Task<bool> DoBuildModel(IList<ModelPoint> modelPoints, ModelBuilderOptions options, CancellationToken ct)
        {
            try
            {
                if (modelBuildCts != null)
                {
                    throw new Exception("Model build already in progress");
                }
                Notification.ShowInformation("Model build started");

                BuildInProgress = true;
                modelBuildCts = new CancellationTokenSource();
                modelBuildStopCts = new CancellationTokenSource();
                var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, modelBuildCts.Token);
                modelBuildTask = modelBuilder.Build(modelPoints, options, cancelTokenSource.Token, modelBuildStopCts.Token, progress, stepProgress);
                var builtModel = await modelBuildTask;
                modelBuildTask = null;
                modelBuildCts = null;
                modelBuildStopCts = null;
                Notification.ShowInformation($"ASA model build completed");
                return true;
            }
            catch (OperationCanceledException)
            {
                Notification.ShowInformation("Model build cancelled");
                Logger.Info("Model build cancelled");
                return false;
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to build model. {e.Message}");
                Logger.Error($"Failed to build model", e);
                return false;
            }
            finally
            {
                modelBuildCts?.Cancel();
                modelBuildCts = null;
                modelBuildStopCts = null;
                BuildInProgress = false;
            }
        }

        private Task<bool> BuildModel()
        {
            if (modelBuildCts != null)
            {
                throw new Exception("Model build already in progress");
            }

            var options = new ModelBuilderOptions()
            {
                WestToEastSorting = modelBuilderOptions.WestToEastSorting,
                NumRetries = BuilderNumRetries,
                MaxPointRMS = MaxPointRMS,
                MinimizeDomeMovement = modelBuilderOptions.MinimizeDomeMovementEnabled,
                MinimizeMeridianFlips = modelBuilderOptions.MinimizeMeridianFlipsEnabled,
                AllowBlindSolves = modelBuilderOptions.AllowBlindSolves,
                MaxConcurrency = modelBuilderOptions.MaxConcurrency,
                DomeShutterWidth_mm = modelBuilderOptions.DomeShutterWidth_mm,
                MaxFailedPoints = MaxFailedPoints,
                RemoveHighRMSPointsAfterBuild = modelBuilderOptions.RemoveHighRMSPointsAfterBuild,
                PlateSolveSubframePercentage = modelBuilderOptions.PlateSolveSubframePercentage,
                UseSync = modelBuilderOptions.UseSync,
                SyncEastAltitude = modelBuilderOptions.SyncEastAltitude,
                SyncWestAltitude = modelBuilderOptions.SyncWestAltitude,
                SyncEastAzimuth = modelBuilderOptions.SyncEastAzimuth,
                SyncWestAzimuth = modelBuilderOptions.SyncWestAzimuth,
                SyncEveryHA = modelBuilderOptions.SyncEveryHA,
                RefEastAltitude = modelBuilderOptions.RefEastAltitude,
                RefWestAltitude = modelBuilderOptions.RefWestAltitude,
                RefEastAzimuth = modelBuilderOptions.RefEastAzimuth,
                RefWestAzimuth = modelBuilderOptions.RefWestAzimuth,
                ModelPointGenerationType = modelBuilderOptions.ModelPointGenerationType,
                AutoGridPathOrderingMode = modelBuilderOptions.AutoGridPathOrderingMode,
            };
            var modelPoints = ModelPoints.ToList();
            return DoBuildModel(modelPoints, options, CancellationToken.None);
        }

        private async Task<bool> CancelBuildModel()
        {
            try
            {
                modelBuildCts?.Cancel();
                var localModelBuildTask = modelBuildTask;
                if (localModelBuildTask != null)
                {
                    await localModelBuildTask;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> StopBuildModel()
        {
            try
            {
                modelBuildStopCts?.Cancel();
                var localModelBuildTask = modelBuildTask;
                if (localModelBuildTask != null)
                {
                    await localModelBuildTask;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Task<bool> CoordsFromFraming()
        {
            try
            {
                this.SiderealPathObjectCoordinates = new InputCoordinates(framingAssistant.DSO.Coordinates);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        private Task<bool> CoordsFromScope()
        {
            try
            {
                var telescopeInfo = telescopeMediator.GetInfo();
                this.SiderealPathObjectCoordinates = new InputCoordinates(telescopeInfo.Coordinates);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        private static CustomHorizon GetEmptyHorizon()
        {
            var horizonDefinition = $"0 0" + Environment.NewLine + "360 0";
            using (var sr = new StringReader(horizonDefinition))
            {
                return CustomHorizon.FromReader_Standard(sr);
            }
        }

        private Task calculateDomeShutterOpeningTask;

        private async Task CalculateDomeShutterOpening(CancellationToken ct)
        {
            if (calculateDomeShutterOpeningTask != null)
            {
                await calculateDomeShutterOpeningTask;
                return;
            }

            calculateDomeShutterOpeningTask = Task.Run(() =>
            {
                var azimuth = DomeInfo.Azimuth;
                if (modelBuilderOptions.DomeShutterWidth_mm <= 0.0)
                {
                    CalculateFixedThresholdDomeShutterOpening(azimuth, ct);
                }
                else
                {
                    CalculateAzimuthAwareDomeShutterOpening(azimuth, ct);
                }
            }, ct);

            try
            {
                await calculateDomeShutterOpeningTask;
            }
            catch (Exception e)
            {
                Logger.Error("Failed to calculate dome shutter opening", e);
            }
            finally
            {
                calculateDomeShutterOpeningTask = null;
            }
        }

        private void CalculateFixedThresholdDomeShutterOpening(double azimuth, CancellationToken ct)
        {
            var azimuthTolerance = profileService.ActiveProfile.DomeSettings.AzimuthTolerance_degrees;
            var dataPoints = new List<DomeShutterOpeningDataPoint>();
            var dataPoints2 = new List<DomeShutterOpeningDataPoint>();

            if (azimuth - azimuthTolerance < 0.0 || azimuth + azimuthTolerance > 360.0)
            {
                dataPoints.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = 0,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
                dataPoints.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = (azimuth + azimuthTolerance) % 360.0,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
                dataPoints2.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = (azimuth - azimuthTolerance + 360.0) % 360.0,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
                dataPoints2.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = 360.0,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
            }
            else
            {
                dataPoints.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = azimuth - azimuthTolerance,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
                dataPoints.Add(new DomeShutterOpeningDataPoint()
                {
                    Azimuth = azimuth + azimuthTolerance,
                    MinAltitude = 0.0,
                    MaxAltitude = 90.0
                });
            }
            ct.ThrowIfCancellationRequested();
            this.DomeShutterOpeningDataPoints = new AsyncObservableCollection<DomeShutterOpeningDataPoint>(dataPoints);
            this.DomeShutterOpeningDataPoints2 = new AsyncObservableCollection<DomeShutterOpeningDataPoint>(dataPoints2);
        }

        private void CalculateAzimuthAwareDomeShutterOpening(double azimuth, CancellationToken ct)
        {
            var azimuthTolerance = profileService.ActiveProfile.DomeSettings.AzimuthTolerance_degrees;
            var dataPoints1_1 = new List<DomeShutterOpeningDataPoint>();
            var dataPoints1_2 = new List<DomeShutterOpeningDataPoint>();
            var dataPoints2_1 = new List<DomeShutterOpeningDataPoint>();
            var dataPoints2_2 = new List<DomeShutterOpeningDataPoint>();
            var azimuthAngle = Angle.ByDegree(azimuth);
            var domeRadius = this.profileService.ActiveProfile.DomeSettings.DomeRadius_mm;
            if (domeRadius <= 0)
            {
                throw new ArgumentException("Dome Radius is not set in Dome Options");
            }

            const double altitudeDelta = 3.0d;
            for (double altitude = 0.0; altitude <= 90.0; altitude += altitudeDelta)
            {
                ct.ThrowIfCancellationRequested();
                var altitudeAngle = Angle.ByDegree(altitude);
                (var leftAzimuthBoundary, var rightAzimuthBoundary) = DomeUtility.CalculateDomeAzimuthRange(altitudeAngle: altitudeAngle, azimuthAngle: azimuthAngle, domeRadius: domeRadius, domeShutterWidthMm: modelBuilderOptions.DomeShutterWidth_mm);
                if (leftAzimuthBoundary.Degree < 0.0)
                {
                    var addDegrees = AstroUtil.EuclidianModulus(leftAzimuthBoundary.Degree, 360.0d) - leftAzimuthBoundary.Degree;
                    leftAzimuthBoundary += Angle.ByDegree(addDegrees);
                    rightAzimuthBoundary += Angle.ByDegree(addDegrees);
                }

                if (rightAzimuthBoundary.Degree < 360.0)
                {
                    dataPoints1_1.Add(new DomeShutterOpeningDataPoint()
                    {
                        Azimuth = leftAzimuthBoundary.Degree,
                        MinAltitude = altitude,
                        MaxAltitude = 90.0
                    });
                    dataPoints1_2.Add(new DomeShutterOpeningDataPoint()
                    {
                        Azimuth = rightAzimuthBoundary.Degree,
                        MinAltitude = altitude,
                        MaxAltitude = 90.0
                    });
                }
                else
                {
                    if (azimuth > 180.0d)
                    {
                        dataPoints1_1.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = leftAzimuthBoundary.Degree,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints1_2.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = 359.9d,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints2_1.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = 0.0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints2_2.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = rightAzimuthBoundary.Degree - 360.0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                    }
                    else
                    {
                        dataPoints2_1.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = leftAzimuthBoundary.Degree,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints2_2.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = 359.9d,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints1_1.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = 0.0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                        dataPoints1_2.Add(new DomeShutterOpeningDataPoint()
                        {
                            Azimuth = rightAzimuthBoundary.Degree - 360.0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        });
                    }
                }
            }

            ct.ThrowIfCancellationRequested();
            dataPoints1_1.Reverse();
            dataPoints2_1.Reverse();
            this.DomeShutterOpeningDataPoints = new AsyncObservableCollection<DomeShutterOpeningDataPoint>(dataPoints1_1.Concat(dataPoints1_2));
            this.DomeShutterOpeningDataPoints2 = new AsyncObservableCollection<DomeShutterOpeningDataPoint>(dataPoints2_1.Concat(dataPoints2_2));
        }

        /*
         * This block was used to provide dome coverage charts that allowed for infinite range. This should be brought back when the zenith can properly be accounted for
        private void CalculateAzimuthAwareDomeShutterOpening(double azimuth, CancellationToken ct) {
            var dataPoints = new List<DomeShutterOpeningDataPoint>();
            var azimuthAngle = Angle.ByDegree(azimuth);
            var domeRadius = this.profileService.ActiveProfile.DomeSettings.DomeRadius_mm;
            if (domeRadius <= 0) {
                throw new ArgumentException("Dome Radius is not set in Dome Options");
            }

            var fullCoverageReached = false;
            DomeShutterOpeningDataPoint leftLimit = null;
            DomeShutterOpeningDataPoint rightLimit = null;
            const double altitudeDelta = 3.0d;
            for (double altitude = 0.0; altitude <= 90.0; altitude += altitudeDelta) {
                var altitudeAngle = Angle.ByDegree(altitude);
                (var leftAzimuthBoundary, var rightAzimuthBoundary) = DomeUtility.CalculateDomeAzimuthRange(altitudeAngle: altitudeAngle, azimuthAngle: azimuthAngle, domeRadius: domeRadius, domeShutterWidthMm: DomeShutterWidth_mm);
                var apertureAngleThresholdDegree = (azimuthAngle - leftAzimuthBoundary).Degree;

                if (leftAzimuthBoundary.Degree < 0.0 || rightAzimuthBoundary.Degree > 360.0) {
                    double boundaryAltitude;
                    // Interpolate since the target azimuth is beyond the circle boundary
                    if (leftAzimuthBoundary.Degree < 0.0) {
                        boundaryAltitude = altitude - altitudeDelta * (1.0d - azimuthAngle.Degree / apertureAngleThresholdDegree);
                    } else {
                        boundaryAltitude = altitude - altitudeDelta * (1.0d - (360.0d - azimuthAngle.Degree) / apertureAngleThresholdDegree);
                    }

                    if (leftLimit == null || leftLimit.MinAltitude > boundaryAltitude) {
                        leftLimit = new DomeShutterOpeningDataPoint() {
                            Azimuth = 0.0,
                            MinAltitude = boundaryAltitude,
                            MaxAltitude = 90.0
                        };
                    }
                    if (rightLimit == null || rightLimit.MinAltitude > boundaryAltitude) {
                        rightLimit = new DomeShutterOpeningDataPoint() {
                            Azimuth = 360.0,
                            MinAltitude = boundaryAltitude,
                            MaxAltitude = 90.0
                        };
                    }
                }
                if (!double.IsNaN(leftAzimuthBoundary.Degree)) {
                    dataPoints.Add(new DomeShutterOpeningDataPoint() {
                        Azimuth = (leftAzimuthBoundary.Degree + 360.0) % 360.0,
                        MinAltitude = altitude,
                        MaxAltitude = 90.0
                    });
                    dataPoints.Add(new DomeShutterOpeningDataPoint() {
                        Azimuth = rightAzimuthBoundary.Degree % 360.0,
                        MinAltitude = altitude,
                        MaxAltitude = 90.0
                    });
                }

                if (double.IsNaN(leftAzimuthBoundary.Degree) && !fullCoverageReached) {
                    if (leftLimit == null) {
                        leftLimit = new DomeShutterOpeningDataPoint() {
                            Azimuth = 0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        };
                    }
                    if (rightLimit == null) {
                        rightLimit = new DomeShutterOpeningDataPoint() {
                            Azimuth = 360.0,
                            MinAltitude = altitude,
                            MaxAltitude = 90.0
                        };
                    }
                    fullCoverageReached = true;
                }
                ct.ThrowIfCancellationRequested();
            }
            if (leftLimit != null) {
                dataPoints.Add(leftLimit);
            }
            if (rightLimit != null) {
                dataPoints.Add(rightLimit);
            }
            this.DomeShutterOpeningDataPoints = new AsyncObservableCollection<DomeShutterOpeningDataPoint>(dataPoints.OrderBy(dp => dp.Azimuth));
            this.DomeShutterOpeningDataPoints2.Clear();
        }
        */

        private Task<bool> ImportPoints()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a File",
                Filter = "Grid Files|*.grd|All Files|*.*"
            };

            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            openFileDialog.InitialDirectory = Path.Combine(programDataPath, @"ASA\Sequence\Grids");
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                // The user selected a file
                string selectedFileName = openFileDialog.FileName;

                this.ModelPoints.Clear();
                this.DisplayModelPoints.Clear();

                /*
                    1                           Number of points
                    -0.895125152281524          Azimuth (Pi / 180)
                    1.08333591598397            Alitute (Pi / 180)
                    "true"                      isMousePoint
                    "False"                     onlySlew
                    1                           PierSide
                */
                string[] lines = File.ReadAllLines(selectedFileName);
                int numberOfPoints = int.Parse(lines[0], CultureInfo.InvariantCulture);

                var points = new List<ModelPoint>();

                for (int lineNo = 1; lineNo <= numberOfPoints * 5; lineNo += 5)
                {
                    double azimuth = double.Parse(lines[lineNo], CultureInfo.InvariantCulture);
                    double altitude = double.Parse(lines[lineNo + 1], CultureInfo.InvariantCulture);
                    bool onlySlew;
                    bool.TryParse(lines[lineNo + 3].Trim('"'), out onlySlew);

                    double pointAzimuth = (azimuth * (180.0 / Math.PI)) % 360.0;
                    if (pointAzimuth < 0)
                    {
                        pointAzimuth += 360.0;
                    }

                    double pointAltitude = altitude * ((double)180 / Math.PI);

                    //MessageBox.Show($"point Alt {pointAltitude.ToString()}, Az {pointAzimuth.ToString()} onlySlew {onlySlew.ToString()}");

                    if (!onlySlew)
                    {
                        points.Add(
                           new ModelPoint(telescopeMediator)
                           {
                               Altitude = pointAltitude,
                               Azimuth = pointAzimuth,

                               //writer.WriteLine(point.MountReportedSideOfPier == PierSide.pierEast ? "\"1\"" : "\"-1\"");
                               //MountReportedSideOfPier = pierSide == 1 ? PierSide.pierEast : PierSide.pierWest,
                               ModelPointState = ModelPointStateEnum.Generated
                           });
                    }
                }

                this.ModelPoints = ImmutableList.ToImmutableList(points);

                this.DisplayModelPoints = new AsyncObservableCollection<ModelPoint>(points);
            }

            return Task.FromResult(true);
        }

        private Task<bool> ExportPoints()
        {
            SaveFileDialog openFileDialog = new SaveFileDialog
            {
                Title = "Select a File",
                Filter = "Grid Files|*.grd|All Files|*.*"
            };

            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            openFileDialog.InitialDirectory = Path.Combine(programDataPath, @"ASA\Sequence\Grids");
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                // The user selected a file
                string selectedFileName = openFileDialog.FileName;
                var exportedPoints = this.ModelPoints.Where(mp => mp.ModelPointState == ModelPointStateEnum.Generated).ToList();
                using (StreamWriter writer = new StreamWriter(selectedFileName))
                {
                    writer.WriteLine(exportedPoints.Count);

                    foreach (ModelPoint p in exportedPoints)
                    {
                        writer.WriteLine(p.Azimuth * (Math.PI / 180));
                        writer.WriteLine(p.Altitude * (Math.PI / 180));
                        writer.WriteLine("\"false\"");
                        writer.WriteLine("\"False\"");
                        writer.WriteLine(p.Azimuth < 180 ? "0" : "1");
                    }
                }
            }
            return Task.FromResult(true);
        }

        public ModelPointGenerationTypeEnum ModelPointGenerationType
        {
            get => this.modelBuilderOptions.ModelPointGenerationType;
            set
            {
                if (this.modelBuilderOptions.ModelPointGenerationType != value)
                {
                    this.modelBuilderOptions.ModelPointGenerationType = value;
                    if (value == ModelPointGenerationTypeEnum.SiderealPath)
                    {
                        hasValidGeneratedSiderealPath = false;
                    }
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsGoldenSpiralOptionsVisible));
                    RaisePropertyChanged(nameof(IsAutoGridOptionsVisible));
                    RaisePropertyChanged(nameof(IsHighAltitudeOptionsVisible));
                    RaisePropertyChanged(nameof(IsSyncOptionsVisible));
                    RaisePropertyChanged(nameof(IsMlptOptionsVisible));
                    RaisePropertyChanged(nameof(AreMlptErrorChartsVisible));
                    RefreshMlptPlannedImageCount();
                }
            }
        }

        public int GoldenSpiralStarCount
        {
            get => this.modelBuilderOptions.GoldenSpiralStarCount;
            set
            {
                if (this.modelBuilderOptions.GoldenSpiralStarCount != value)
                {
                    this.modelBuilderOptions.GoldenSpiralStarCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double AutoGridRASpacingDegrees
        {
            get => this.modelBuilderOptions.AutoGridRASpacingDegrees;
            set
            {
                if (Math.Abs(this.modelBuilderOptions.AutoGridRASpacingDegrees - value) > double.Epsilon)
                {
                    this.modelBuilderOptions.AutoGridRASpacingDegrees = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double AutoGridDecSpacingDegrees
        {
            get => this.modelBuilderOptions.AutoGridDecSpacingDegrees;
            set
            {
                if (Math.Abs(this.modelBuilderOptions.AutoGridDecSpacingDegrees - value) > double.Epsilon)
                {
                    this.modelBuilderOptions.AutoGridDecSpacingDegrees = value;
                    RaisePropertyChanged();
                }
            }
        }

        public AutoGridInputModeEnum AutoGridInputMode
        {
            get => this.modelBuilderOptions.AutoGridInputMode;
            set
            {
                if (this.modelBuilderOptions.AutoGridInputMode != value)
                {
                    this.modelBuilderOptions.AutoGridInputMode = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsAutoGridDesiredPointsMode));
                    RaisePropertyChanged(nameof(IsAutoGridSpacingMode));
                }
            }
        }

        public bool IsAutoGridDesiredPointsMode => this.modelBuilderOptions.AutoGridInputMode == AutoGridInputModeEnum.DesiredPoints;

        public bool IsAutoGridSpacingMode => !IsAutoGridDesiredPointsMode;

        public bool IsGoldenSpiralOptionsVisible => this.modelBuilderOptions.ModelPointGenerationType == ModelPointGenerationTypeEnum.GoldenSpiral;

        public bool IsAutoGridOptionsVisible => this.modelBuilderOptions.ModelPointGenerationType == ModelPointGenerationTypeEnum.AutoGrid;

        public bool IsHighAltitudeOptionsVisible => this.modelBuilderOptions.ModelPointGenerationType == ModelPointGenerationTypeEnum.GoldenSpiral;

        public bool IsSyncOptionsVisible => this.modelBuilderOptions.ModelPointGenerationType != ModelPointGenerationTypeEnum.SiderealPath;

        public bool IsMlptOptionsVisible => this.modelBuilderOptions.ModelPointGenerationType == ModelPointGenerationTypeEnum.SiderealPath;

        public bool AreMlptErrorChartsVisible => IsMlptOptionsVisible;

        public AutoGridPathOrderingModeEnum AutoGridPathOrderingMode
        {
            get => this.modelBuilderOptions.AutoGridPathOrderingMode;
            set
            {
                if (this.modelBuilderOptions.AutoGridPathOrderingMode != value)
                {
                    this.modelBuilderOptions.AutoGridPathOrderingMode = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool showDisplayPath = false;

        public bool ShowDisplayPath
        {
            get => showDisplayPath;
            set
            {
                if (showDisplayPath != value)
                {
                    showDisplayPath = value;
                    RaisePropertyChanged();
                    RefreshDisplayPathPoints();
                }
            }
        }

        public int AutoGridDesiredPointCount
        {
            get => this.modelBuilderOptions.AutoGridDesiredPointCount;
            set
            {
                if (this.modelBuilderOptions.AutoGridDesiredPointCount != value)
                {
                    this.modelBuilderOptions.AutoGridDesiredPointCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int HighAltitudeStars
        {
            get => this.modelBuilderOptions.HighAltitudeStars;
            set
            {
                if (this.modelBuilderOptions.HighAltitudeStars != value)
                {
                    this.modelBuilderOptions.HighAltitudeStars = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int HighAltitudeMin
        {
            get => this.modelBuilderOptions.HighAltitudeMin;
            set
            {
                if (this.modelBuilderOptions.HighAltitudeMin != value)
                {
                    this.modelBuilderOptions.HighAltitudeMin = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int HighAltitudeMax
        {
            get => this.modelBuilderOptions.HighAltitudeMax;
            set
            {
                if (this.modelBuilderOptions.HighAltitudeMax != value)
                {
                    this.modelBuilderOptions.HighAltitudeMax = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SyncEveryHA
        {
            get => this.modelBuilderOptions.SyncEveryHA;
            set
            {
                if (this.modelBuilderOptions.SyncEveryHA != value)
                {
                    this.modelBuilderOptions.SyncEveryHA = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SyncEastAltitude
        {
            get => this.modelBuilderOptions.SyncEastAltitude;
            set
            {
                if (this.modelBuilderOptions.SyncEastAltitude != value)
                {
                    this.modelBuilderOptions.SyncEastAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SyncWestAltitude
        {
            get => this.modelBuilderOptions.SyncWestAltitude;
            set
            {
                if (this.modelBuilderOptions.SyncWestAltitude != value)
                {
                    this.modelBuilderOptions.SyncWestAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SyncEastAzimuth
        {
            get => this.modelBuilderOptions.SyncEastAzimuth;
            set
            {
                if (this.modelBuilderOptions.SyncEastAzimuth != value)
                {
                    this.modelBuilderOptions.SyncEastAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SyncWestAzimuth
        {
            get => this.modelBuilderOptions.SyncWestAzimuth;
            set
            {
                if (this.modelBuilderOptions.SyncWestAzimuth != value)
                {
                    this.modelBuilderOptions.SyncWestAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double RefEastAltitude
        {
            get => this.modelBuilderOptions.RefEastAltitude;
            set
            {
                if (this.modelBuilderOptions.RefEastAltitude != value)
                {
                    this.modelBuilderOptions.RefEastAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double RefWestAltitude
        {
            get => this.modelBuilderOptions.RefWestAltitude;
            set
            {
                if (this.modelBuilderOptions.RefWestAltitude != value)
                {
                    this.modelBuilderOptions.RefWestAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double RefEastAzimuth
        {
            get => this.modelBuilderOptions.RefEastAzimuth;
            set
            {
                if (this.modelBuilderOptions.RefEastAzimuth != value)
                {
                    this.modelBuilderOptions.RefEastAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double RefWestAzimuth
        {
            get => this.modelBuilderOptions.RefWestAzimuth;
            set
            {
                if (this.modelBuilderOptions.RefWestAzimuth != value)
                {
                    this.modelBuilderOptions.RefWestAzimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseSync
        {
            get => this.modelBuilderOptions.UseSync;
            set
            {
                if (this.modelBuilderOptions.UseSync != value)
                {
                    this.modelBuilderOptions.UseSync = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int SiderealTrackStartOffsetMinutes
        {
            get => this.modelBuilderOptions.SiderealTrackStartOffsetMinutes;
            set
            {
                if (this.modelBuilderOptions.SiderealTrackStartOffsetMinutes != value)
                {
                    this.modelBuilderOptions.SiderealTrackStartOffsetMinutes = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int SiderealTrackEndOffsetMinutes
        {
            get => this.modelBuilderOptions.SiderealTrackEndOffsetMinutes;
            set
            {
                if (this.modelBuilderOptions.SiderealTrackEndOffsetMinutes != value)
                {
                    this.modelBuilderOptions.SiderealTrackEndOffsetMinutes = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double SiderealTrackRADeltaDegrees
        {
            get => this.modelBuilderOptions.SiderealTrackRADeltaDegrees;
            set
            {
                if (this.modelBuilderOptions.SiderealTrackRADeltaDegrees != value)
                {
                    this.modelBuilderOptions.SiderealTrackRADeltaDegrees = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool domeControlEnabled;

        public bool DomeControlEnabled
        {
            get => domeControlEnabled;
            private set
            {
                if (domeControlEnabled != value)
                {
                    domeControlEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        private DataPoint scopePosition;

        public DataPoint ScopePosition
        {
            get => scopePosition;
            set
            {
                scopePosition = value;
                RaisePropertyChanged();
            }
        }

        private bool showNextUpDomePosition = false;

        public bool ShowNextUpDomePosition
        {
            get => showNextUpDomePosition;
            set
            {
                if (showNextUpDomePosition != value)
                {
                    showNextUpDomePosition = value;
                    RaisePropertyChanged();
                }
            }
        }

        private DataPoint nextUpDomePosition;

        public DataPoint NextUpDomePosition
        {
            get => nextUpDomePosition;
            set
            {
                nextUpDomePosition = value;
                RaisePropertyChanged();
            }
        }

        private CustomHorizon customHorizon = EMPTY_HORIZON;

        public CustomHorizon CustomHorizon
        {
            get => customHorizon;
            private set
            {
                if (value == null)
                {
                    customHorizon = EMPTY_HORIZON;
                }
                else
                {
                    customHorizon = value;
                }
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<DataPoint> horizonDataPoints = new AsyncObservableCollection<DataPoint>();

        public AsyncObservableCollection<DataPoint> HorizonDataPoints
        {
            get => horizonDataPoints;
            set
            {
                horizonDataPoints = value;
                RaisePropertyChanged();
            }
        }

        public List<DataPoint> HorizonDataPointsPolar
        {
            get
            {
                return HorizonDataPoints.Select(p =>
                {
                    double radius = p.Y; // Altitude becomes radius
                    double angle = p.X;   // Azimuth is angle
                    double x = radius * Math.Cos(angle * Math.PI / 180);
                    double y = radius * Math.Sin(angle * Math.PI / 180);
                    return new DataPoint(x, y);
                }).ToList();
            }
        }

        private Angle domeShutterAzimuthForOpening;

        private AsyncObservableCollection<DomeShutterOpeningDataPoint> domeShutterOpeningDataPoints = new AsyncObservableCollection<DomeShutterOpeningDataPoint>();

        public AsyncObservableCollection<DomeShutterOpeningDataPoint> DomeShutterOpeningDataPoints
        {
            get => domeShutterOpeningDataPoints;
            set
            {
                domeShutterOpeningDataPoints = value;
                RaisePropertyChanged();
            }
        }

        // If the dome shutter opening wraps around 360, we need a 2nd set of points to render the full dome slit exposure area
        private AsyncObservableCollection<DomeShutterOpeningDataPoint> domeShutterOpeningDataPoints2 = new AsyncObservableCollection<DomeShutterOpeningDataPoint>();

        public AsyncObservableCollection<DomeShutterOpeningDataPoint> DomeShutterOpeningDataPoints2
        {
            get => domeShutterOpeningDataPoints2;
            set
            {
                domeShutterOpeningDataPoints2 = value;
                RaisePropertyChanged();
            }
        }

        private ImmutableList<ModelPoint> modelPoints = ImmutableList.Create<ModelPoint>();

        public ImmutableList<ModelPoint> ModelPoints
        {
            get => modelPoints;
            set
            {
                modelPoints = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<ModelPoint> displayModelPoints = new AsyncObservableCollection<ModelPoint>();

        public AsyncObservableCollection<ModelPoint> DisplayModelPoints
        {
            get => displayModelPoints;
            set
            {
                UnsubscribeDisplayModelPoints(displayModelPoints);
                displayModelPoints = value;
                SubscribeDisplayModelPoints(displayModelPoints);
                RaisePropertyChanged();
                RefreshMlptPlannedImageCount();
                RefreshMlptErrorCharts();
                RefreshDisplayPathPoints();
            }
        }

        private AsyncObservableCollection<DataPoint> displayPathPoints = new AsyncObservableCollection<DataPoint>();

        public AsyncObservableCollection<DataPoint> DisplayPathPoints
        {
            get => displayPathPoints;
            private set
            {
                displayPathPoints = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<DataPoint> displayPathPointsPolar = new AsyncObservableCollection<DataPoint>();

        public AsyncObservableCollection<DataPoint> DisplayPathPointsPolar
        {
            get => displayPathPointsPolar;
            private set
            {
                displayPathPointsPolar = value;
                RaisePropertyChanged();
            }
        }

        private int mlptPlannedImageCount = 1;

        public int MlptPlannedImageCount
        {
            get => mlptPlannedImageCount;
            private set
            {
                if (mlptPlannedImageCount != value)
                {
                    mlptPlannedImageCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        private AsyncObservableCollection<DataPoint> mlptRaErrorPoints = new AsyncObservableCollection<DataPoint>();

        public AsyncObservableCollection<DataPoint> MlptRaErrorPoints
        {
            get => mlptRaErrorPoints;
            private set
            {
                mlptRaErrorPoints = value;
                RaisePropertyChanged();
            }
        }

        private AsyncObservableCollection<DataPoint> mlptDeErrorPoints = new AsyncObservableCollection<DataPoint>();

        public AsyncObservableCollection<DataPoint> MlptDeErrorPoints
        {
            get => mlptDeErrorPoints;
            private set
            {
                mlptDeErrorPoints = value;
                RaisePropertyChanged();
            }
        }

        private double maxMlptRaErrorArcsec;

        public double MaxMlptRaErrorArcsec
        {
            get => maxMlptRaErrorArcsec;
            private set
            {
                if (Math.Abs(maxMlptRaErrorArcsec - value) > double.Epsilon)
                {
                    maxMlptRaErrorArcsec = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double maxMlptDeErrorArcsec;

        public double MaxMlptDeErrorArcsec
        {
            get => maxMlptDeErrorArcsec;
            private set
            {
                if (Math.Abs(maxMlptDeErrorArcsec - value) > double.Epsilon)
                {
                    maxMlptDeErrorArcsec = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double mlptRaErrorAxisLimitArcsec = 1.0;

        public double MlptRaErrorAxisLimitArcsec
        {
            get => mlptRaErrorAxisLimitArcsec;
            private set
            {
                if (Math.Abs(mlptRaErrorAxisLimitArcsec - value) > double.Epsilon)
                {
                    mlptRaErrorAxisLimitArcsec = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(MlptRaErrorAxisMinimumArcsec));
                }
            }
        }

        public double MlptRaErrorAxisMinimumArcsec => -MlptRaErrorAxisLimitArcsec;

        private double mlptDeErrorAxisLimitArcsec = 1.0;

        public double MlptDeErrorAxisLimitArcsec
        {
            get => mlptDeErrorAxisLimitArcsec;
            private set
            {
                if (Math.Abs(mlptDeErrorAxisLimitArcsec - value) > double.Epsilon)
                {
                    mlptDeErrorAxisLimitArcsec = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(MlptDeErrorAxisMinimumArcsec));
                }
            }
        }

        public double MlptDeErrorAxisMinimumArcsec => -MlptDeErrorAxisLimitArcsec;

        public int BuilderNumRetries
        {
            get => this.modelBuilderOptions.BuilderNumRetries;
            set
            {
                if (this.modelBuilderOptions.BuilderNumRetries != value)
                {
                    this.modelBuilderOptions.BuilderNumRetries = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MaxFailedPoints
        {
            get => this.modelBuilderOptions.MaxFailedPoints;
            set
            {
                if (this.modelBuilderOptions.MaxFailedPoints != value)
                {
                    this.modelBuilderOptions.MaxFailedPoints = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double MaxPointRMS
        {
            get => this.modelBuilderOptions.MaxPointRMS;
            set
            {
                if (this.modelBuilderOptions.MaxPointRMS != value)
                {
                    this.modelBuilderOptions.MaxPointRMS = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool buildInProgress;

        public bool BuildInProgress
        {
            get => buildInProgress;
            private set
            {
                if (buildInProgress != value)
                {
                    buildInProgress = value;
                    RaisePropertyChanged();
                }
            }
        }

        private InputCoordinates siderealPathObjectCoordinates;

        public InputCoordinates SiderealPathObjectCoordinates
        {
            get => siderealPathObjectCoordinates;
            private set
            {
                siderealPathObjectCoordinates = value;
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
                if (value != null)
                {
                    modelBuilderOptions.SiderealTrackStartTimeProvider = value.Name;
                }

                selectedSiderealPathStartDateTimeProvider = value;
                if (selectedSiderealPathStartDateTimeProvider != null)
                {
                    RaisePropertyChanged();
                }
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
                if (value != null)
                {
                    modelBuilderOptions.SiderealTrackEndTimeProvider = value.Name;
                }

                selectedSiderealPathEndDateTimeProvider = value;
                if (selectedSiderealPathEndDateTimeProvider != null)
                {
                    RaisePropertyChanged();
                }
            }
        }

        public double GridAltToDegrees(double altitute)
        {
            return altitute * (180 / Math.PI);
        }

        public double GridAzToDegrees(double azimuth)
        {
            azimuth = azimuth * (180 / Math.PI);

            if (azimuth <= 90)
            {
                // North to East
                azimuth = 90 - azimuth;
            }
            else
            {
                // West to South
                azimuth = 270 - azimuth;
            }
            return azimuth;
        }

        public ICommand ClearPointsCommand { get; private set; }
        public ICommand AddHighAltitudeCommand { get; private set; }
        public ICommand GeneratePointsCommand { get; private set; }
        public ICommand BuildCommand { get; private set; }
        public ICommand CancelBuildCommand { get; private set; }
        public ICommand StopBuildCommand { get; private set; }
        public ICommand CoordsFromFramingCommand { get; private set; }
        public ICommand CoordsFromScopeCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
        public ICommand SolveCommand { get; private set; }
    }
}