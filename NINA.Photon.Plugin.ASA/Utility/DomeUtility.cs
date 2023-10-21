#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry;
using System;

namespace NINA.Photon.Plugin.ASA.Utility {

    public static class DomeUtility {

        public static (Angle, Angle) CalculateDomeAzimuthRange(Angle altitudeAngle, Angle azimuthAngle, double domeRadius, double domeShutterWidthMm) {
            var radiusAtAltitude = Math.Cos(altitudeAngle.Radians) * domeRadius;
            var oppositeOverHypotenuse = 0.5 * domeShutterWidthMm / radiusAtAltitude;
            var apertureThresholdRadians = Math.Asin(oppositeOverHypotenuse);
            Angle apertureThresholdAngle;
            if (double.IsNaN(apertureThresholdRadians)) {
                // Put an upper bound on the amount of aperture covered by a shutter of one quadrant of the dome. This could be improved further by knowing how much zenith clearance the shutter has
                apertureThresholdAngle = Angle.ByDegree(45.0d);
            } else {
                apertureThresholdAngle = Angle.ByRadians(Math.Min(Math.PI / 4.0d, apertureThresholdRadians));
            }
            var leftAzimuthBoundary = azimuthAngle - apertureThresholdAngle;
            var rightAzimuthBoundary = azimuthAngle + apertureThresholdAngle;
            return (leftAzimuthBoundary, rightAzimuthBoundary);
        }
    }
}