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

namespace NINA.Photon.Plugin.ASA.Converters
{
    /// <summary>
    /// Formats the position error scale of the mount info dock. Zero is shown as "Auto",
    /// any other value as an arcsecond amount. Also parses text typed into the editable
    /// dropdown back into a scale value.
    /// </summary>
    public class ErrorScaleToStringConverter : IValueConverter
    {
        private const string AutoText = "Auto";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scale)
            {
                return scale > 0 ? $"{scale.ToString("0.###", culture)}\u2033" : AutoText;
            }
            return AutoText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return d < 0 ? 0.0d : d;
            }

            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0.0d;
            }

            text = text.Trim().TrimEnd('\u2033', '"', '\'').Trim();
            if (text.Equals(AutoText, StringComparison.OrdinalIgnoreCase))
            {
                return 0.0d;
            }

            if (double.TryParse(text, NumberStyles.Float, culture, out var parsed)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed < 0 ? 0.0d : parsed;
            }

            return Binding.DoNothing;
        }
    }
}
