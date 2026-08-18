#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Model;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NINA.Photon.Plugin.ASA.Converters
{
    public class ModelPointStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ModelPointStateEnum)
            {
                var s = (ModelPointStateEnum)value;
                switch (s)
                {
                    case ModelPointStateEnum.Generated:
                        return (Color)ColorConverter.ConvertFromString("#6BAED6");

                    case ModelPointStateEnum.Failed:
                        return (Color)ColorConverter.ConvertFromString("#D55E00");

                    case ModelPointStateEnum.UpNext:
                        return (Color)ColorConverter.ConvertFromString("#FEE08B");

                    case ModelPointStateEnum.Exposing:
                        return (Color)ColorConverter.ConvertFromString("#66C2A4");

                    case ModelPointStateEnum.Processing:
                        return (Color)ColorConverter.ConvertFromString("#8C564B");
                }
            }
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}