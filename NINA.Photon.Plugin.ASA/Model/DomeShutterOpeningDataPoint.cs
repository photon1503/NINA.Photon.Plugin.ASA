#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;

namespace NINA.Photon.Plugin.ASA.Model {

    public class DomeShutterOpeningDataPoint : BaseINPC {
        private double azimuth;

        public double Azimuth {
            get => azimuth;
            set {
                if (azimuth != value) {
                    azimuth = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double maxAltitude;

        public double MaxAltitude {
            get => maxAltitude;
            set {
                if (maxAltitude != value) {
                    maxAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double minAltitude;

        public double MinAltitude {
            get => minAltitude;
            set {
                if (minAltitude != value) {
                    minAltitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        public override string ToString() {
            return $"{Azimuth} => [{MinAltitude}, {MaxAltitude}]";
        }
    }
}