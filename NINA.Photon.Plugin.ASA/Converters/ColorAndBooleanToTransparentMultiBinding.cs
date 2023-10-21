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
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NINA.Photon.Plugin.ASA.Converters {

    public class ColorAndBooleanToTransparentMultiBinding : IMultiValueConverter {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length == 2) {
                if (values[0] != null && values[1] != null && values[0] != DependencyProperty.UnsetValue && values[1] != DependencyProperty.UnsetValue) {
                    var originalColor = (Color)values[0];
                    var enabled = (bool)values[1];
                    if (enabled) {
                        return originalColor;
                    } else {
                        return Colors.Transparent;
                    }
                }
            }

            return Colors.Transparent;
        }

        object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}