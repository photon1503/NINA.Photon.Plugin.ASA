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
    [ExportMetadata("Name", "Weather Update After Time")]
    [ExportMetadata("Description", "Weather update for refraction correction after x Minutes")]
    [ExportMetadata("Icon", "ASASVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class WeatherUpdateAfterTime : SequenceTrigger, IValidatable
    {
        private IASAOptions options;
        private readonly IMountMediator mountMediator;
        private IMount mount;
        private readonly IMountModelBuilderMediator mountModelBuilderMediator;
        private readonly IModelPointGenerator modelPointGenerator;
        private readonly INighttimeCalculator nighttimeCalculator;

        private readonly ITelescopeMediator telescopeMediator;
        private readonly IWeatherDataMediator weatherDataMediator;

        private DateTime initialTime;
        private bool firstRun = true;
        private bool initialized = false;

        [ImportingConstructor]
        public WeatherUpdateAfterTime(INighttimeCalculator nighttimeCalculator, ITelescopeMediator telescopeMediator, IWeatherDataMediator weatherDataMediator) :
            this(ASAPlugin.ASAOptions, ASAPlugin.MountMediator, ASAPlugin.Mount,
                ASAPlugin.MountModelBuilderMediator, ASAPlugin.ModelPointGenerator,
                nighttimeCalculator, telescopeMediator, weatherDataMediator)
        {
        }

        public WeatherUpdateAfterTime(IASAOptions options, IMountMediator mountMediator, IMount mount,
            IMountModelBuilderMediator mountModelBuilderMediator, IModelPointGenerator modelPointGenerator,
            INighttimeCalculator nighttimeCalculator, ITelescopeMediator telescopeMediator, IWeatherDataMediator weatherDataMediator)
        {
            this.options = options;
            this.mount = mount;
            this.mountMediator = mountMediator;
            this.nighttimeCalculator = nighttimeCalculator;
            this.telescopeMediator = telescopeMediator;
            this.weatherDataMediator = weatherDataMediator;

            Amount = 15;
        }

        private WeatherUpdateAfterTime(WeatherUpdateAfterTime cloneMe) : this(cloneMe.nighttimeCalculator, cloneMe.telescopeMediator, cloneMe.weatherDataMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            var cloned = new WeatherUpdateAfterTime(this)
            {
                // Copy all scalar properties

                Inherited = Inherited,

                //   WeatherDataMediator = weatherDataMediator,
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

        public override void Initialize()
        {
            initialTime = DateTime.Now;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            // send current weather to Autoslew
            double temperature = weatherDataMediator.GetInfo().Temperature;
            double humidity = weatherDataMediator.GetInfo().Humidity;
            double pressure = weatherDataMediator.GetInfo().Pressure;

            try
            {
                if (telescopeMediator != null && telescopeMediator.GetDevice() != null && telescopeMediator.GetDevice().Connected)
                {
                    mount.SetTemperature(temperature);
                    mount.SetHumidity(humidity);
                    mount.SetPressure(pressure);
                }
                else
                {
                    issues.Add("Telescope not connected, cannot send weather data to Autoslew.");
                }

                Logger.Info($"Weather data sent to Autoslew: Temperature={temperature}, Humidity={humidity}, Pressure={pressure}");
            }
            catch (Exception ex)
            {
                issues.Add($"Error sending weather data to Autoslew: {ex.Message}");
            }

            initialTime = DateTime.Now;
        }

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

        public override void SequenceBlockInitialize()
        {
            initialTime = DateTime.Now;

            if (!initialized)
            {
                initialized = true;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            Elapsed = Math.Round((DateTime.Now - initialTime).TotalMinutes, 2);
            bool timeConditionMet = Elapsed >= Amount;

            if (firstRun)
            {
                firstRun = false;
                timeConditionMet = true; // Force trigger on first run
            }

            return timeConditionMet;
        }

        public bool Validate()
        {
            var i = new List<string>();

            if (telescopeMediator != null && telescopeMediator.GetDevice() != null && telescopeMediator.GetDevice().Connected)
            {
                try
                {
                    var version = mount.AutoslewVersion();

                    if (VersionHelper.IsOlderVersion(version, "6.0"))
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
            Validate();
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(WeatherUpdateAfterTime)}";
        }
    }
}