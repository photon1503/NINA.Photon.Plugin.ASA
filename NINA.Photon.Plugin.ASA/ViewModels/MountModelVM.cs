﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.ViewModel;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.Immutable;
using NINA.Core.Enum;
using System.Windows;
using System.Linq;
using NINA.Equipment.Interfaces;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using NINA.Photon.Plugin.ASA.Utility;
using System.Net;

namespace NINA.Photon.Plugin.ASA.ViewModels
{
    //[Export(typeof(IDockableVM))]
    public class MountModelVM : DockableVM, IMountModelVM, ITelescopeConsumer, IMountConsumer
    {
        private readonly IMount mount;
        private readonly IMountMediator mountMediator;
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IModelAccessor modelAccessor;
        private IProgress<ApplicationStatus> progress;
        private bool disposed = false;
        private CancellationTokenSource disconnectCts;
        private readonly object alignmentModelLoadLock = new object();

        private readonly SynchronizationContext synchronizationContext =
       Application.Current?.Dispatcher != null
       ? new DispatcherSynchronizationContext(Application.Current.Dispatcher)
       : null;

        [ImportingConstructor]
        public MountModelVM(IProfileService profileService, IApplicationStatusMediator applicationStatusMediator, ITelescopeMediator telescopeMediator) :
            this(profileService,
                ASAPlugin.MountModelMediator,
                telescopeMediator,
                applicationStatusMediator,
                ASAPlugin.Mount,
                ASAPlugin.MountMediator,
                ASAPlugin.ModelAccessor)
        {
        }

        public MountModelVM(
            IProfileService profileService,
            IMountModelMediator mountModelMediator,
            ITelescopeMediator telescopeMediator,
            IApplicationStatusMediator applicationStatusMediator,
            IMount mount,
            IMountMediator mountMediator,
            IModelAccessor modelAccessor) : base(profileService)
        {
            this.Title = "ASA Model";

            var dict = new ResourceDictionary();
            dict.Source = new Uri("NINA.Photon.Plugin.ASA;component/Resources/SVGDataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ASASVG"];
            ImageGeometry.Freeze();

            this.applicationStatusMediator = applicationStatusMediator;
            this.mount = mount;
            this.mountMediator = mountMediator;
            this.telescopeMediator = telescopeMediator;
            this.modelAccessor = modelAccessor;
            this.disconnectCts = new CancellationTokenSource();

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

            this.ModelNames = new AsyncObservableCollection<string>() { GetUnselectedModelName() };
            this.SelectedModelName = GetUnselectedModelName();

            this.RefreshCommand = new AsyncRelayCommand<bool>(async o =>
            {
                await LoadModelNames(this.disconnectCts.Token);
                await LoadAlignmentModel(this.disconnectCts.Token);
            });
            this.DeleteSelectedModelCommand = new AsyncRelayCommand(DeleteSelectedModel);
            this.LoadSelectedModelCommand = new AsyncRelayCommand(LoadSelectedModel);
            this.SaveSelectedModelCommand = new AsyncRelayCommand(SaveSelectedModel);
            this.SaveAsModelCommand = new AsyncRelayCommand(SaveAsModel);
            this.DeleteWorstStarCommand = new AsyncRelayCommand(DeleteWorstStar);

            mountModelMediator.RegisterHandler(this);
            this.telescopeMediator.RegisterConsumer(this);
            this.mountMediator.RegisterConsumer(this);
        }

        public async Task<bool> PowerOn(CancellationToken ct)
        {
            if (MountInfo.Connected)
            {
                return true;
            }

            Task progressDelay = null;
            CancellationTokenSource timeoutCts = null;
            try
            {
                //await MountUtility.WaitUntilResponding(ipAddress, this.Options.Port, linkedCt.Token);
                //TODO MotorOn
                telescopeMediator.Action("MotorOn", "");

                return await telescopeMediator.Connect();
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts?.IsCancellationRequested == true)
                {
                    Notification.ShowError("Timed out waiting for mount to power on");
                    Logger.Error("Timed out waiting for mount to power on");
                }
                else
                {
                    Logger.Info("ASA power on cancelled");
                }
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to power on ASA mount: {e.Message}");
                Logger.Error("Failed to power on ASA mount", e);
            }
            finally
            {
                if (progressDelay != null)
                {
                    await progressDelay;
                }
                this.progress.Report(new ApplicationStatus() { });
            }
            return false;
        }

