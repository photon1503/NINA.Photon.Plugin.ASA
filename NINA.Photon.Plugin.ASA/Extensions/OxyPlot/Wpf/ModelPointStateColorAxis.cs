#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using OxyPlot.Wpf;

namespace NINA.Photon.Plugin.ASA.Extensions.OxyPlot.Wpf {

    public class ModelPointStateColorAxis : Axis {

        public ModelPointStateColorAxis() {
            this.InternalAxis = new OxyPlot.ModelPointStateColorAxis();
        }

        public override global::OxyPlot.Axes.Axis CreateModel() {
            this.SynchronizeProperties();
            return this.InternalAxis;
        }
    }
}