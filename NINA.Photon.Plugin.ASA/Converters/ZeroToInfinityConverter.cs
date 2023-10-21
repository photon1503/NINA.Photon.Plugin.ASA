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

    public class ZeroToInfinityConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is int) {
                var d = (int)value;
                if (d <= 0) {
                    return "unlimited";
                }
                return d.ToString();
            } else if (value is double) {
                var d = (double)value;
                if (d <= 0.0d || double.IsNaN(d)) {
                    return "unlimited";
                }
                return d.ToString();
            }
            throw new ArgumentException("Invalid Type for Converter");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string) {
                var s = (string)value;
                if (targetType == typeof(int)) {
                    if (s == "unlimited" || !double.TryParse(s, out var result) || result <= 0.0) {
                        return 0;
                    }
                    return (int)result;
                } else if (targetType == typeof(double)) {
                    if (s == "unlimited" || !double.TryParse(s, out var result) || result <= 0.0) {
                        return 0.0d;
                    }
                    return result;
                }
            }
            throw new ArgumentException("Invalid Type for Converter");
        }
    }
}