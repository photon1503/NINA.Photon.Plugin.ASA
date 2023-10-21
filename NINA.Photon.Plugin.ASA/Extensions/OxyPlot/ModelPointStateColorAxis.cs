#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Model;
using OxyPlot;
using OxyPlot.Axes;

namespace NINA.Photon.Plugin.ASA.Extensions.OxyPlot {

    public class ModelPointStateColorAxis : LinearAxis, IColorAxis {

        public OxyColor GetColor(int paletteIndex) {
            var modelPointState = (ModelPointStateEnum)paletteIndex;
            switch (modelPointState) {
                case ModelPointStateEnum.Generated:
                    return OxyColors.LightGreen;

                case ModelPointStateEnum.BelowHorizon:
                case ModelPointStateEnum.OutsideAltitudeBounds:
                case ModelPointStateEnum.OutsideAzimuthBounds:
                    return OxyColors.Brown;

                case ModelPointStateEnum.Failed:
                case ModelPointStateEnum.FailedRMS:
                    return OxyColors.Red;

                case ModelPointStateEnum.UpNext:
                    return OxyColors.Yellow;

                case ModelPointStateEnum.Exposing:
                    return OxyColors.LightBlue;

                case ModelPointStateEnum.Processing:
                    return OxyColors.Blue;

                case ModelPointStateEnum.AddedToModel:
                    return OxyColors.ForestGreen;
            }
            return OxyColors.Black;
        }

        public int GetPaletteIndex(double value) {
            return (int)value;
        }

        public override void Render(IRenderContext rc, int pass) {
            if (this.Position == AxisPosition.None) {
                return;
            }
            base.Render(rc, pass);
        }
    }
}