        private Task<bool> DeleteWorstStar()
        {
            try
            {
                var alignmentStars = new AlignmentStarPoint[this.LoadedAlignmentModel.AlignmentStarCount];
                this.LoadedAlignmentModel.AlignmentStars.CopyTo(alignmentStars, 0);

                var largestError = double.MinValue;
                var worstStarIndex = -1;
                for (int i = 0; i < alignmentStars.Length; ++i)
                {
                    if (alignmentStars[i].ErrorArcsec > largestError)
                    {
                        largestError = alignmentStars[i].ErrorArcsec;
                        worstStarIndex = i;
                    }
                }

                if (worstStarIndex >= 0)
                {
                    var toDelete = alignmentStars[worstStarIndex];
                    Logger.Info($"Deleting alignment star {worstStarIndex + 1}. Alt={toDelete.Altitude:.00}, Az={toDelete.Azimuth:.00}, RMS={toDelete.ErrorArcsec:.00} arcsec");
                    if (!this.DeleteAlignmentStar(worstStarIndex + 1))
                    {
                        Notification.ShowError("Failed to delete worst alignment star");
                        Logger.Error("Failed to delete worst alignment star");
                        return Task.FromResult(false);
                    }

                    ModelLoaded = false;
                    _ = LoadAlignmentModel(this.disconnectCts.Token);
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(false);
                }
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to delete worst alignment star. {e.Message}");
                Logger.Error($"Failed to delete worst alignment star", e);
                return Task.FromResult(false);
            }
        }

        private Task<bool> DeleteSelectedModel()
        {
            try
            {
                var selectedModelName = this.SelectedModelName;
                Logger.Info($"Deleting model {selectedModelName}");
                this.DeleteModel(selectedModelName);
                ModelNamesLoaded = false;
                _ = LoadModelNames(this.disconnectCts.Token);
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to delete {selectedModelName}. {e.Message}");
                Logger.Error($"Failed to delete {selectedModelName}", e);
                return Task.FromResult(false);
            }
        }

        private Task<bool> LoadSelectedModel()
        {
            try
            {
                var selectedModelName = this.SelectedModelName;
                Logger.Info($"Loading model {selectedModelName}");
                ModelLoaded = false;
                this.LoadModel(selectedModelName);
                _ = LoadAlignmentModel(this.disconnectCts.Token);
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to load {selectedModelName}. {e.Message}");
                Logger.Error($"Failed to load {selectedModelName}", e);
                return Task.FromResult(false);
            }
        }

        private Task<bool> SaveSelectedModel()
        {
            try
            {
                var selectedModelName = this.SelectedModelName;
                Logger.Info($"Saving model as {selectedModelName}");
                if (this.SaveModel(selectedModelName))
                {
                    Notification.ShowInformation($"Saved {selectedModelName}");
                    return Task.FromResult(true);
                }
                Notification.ShowError("Failed to save model");
                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to save {selectedModelName}. {e.Message}");
                Logger.Error($"Failed to save {selectedModelName}", e);
                return Task.FromResult(false);
            }
        }

