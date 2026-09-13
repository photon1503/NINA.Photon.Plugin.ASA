#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using System;

namespace NINA.Photon.Plugin.ASA.Model
{
    /// <summary>
    /// Result of the ASCOM "report" action for a single mount axis.
    /// Angular values are reported by the mount in radians.
    /// </summary>
    public class AxisReport
    {
        [JsonProperty("QCurr")]
        public double QCurr { get; set; }

        [JsonProperty("PosErr")]
        public double PosErr { get; set; }

        [JsonProperty("EncPos")]
        public double EncPos { get; set; }

        [JsonProperty("Velocity")]
        public double Velocity { get; set; }

        [JsonProperty("LastTime")]
        public DateTime LastTime { get; set; }

        private const double RadiansToArcsec = 206264.806247096d;
        private const double RadiansToDegrees = 180.0d / Math.PI;

        [JsonIgnore]
        public double PosErrArcsec => PosErr * RadiansToArcsec;

        [JsonIgnore]
        public double EncPosDegrees => EncPos * RadiansToDegrees;

        [JsonIgnore]
        public double VelocityDegreesPerSecond => Velocity * RadiansToDegrees;

        public static AxisReport Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            return JsonConvert.DeserializeObject<AxisReport>(json);
        }
    }
}
