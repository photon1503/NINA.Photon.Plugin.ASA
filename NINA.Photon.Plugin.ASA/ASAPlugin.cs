#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Properties;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.ComponentModel.Composition;
using System.Windows.Input;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Equipment.Interfaces;
using NINA.Photon.Plugin.ASA.ModelManagement;
using NINA.PlateSolving.Interfaces;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using NINA.WPF.Base.Mediator;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;

namespace NINA.Photon.Plugin.ASA
{
    /// <summary>
    /// Longer term consideration TODO list:
    ///  1. Split download time from exposure to avoid waiting for download before slewing to the next point
    ///  2. Minimize distance between points instead of going purely based on azimuth
    ///  3. Use AltAz slew on the mount instead of calculating our own refaction-adjusted RA/DEC
    ///  4. Option to save failed points and images used to plate solve
    ///
    /// Short term TODO list:
    ///  1. Plugins to trigger model build
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class ASAPlugin : PluginBase
    {
        [ImportingConstructor]
        public ASAPlugin(
            IProfileService profileService, ITelescopeMediator telescopeMediator, IApplicationStatusMediator applicationStatusMediator, IDomeMediator domeMediator, IDomeSynchronization domeSynchronization,
            IPlateSolverFactory plateSolverFactory, IImagingMediator imagingMediator, IFilterWheelMediator filterWheelMediator, IWeatherDataMediator weatherDataMediator, ICameraMediator cameraMediator, IImageDataFactory imageDataFactory, IGuiderMediator guiderMediator)
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                Settings.Default.Save();
            }

            if (ASAOptions == null)
            {
                ASAOptions = new ASAOptions(profileService);
            }

            ResetModelBuilderDefaultsCommand = new RelayCommand(ASAOptions.ResetDefaults);

            MountCommander = new TelescopeMediatorMountCommander(telescopeMediator, ASAOptions);
            Mount = new Mount(MountCommander);
            MountMediator = new MountMediator();
            MountModelMediator = new MountModelMediator();
            ApplicationStatusMediator = new ApplicationStatusMediator();
            DateTime = new SystemDateTime();
            ModelAccessor = new ModelAccessor(telescopeMediator, MountModelMediator, DateTime);
            ModelPointGenerator = new ModelPointGenerator(profileService, telescopeMediator, weatherDataMediator, ASAOptions, MountMediator, Mount);
            ModelBuilder = new ModelBuilder(profileService, MountModelMediator, Mount, telescopeMediator, domeMediator, cameraMediator, domeSynchronization, plateSolverFactory, imagingMediator, filterWheelMediator, weatherDataMediator, imageDataFactory, guiderMediator, ASAOptions);
            MountModelBuilderMediator = new MountModelBuilderMediator();
        }

        public static ASAOptions ASAOptions { get; private set; }

        public ICommand ResetModelBuilderDefaultsCommand { get; private set; }

        public static IMountCommander MountCommander { get; private set; }

        public static IMount Mount { get; private set; }

        public static IModelAccessor ModelAccessor { get; private set; }

        public static IModelBuilder ModelBuilder { get; private set; }

        public static ICustomDateTime DateTime { get; private set; }

        public static IMountMediator MountMediator { get; private set; }

        public static IMountModelMediator MountModelMediator { get; private set; }

        public static IModelPointGenerator ModelPointGenerator { get; private set; }

        public static IMountModelBuilderMediator MountModelBuilderMediator { get; private set; }

        public static IApplicationStatusMediator ApplicationStatusMediator { get; private set; }
    }
}