        private Task<bool> SaveAsModel()
        {
            try
            {
                var result = MyInputBox.Show("Save Model As...", "Model Name", "What name to save the active model with");
                if (result.MessageBoxResult == System.Windows.MessageBoxResult.OK)
                {
                    var inputModelName = result.InputText;
                    Logger.Info($"Saving model as {inputModelName}");
                    if (this.SaveModel(inputModelName))
                    {
                        Notification.ShowInformation($"Saved {inputModelName}");
                        ModelNamesLoaded = false;
                        _ = LoadModelNames(this.disconnectCts.Token);
                        return Task.FromResult(true);
                    }
                    Notification.ShowError("Failed to save model");
                    return Task.FromResult(false);
                }
                else
                {
                    Logger.Info("Save cancelled by user");
                    return Task.FromResult(false);
                }
            }
            catch (Exception e)
            {
                Notification.ShowError($"Failed to save {selectedModelName}. {e.Message}");
                Logger.Error($"Failed to save {selectedModelName}", e);
                return Task.FromResult(false);
            }
        }

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
        }

        public void UpdateDeviceInfo(MountInfo deviceInfo)
        {
            this.MountInfo = deviceInfo;
            if (this.MountInfo.Connected)
            {
                DoConnect();
            }
            else
            {
                DoDisconnect();
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

        private TelescopeInfo telescopeInfo = DeviceInfo.CreateDefaultInstance<TelescopeInfo>();

        public TelescopeInfo TelescopeInfo
        {
            get => telescopeInfo;
            private set
            {
                telescopeInfo = value;
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

        private readonly LoadedAlignmentModel loadedAlignmentModel = new LoadedAlignmentModel();

        public LoadedAlignmentModel LoadedAlignmentModel => loadedAlignmentModel;

        private void DoConnect()
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

            this.disconnectCts?.Cancel();
            this.disconnectCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                await LoadModelNames(this.disconnectCts.Token);
                await LoadAlignmentModel(this.disconnectCts.Token);
            }, this.disconnectCts.Token);

            this.progress.Report(new ApplicationStatus() { });
            Connected = true;
        }

        private void DoDisconnect()
        {
            if (!Connected)
            {
                return;
            }

            this.disconnectCts?.Cancel();
            LoadedAlignmentModel.Clear();
            Connected = false;
        }

        private volatile Task alignmentModelLoadTask;

        private async Task LoadAlignmentModel(CancellationToken ct)
        {
            Task loadTask = null;
            lock (alignmentModelLoadLock)
            {
                loadTask = alignmentModelLoadTask;
                if (loadTask == null)
                {
                    loadTask = Task.Run(() =>
                    {
                        try
                        {
                            ModelLoaded = false;
                            modelAccessor.LoadActiveModelInto(LoadedAlignmentModel, progress: this.progress, ct: ct);
                            if (LoadedAlignmentModel.AlignmentStarCount <= 0)
                            {
                                Notification.ShowWarning("No alignment stars in loaded model");
                                Logger.Warning("No alignment stars in loaded model");
                            }
                            else
                            {
                                ModelLoaded = true;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Notification.ShowError("Failed to get ASA alignment model");
                            Logger.Error("Failed to get alignment model", ex);
                        }
                    }, ct);
                    this.alignmentModelLoadTask = loadTask;
                }
            }

            await loadTask;
            lock (alignmentModelLoadLock)
            {
                this.alignmentModelLoadTask = null;
            }
        }

        public async Task<LoadedAlignmentModel> GetLoadedAlignmentModel(CancellationToken ct)
        {
            var localAlignmentModelLoadTask = alignmentModelLoadTask;
            if (localAlignmentModelLoadTask == null)
            {
                return LoadedAlignmentModel.Clone();
            }

            var tcs = new TaskCompletionSource<object>();
            using (ct.Register(() => tcs.SetCanceled()))
            {
                _ = await Task.WhenAny(localAlignmentModelLoadTask, tcs.Task);
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                return LoadedAlignmentModel.Clone();
            }
        }

        private Task alignmentModelNameLoadTask;

        private async Task LoadModelNames(CancellationToken ct)
        {
            var localTask = alignmentModelNameLoadTask;
            if (localTask != null)
            {
                await localTask;
                return;
            }

            this.alignmentModelNameLoadTask = Task.Run(() =>
            {
                bool succeeded = false;
                try
                {
                    ModelNamesLoaded = false;
                    //var modelCount = this.GetModelCount();
                    ct.ThrowIfCancellationRequested();
                    this.ModelNames.Clear();
                    this.ModelNames.Add(GetUnselectedModelName());
                    for (int i = 1; i <= 1; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        //        this.ModelNames.Add(this.GetModelName(i));
                    }
                    succeeded = true;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    Notification.ShowError($"Failed to load ASA models. {e.Message}");
                }
                finally
                {
                    if (!succeeded)
                    {
                        this.ModelNames.Clear();
                        this.ModelNames.Add(GetUnselectedModelName());
                    }
                    this.SelectedModelName = GetUnselectedModelName();
                    ModelNamesLoaded = true;
                }
            }, ct);

            try
            {
                await this.alignmentModelNameLoadTask;
            }
            finally
            {
                this.alignmentModelNameLoadTask = null;
            }
        }

        private static string GetUnselectedModelName()
        {
            return "- Select Model -";
        }

        private AsyncObservableCollection<string> modelNames;

        public AsyncObservableCollection<string> ModelNames
        {
            get => modelNames;
            set
            {
                modelNames = value;
                RaisePropertyChanged();
                SelectedModelIndex = 0;
            }
        }

        private int selectedModelIndex;

        public int SelectedModelIndex
        {
            get => selectedModelIndex;
            set
            {
                if (selectedModelIndex != value)
                {
                    selectedModelIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string selectedModelName;

        public string SelectedModelName
        {
            get => selectedModelName;
            set
            {
                if (selectedModelName != value)
                {
                    selectedModelName = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool modelLoaded = false;

        public bool ModelLoaded
        {
            get => modelLoaded;
            private set
            {
                if (modelLoaded != value)
                {
                    modelLoaded = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool modelNamesLoaded = false;

        public bool ModelNamesLoaded
        {
            get => modelNamesLoaded;
            private set
            {
                if (modelNamesLoaded != value)
                {
                    modelNamesLoaded = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand DeleteSelectedModelCommand { get; private set; }
        public ICommand LoadSelectedModelCommand { get; private set; }
        public ICommand SaveSelectedModelCommand { get; private set; }
        public ICommand SaveAsModelCommand { get; private set; }
        public ICommand DeleteWorstStarCommand { get; private set; }
        public ICommand ClearAlignmentCommand { get; private set; }

        public Task<IList<string>> Rescan()
        {
            throw new NotImplementedException();
        }

        public Task<bool> Connect()
        {
            throw new NotImplementedException();
        }

        public Task Disconnect()
        {
            throw new NotImplementedException();
        }

        public MountModelInfo GetDeviceInfo()
        {
            return new MountModelInfo()
            {
                Connected = Connected,
                LoadedAlignmentModel = LoadedAlignmentModel,
                ModelNames = ImmutableList.ToImmutableList(ModelNames)
            };
        }

        public string[] GetModelNames()
        {
            if (Connected)
            {
                return this.ModelNames.ToArray();
            }
            return new string[0];
        }

        public bool LoadModel(string name)
        {
            if (Connected)
            {
                if (mount.LoadModel(name))
                {
                    _ = LoadAlignmentModel(disconnectCts.Token);
                    return true;
                }
                return false;
            }
            return false;
        }

        public bool SaveModel(string name)
        {
            if (Connected)
            {
                if (this.DeleteModel(selectedModelName))
                {
                    Logger.Info($"Deleted existing model {name} prior to saving");
                }
                return mount.SaveModel(name);
            }
            return false;
        }

        public bool DeleteModel(string name)
        {
            if (Connected)
            {
                return mount.DeleteModel(name);
            }
            return false;
        }

        public bool StartNewAlignmentSpec()
        {
            if (Connected)
            {
                return mount.StartNewAlignmentSpec();
            }
            return false;
        }

        public bool FinishAlignmentSpec()
        {
            if (Connected)
            {
                if (mount.FinishAlignmentSpec())
                {
                    _ = LoadAlignmentModel(disconnectCts.Token);
                    return true;
                }
                return false;
            }
            return false;
        }

        public bool DeleteAlignmentStar(int alignmentStarIndex)
        {
            if (Connected)
            {
                return mount.DeleteAlignmentStar(alignmentStarIndex);
            }
            return false;
        }

        public int AddAlignmentStar(
            double mountRightAscension,
            double mountDeclination,
            PierSide sideOfPier,
            double plateSolvedRightAscension,
            double plateSolvedDeclination,
            double localSiderealTime)
        {
            if (Connected)
            {
                //return mount.AddAlignmentPointToSpec(mountRightAscension, mountDeclination, sideOfPier, plateSolvedRightAscension, plateSolvedDeclination, localSiderealTime);

                //TODO Add to list
                Notification.ShowInformation("Adding point");
                return 1;
            }
            return 0;
        }

        public string Action(string actionName, string actionParameters)
        {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true)
        {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true)
        {
            throw new NotImplementedException();
        }

        public void SendCommandBlind(string command, bool raw = true)
        {
            throw new NotImplementedException();
        }

        public IDevice GetDevice()
        {
            throw new NotImplementedException();
        }
    }
}