#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Globalization;
using System.Windows.Data;

namespace NINA.Photon.Plugin.ASA.Converters {

    public class NumericToHalfConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is int) {
                var d = (int)value;
                return d / 2;
            }
            if (value is long) {
                var d = (long)value;
                return d / 2;
            }
            if (value is float) {
                var d = (float)value;
                return d / 2;
            }
            if (value is double) {
                var d = (double)value;
                return d / 2;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}