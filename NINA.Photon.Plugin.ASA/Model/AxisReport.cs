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
    /// Angular values are reported by the mount in degrees (PosErr, EncPos) and degrees per
    /// second (Velocity), matching the units the ASA driver itself uses.
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

        private const double DegreesToArcsec = 3600.0d;

        [JsonIgnore]
        public double PosErrArcsec => PosErr * DegreesToArcsec;

        [JsonIgnore]
        public double EncPosDegrees => EncPos;

        [JsonIgnore]
        public double VelocityDegreesPerSecond => Velocity;

        /// <summary>
        /// Axis velocity in arcseconds per second. Sidereal rate is about 15.04.
        /// </summary>
        [JsonIgnore]
        public double VelocityArcsecPerSecond => Velocity * DegreesToArcsec;

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
