#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Tools;
using Grpc.Core;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.FansOff
{
    [ExportMetadata("Name", "Weather Update")]
    [ExportMetadata("Description", "Weather update for refraction correction")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TestMe : SequenceItem
    {
        private readonly IWeatherDataMediator weatherDataMediator;

        [ImportingConstructor]
        public TestMe(IWeatherDataMediator weatherDataMediator) : this(weatherDataMediator, ASAPlugin.MountMediator, ASAPlugin.ASAOptions, ASAPlugin.Mount)
        {
        }

        public TestMe(IWeatherDataMediator weatherDataMediator, IMountMediator mountMediator, IASAOptions options, IMount mount)
        {
            this.mountMediator = mountMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.mount = mount;
        }

        private TestMe(TestMe cloneMe) : this(cloneMe.weatherDataMediator, cloneMe.mountMediator, cloneMe.options, cloneMe.mount)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new TestMe(this)
            {
            };
        }

        private IMountMediator mountMediator;
        private IMount mount;

        public ObservableCollection<int> FanSpeedOptions { get; set; }

        private ASAOptions options;
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

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            //var fanSpeed = Utilities.Utilities.ResolveTokens(FanSpeed, this, metadata);

            // send current weather to Autoslew
            double temperature = weatherDataMediator.GetInfo().Temperature;
            double humidity = weatherDataMediator.GetInfo().Humidity;
            double pressure = weatherDataMediator.GetInfo().Pressure;

            try
            {
                if (mount != null)
                {
                    mount.SetTemperature(temperature);
                    mount.SetHumidity(humidity);
                    mount.SetPressure(pressure);
                }
                else
                {
                    Logger.Error("Telescope not connected, cannot send weather data to Autoslew.");
                }

                Logger.Info($"Weather data sent to Autoslew: Temperature={temperature}, Humidity={humidity}, Pressure={pressure}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending weather data to Autoslew: {ex.Message}");
            }
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(TestMe)}";
        }
    }
}