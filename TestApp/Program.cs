#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.DriverAccess;
using Accord.Math;
using NINA.Astrometry;
using NINA.Photon.Plugin.ASA.Equipment;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestApp {

    internal class Program {

        public static Func<object[], T> AnonymousInstantiator<T>(T example) {
            var ctor = typeof(T).GetConstructors().First();
            var paramExpr = Expression.Parameter(typeof(object[]));
            return Expression.Lambda<Func<object[], T>>
            (
                Expression.New
                (
                    ctor,
                    ctor.GetParameters().Select
                    (
                        (x, i) => Expression.Convert
                        (
                            Expression.ArrayIndex(paramExpr, Expression.Constant(i)),
                            x.ParameterType
                        )
                    )
                ), paramExpr).Compile();
        }

        private static void Foo(Type t) {
            var allProperties = t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var allMethods = t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Where(m => !m.IsSpecialName).ToList();
            var allMethodParameters = allMethods.Select(m => m.GetParameters()).ToList();

            var allMethodParametersStrings = allMethodParameters.Select(pl => string.Join(", ", pl.Select(p => $"{p.ParameterType.FullName} {p.Name}"))).ToList();
            Console.WriteLine();
        }

        private static void Main(string[] args) {
            /*
            string id = ASCOM.DriverAccess.Telescope.Choose("");
            if (string.IsNullOrEmpty(id))
                return;
            */

            // create this device
            ASCOM.DriverAccess.Telescope device = new ASCOM.DriverAccess.Telescope("ASCOM.ASA_mount.Telescope");
            device.Connected = true;

            var mountCommander = new AscomMountCommander(device);
            var mount = new Mount(mountCommander);

            var ip = mount.GetIPAddress();
            var mac = mount.GetMACAddress();
            Console.WriteLine();

            /*
            var productFirmware = mount.GetProductFirmware();
            var mountId = mount.GetId();
            var alignmentModelInfo = mount.GetAlignmentModelInfo();
            var declination = mount.GetDeclination();
            var rightAscension = mount.GetRightAscension();
            var sideOfPier = mount.GetSideOfPier();
            var lst = mount.GetLocalSiderealTime();
            var modelCount = mount.GetModelCount();
            var modelNames = Enumerable.Range(1, modelCount).Select(i => mount.GetModelName(i)).ToList();
            var alignmentStarCount = mount.GetAlignmentStarCount();
            var alignmentStars = Enumerable.Range(1, alignmentStarCount).Select(i => mount.GetAlignmentStarInfo(i)).ToList();
            */
            // Make flat plane out of 4 corners, project scope vector onto it, then calculate if the point is within the rectangle
            // https://math.stackexchange.com/questions/100439/determine-where-a-vector-will-intersect-a-plane

            /*
            var domeRadius = 1000.0d;
            var shutterWidth = 600.0d;
            var distancePastZenith = 200.0d;
            var domeAzimuth = Angle.ByDegree(45.0d);
            var scopeAzimuth = Angle.ByDegree(45.0d);
            var scopeAltitude = Angle.ByDegree(0.0d);
            var shutterOpenAngle = ArcAngleFromCartesianDistance(shutterWidth, domeRadius);
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth, scopeAzimuth, scopeAltitude)}");
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth + Angle.ByRadians(Math.PI), scopeAzimuth, scopeAltitude)}");
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth + shutterOpenAngle, scopeAzimuth, scopeAltitude)}");
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth + Angle.ByRadians(shutterOpenAngle.Radians / 1.9d), scopeAzimuth, scopeAltitude)}");
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth + Angle.ByRadians(shutterOpenAngle.Radians / 2.0d), scopeAzimuth, scopeAltitude)}");
            Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth + Angle.ByRadians(shutterOpenAngle.Radians / 2.1d), scopeAzimuth, scopeAltitude)}");
            */
            // Console.WriteLine($"Is in shutter: {IsInShutter(domeRadius, shutterWidth, distancePastZenith, domeAzimuth, scopeAzimuth, scopeAltitude)}");
        }

        public class CartesianCoordinates {
            public double X { get; private set; }
            public double Y { get; private set; }
            public double Z { get; private set; }

            public CartesianCoordinates(double x, double y, double z) {
                this.X = x;
                this.Y = y;
                this.Z = z;
            }

            public double Magnitude {
                get => Math.Sqrt(X * X + Y * Y + Z * Z);
            }

            public SphericalCoordinates ToSpherical() {
                var r = Math.Sqrt(X * X + Y * Y + Z * Z);
                var theta = Angle.ByRadians(Math.Acos(Z / r));
                Angle phi;
                if (X > 0) {
                    phi = Angle.ByRadians(Math.Atan(Y / X));
                } else if (X < 0) {
                    phi = Angle.ByRadians(Math.Atan(Y / X)) + Angle.ByRadians(Math.PI);
                } else {
                    phi = Angle.ByRadians(Math.PI / 2.0d);
                }
                return new SphericalCoordinates(theta, phi, r);
            }

            public CartesianCoordinates Unit {
                get => this / this.Magnitude;
            }

            public CartesianCoordinates CrossProduct(CartesianCoordinates c) {
                var result = new double[3];
                Matrix.Cross(new double[] { X, Y, Z }, new double[] { c.X, c.Y, c.Z }, result);
                return new CartesianCoordinates(result[0], result[1], result[2]);
            }

            public Angle AngleBetween(CartesianCoordinates c) {
                // To get sign information: https://stackoverflow.com/questions/5188561/signed-angle-between-two-3d-vectors-with-same-origin-within-the-same-plane
                var cross = this.CrossProduct(c);
                // var vn = (this - c).Unit;
                return Angle.ByRadians(Math.Atan2(cross * cross.Unit, this * c));
                /*
                // ((Va x Vb) . Vn) / (Va . Vb)

                return Angle.ByRadians(Math.Acos((this * c) / (this.Magnitude * c.Magnitude)));
                // atan2d(x1*y2-y1*x2,x1*x2+y1*y2);
                // return Angle.ByRadians(Math.Atan2())
                */
            }

            public static double operator *(CartesianCoordinates c1, CartesianCoordinates c2) {
                return c1.X * c2.X + c1.Y * c2.Y + c1.Z * c2.Z;
            }

            public static CartesianCoordinates operator *(CartesianCoordinates c, double d) {
                return new CartesianCoordinates(c.X * d, c.Y * d, c.Z * d);
            }

            public static CartesianCoordinates operator /(CartesianCoordinates c, double d) {
                return new CartesianCoordinates(c.X / d, c.Y / d, c.Z / d);
            }

            public static CartesianCoordinates operator +(CartesianCoordinates c1, CartesianCoordinates c2) {
                return new CartesianCoordinates(c1.X + c2.X, c1.Y + c2.Y, c1.Z + c2.Z);
            }

            public static CartesianCoordinates operator -(CartesianCoordinates c1, CartesianCoordinates c2) {
                return new CartesianCoordinates(c1.X - c2.X, c1.Y - c2.Y, c1.Z - c2.Z);
            }

            public double Distance(CartesianCoordinates c) {
                return (this - c).Magnitude;
            }

            public override string ToString() {
                return $"({X}, {Y}, {Z})";
            }
        }

        public class SphericalCoordinates {
            public Angle Azimuth { get; private set; }
            public Angle Altitude { get; private set; }
            public double Radius { get; private set; }

            public SphericalCoordinates(Angle azimuth, Angle altitude, double radius) {
                this.Azimuth = azimuth;
                this.Altitude = altitude;
                this.Radius = radius;
            }

            public CartesianCoordinates ToCartesian() {
                var x = Radius * Math.Cos(Azimuth.Radians) * Math.Sin(Altitude.Radians);
                var y = Radius * Math.Sin(Azimuth.Radians) * Math.Sin(Altitude.Radians);
                var z = Radius * Math.Cos(Altitude.Radians);
                return new CartesianCoordinates(x, y, z);
            }

            public double DotProduct(SphericalCoordinates s) {
                // https://math.stackexchange.com/questions/243142/what-is-the-general-formula-for-calculating-dot-and-cross-products-in-spherical
                // Azimuth = θ
                // Altitude = φ
                // Radius = r
                // r1*r2(sinφ1 * sinφ2 * cos(θ1−θ2) + cosφ1 * cosφ2)
                var part1 = this.Radius * s.Radius;
                var part2 = Math.Sin(this.Altitude.Radians) * Math.Sin(s.Altitude.Radians) * Math.Cos(this.Azimuth.Radians - s.Azimuth.Radians) + Math.Cos(this.Altitude.Radians) * Math.Cos(s.Altitude.Radians);
                return part1 * part2;
            }

            public static SphericalCoordinates operator -(SphericalCoordinates s, SphericalCoordinates t) {
                var cartesianDifference = s.ToCartesian() - t.ToCartesian();
                return cartesianDifference.ToSpherical();
            }

            public override string ToString() {
                return $"({Azimuth}, {Altitude}, {Radius})";
            }
        }

        public class SphericalPlane {
            public SphericalCoordinates Point1 { get; private set; }
            public SphericalCoordinates Point2 { get; private set; }
            public SphericalCoordinates Point3 { get; private set; }
            public SphericalCoordinates Point4 { get; private set; }

            public SphericalPlane(SphericalCoordinates point1, SphericalCoordinates point2, SphericalCoordinates point3, SphericalCoordinates point4) {
                this.Point1 = point1;
                this.Point2 = point2;
                this.Point3 = point3;
                this.Point4 = point4;
            }

            public bool IsInPlane(SphericalCoordinates s) {
                // https://math.stackexchange.com/questions/476608/how-to-check-if-point-is-within-a-rectangle-on-a-plane-in-3d-space
                // If b,c,d,e are a rectangle and a is coplanar with them, you need only check that ⟨b,c−b⟩≤⟨a,c−b⟩≤⟨c,c−b⟩ and ⟨b,e−b⟩≤⟨a,e−b⟩≤⟨e,e−b⟩ (where ⟨,⟩ denotes scalar product).
                /*
                var lowerLimit1 = Point1.DotProduct(Point2 - Point1);
                var value1 = s.DotProduct(Point2 - Point1);
                var upperLimit1 = Point3.DotProduct(Point2 - Point1);

                var lowerLimit2 = Point1.DotProduct(Point4 - Point1);
                var value2 = s.DotProduct(Point4 - Point1);
                var upperLimit2 = Point4.DotProduct(Point4 - Point1);
                return lowerLimit1 <= value1 && value1 <= upperLimit1 && lowerLimit2 <= value2 && value2 <= upperLimit2;
                */
                var (pointLeft, distanceToLeft) = MinDistanceSphereArcAndPoint(Point1, Point3, s);

                var (pointTop, distanceToTop) = MinDistanceSphereArcAndPoint(Point3, Point4, s);
                // var (pointLeft, distanceToLeft) = MinDistanceSphereArcAndPoint(Point1, Point3, s);
                var (pointRight, distanceToRight) = MinDistanceSphereArcAndPoint(Point2, Point4, s);
                var topAngle = pointTop.ToCartesian().AngleBetween(s.ToCartesian());
                var leftAngle = pointLeft.ToCartesian().AngleBetween(s.ToCartesian());
                var rightAngle = pointRight.ToCartesian().AngleBetween(s.ToCartesian());
                /*
                var belowTop = (s - pointTop).Altitude.Degree <= 90.0d && (s - pointTop).Altitude.Degree >= -90.0d;
                var insideLeft = (s - pointLeft).Altitude.Degree >= 180.0d && (s - pointLeft).Altitude.Degree <= 360.0d;
                var insideRight = (s - pointRight).Altitude.Degree <= 180.0d && (s - pointRight).Altitude.Degree >= 0.0d;
                */
                // return belowTop && insideLeft && insideRight;
                return true;
            }
        }

        public static bool IsInShutter(double domeRadius, double shutterWidth, double distancePastZenith, Angle domeAzimuth, Angle scopeAzimuth, Angle scopeAltitude) {
            // This is the angle formed from zenith to the center of the dome shutter at 0 altitude, to the corner of the shutter at 0 altitude
            var angleShutterCornerOpening = Angle.ByRadians(Math.Asin(shutterWidth / (2.0d * domeRadius)));
            // azimuth, 90-altitude, radius - spherical coordinates
            var point1 = new SphericalCoordinates(domeAzimuth + angleShutterCornerOpening, Angle.ByRadians(Math.PI / 2.0d), domeRadius);
            var point2 = new SphericalCoordinates(domeAzimuth - angleShutterCornerOpening, Angle.ByRadians(Math.PI / 2.0d), domeRadius);

            var shutterPastZenithPoint = new SphericalCoordinates(domeAzimuth + Angle.ByRadians(Math.PI), ArcAngleFromArcLength(distancePastZenith, domeRadius), domeRadius);
            var shutterPastZenithPointCartesian = shutterPastZenithPoint.ToCartesian();
            var zenithPointLevelWithShutter = new CartesianCoordinates(0.0d, 0.0d, shutterPastZenithPointCartesian.Z);
            var levelDistanceToShutter = (shutterPastZenithPointCartesian - zenithPointLevelWithShutter).Magnitude;
            var angleShutterCornerEnding = Angle.ByRadians(Math.Atan2(shutterWidth / 2.0d, levelDistanceToShutter));
            double cartesianDistancePastZenith = CartesianDistanceFromArcLength(distancePastZenith, domeRadius);
            double cartesianDistanceToShutterCornerPastZenith = Math.Sqrt(shutterWidth * shutterWidth / 4.0d + cartesianDistancePastZenith * cartesianDistancePastZenith);
            var inverseAltitudeShutterCorner = ArcAngleFromCartesianDistance(cartesianDistanceToShutterCornerPastZenith, domeRadius);
            // var angleShutterCornerEnding = Angle.ByRadians(Math.Asin(shutterWidth / (2.0 * cartesianDistanceToShutterCornerPastZenith)));
            var point3 = new SphericalCoordinates(domeAzimuth - (Angle.ByRadians(Math.PI) - angleShutterCornerEnding), inverseAltitudeShutterCorner, domeRadius);
            var point4 = new SphericalCoordinates(domeAzimuth + (Angle.ByRadians(Math.PI) - angleShutterCornerEnding), inverseAltitudeShutterCorner, domeRadius);
            var plane = new SphericalPlane(point1, point2, point3, point4);
            var scopeCoordinates = new SphericalCoordinates(scopeAzimuth, Angle.ByRadians(Math.PI / 2.0d) - scopeAltitude, domeRadius);
            return plane.IsInPlane(scopeCoordinates);
        }

        public static double CartesianDistanceFromArcLength(double arcLength, double radius) {
            var arcAngle = ArcAngleFromArcLength(arcLength, radius);
            return 2.0d * Math.Sin(arcAngle.Radians / 2.0d) * radius;
        }

        public static Angle ArcAngleFromCartesianDistance(double cartesianDistance, double radius) {
            return Angle.ByRadians(2.0d * Math.Asin(cartesianDistance / (2.0d * radius)));
        }

        public static Angle ArcAngleFromArcLength(double arcLength, double radius) {
            double circumference = 2.0 * Math.PI * radius;
            double circleRatio = arcLength / circumference;
            return Angle.ByRadians(Math.PI * 2.0d * circleRatio);
        }

        public static Vector3 SlerpUnitVectors(Vector3 p0, Vector3 p1, float t) {
            var omega = Math.Acos(Vector3.Dot(p0, p1));
            var d = (float)Math.Sin(omega);
            var s0 = (float)Math.Sin((1f - t) * omega);
            var s1 = (float)Math.Sin(t * omega);
            return (p0 * s0 + p1 * s1) / d;
        }

        public static CartesianCoordinates SlerpUnitVectors(CartesianCoordinates p0, CartesianCoordinates p1, double t) {
            var omega = Math.Acos(p0 * p1);
            var d = Math.Sin(omega);
            var s0 = Math.Sin((1.0d - t) * omega);
            var s1 = Math.Sin(t * omega);
            return (p0 * s0 + p1 * s1) / d;
        }

        public static (SphericalCoordinates, double) MinDistanceSphereArcAndPoint(SphericalCoordinates s1, SphericalCoordinates s2, SphericalCoordinates point) {
            var c1 = s1.ToCartesian();
            var c1u = c1.Unit;
            var c2 = s2.ToCartesian();
            var c2u = c2.Unit;
            var c3 = point.ToCartesian();
            var c3u = c3.Unit;
            CartesianCoordinates minDistancePointUnit = null;
            double minDistance = double.PositiveInfinity;
            for (double t = 0.0d; t <= 1.0d; t += 0.01d) {
                var interpolatedPoint = SlerpUnitVectors(c1u, c2u, t);
                var distance = c3u.Distance(interpolatedPoint);
                if (distance < minDistance) {
                    minDistance = distance;
                    minDistancePointUnit = interpolatedPoint;
                }
            }
            var minDistancePoint = minDistancePointUnit * c2.Magnitude;
            return (minDistancePoint.ToSpherical(), c3.Distance(minDistancePoint));
        }
    }
}