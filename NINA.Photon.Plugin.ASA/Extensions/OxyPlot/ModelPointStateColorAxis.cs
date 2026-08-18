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

namespace NINA.Photon.Plugin.ASA.Extensions.OxyPlot
{
    public class ModelPointStateColorAxis : LinearAxis, IColorAxis
    {
        private const int GeneratedEastPaletteIndex = 1000;
        private const int GeneratedWestPaletteIndex = 1001;

        public OxyColor GetColor(int paletteIndex)
        {
            if (paletteIndex == GeneratedEastPaletteIndex)
            {
                return OxyColors.DeepSkyBlue;
            }

            if (paletteIndex == GeneratedWestPaletteIndex)
            {
                return OxyColors.Orange;
            }

            var modelPointState = (ModelPointStateEnum)paletteIndex;
            switch (modelPointState)
            {
                case ModelPointStateEnum.Generated:
                    return OxyColor.Parse("#6BAED6");   // Light Steel Blue

                case ModelPointStateEnum.BelowHorizon:
                case ModelPointStateEnum.OutsideAltitudeBounds:
                case ModelPointStateEnum.OutsideAzimuthBounds:
                    return OxyColors.Gray;

                case ModelPointStateEnum.Failed:
                case ModelPointStateEnum.FailedRMS:
                    return OxyColor.Parse("#D55E00");   // Orange-Peach

                case ModelPointStateEnum.UpNext:
                    return OxyColor.Parse("#FEE08B");   // Pale Gold

                case ModelPointStateEnum.Exposing:
                    return OxyColor.Parse("#66C2A4");   // Mint Green

                case ModelPointStateEnum.Processing:
                    return OxyColor.Parse("#8C564B");   // Brown

                case ModelPointStateEnum.AddedToModel:
                    return OxyColors.ForestGreen;       // Final success
            }
            return OxyColors.Black;
        }

        public int GetPaletteIndex(double value)
        {
            return (int)value;
        }

        public override void Render(IRenderContext rc, int pass)
        {
            if (this.Position == AxisPosition.None)
            {
                return;
            }
            base.Render(rc, pass);
        }
    }
}