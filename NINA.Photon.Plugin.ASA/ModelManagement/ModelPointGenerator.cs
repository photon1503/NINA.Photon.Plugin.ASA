#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Statistics;
using Accord.Statistics.Distributions.Univariate;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Photon.Plugin.ASA.ModelManagement
{
    public class ModelPointGenerator : IModelPointGenerator
    {
        public const int MAX_POINTS = 1000;

        // Epsilon to optimize average nearest neighbor distance
        private const double EPSILON = 0.36d;

        private static readonly double GOLDEN_RATIO = (1.0d + Math.Sqrt(5d)) / 2.0d;

        private readonly IProfileService profileService;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IASAOptions options;
        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly IMountMediator mountMediator;
        private readonly IMount mount;

        private const double AUTO_GRID_RING_SCALE_EXPONENT = 0.82d;
        private const double AUTO_GRID_RING_PHASE_STEP_DEGREES = 12.0d;

        public ModelPointGenerator(IProfileService profileService, ITelescopeMediator telescopeMediator, IWeatherDataMediator weatherDataMediator, IASAOptions options, IMountMediator mountMediator, IMount mount)
        {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.options = options;
            this.mountMediator = mountMediator;
            this.mount = mount;
        }

        public List<ModelPoint> GenerateGoldenSpiral(int numPoints, CustomHorizon horizon)
        {
            if (numPoints > MAX_POINTS)
            {
                throw new Exception($"ASA mounts do not support more than {MAX_POINTS} points");
            }
            else if (numPoints < 3)
            {
                throw new Exception("At least 3 points required for a viable model");
            }

            // http://extremelearning.com.au/how-to-evenly-distribute-points-on-a-sphere-more-effectively-than-the-canonical-fibonacci-lattice/
            var points = new List<ModelPoint>();

            int minViableNumPoints = 0;
            int maxViableNumPoints = int.MaxValue;
            int currentNumPoints = numPoints;
            while (true)
            {
                points.Clear();
                int validPoints = 0;
                for (int i = 0; i < currentNumPoints; ++i)
                {
                    // (azimuth) theta = 2 * pi * i / goldenRatio
                    // (altitude) phi = arccos(1 - 2 * (i + epsilon) / (n - 1 + 2 * epsilon))
                    var azimuth = Angle.ByRadians(2.0d * Math.PI * i / GOLDEN_RATIO);
                    // currentNumPoints * 2 enables us to process only half of the sphere
                    var inverseAltitude = Angle.ByRadians(Math.Acos(1.0d - 2.0d * ((double)i + EPSILON) / ((currentNumPoints * 2) - 1.0d + 2.0d * EPSILON)));
                    // The golden spiral algorithm uses theta from 0 - 180, where theta 0 is zenith
                    var altitudeDegrees = 90.0d - AstroUtil.EuclidianModulus(inverseAltitude.Degree, 180.0);
                    if (altitudeDegrees < 0.0d || double.IsNaN(altitudeDegrees))
                    {
                        continue;
                    }

                    var azimuthDegrees = AstroUtil.EuclidianModulus(azimuth.Degree, 360.0);
                    if (altitudeDegrees < 0.1d)
                    {
                        altitudeDegrees = 0.1d;
                    }
                    if (altitudeDegrees > 89.9)
                    {
                        altitudeDegrees = 89.9;
                    }

                    var horizonAltitude = horizon.GetAltitude(azimuthDegrees);
                    var creationState = DeterminePointState(altitudeDegrees, azimuthDegrees, horizonAltitude, applyAzimuthBounds: true);
                    if (creationState == ModelPointStateEnum.Generated)
                    {
                        ++validPoints;
                    }
                    points.Add(
                        new ModelPoint(telescopeMediator)
                        {
                            AutoGridBandIndex = i,
                            Altitude = altitudeDegrees,
                            Azimuth = azimuthDegrees,
                            ModelPointState = creationState
                        });
                }

                if (validPoints == numPoints)
                {
                    return points;
                }
                else if (validPoints < numPoints)
                {
                    // After excluding points below the horizon, we are short. Remember where we currently are, and try more points in another iteration.
                    // This may take several iterations, but it is guaranteed to converge
                    minViableNumPoints = currentNumPoints;
                    var nextNumPoints = Math.Min(maxViableNumPoints, currentNumPoints + (numPoints - validPoints));
                    if (nextNumPoints == currentNumPoints)
                    {
                        if (validPoints < numPoints)
                        {
                            Notification.ShowInformation($"Only {validPoints} could be generated. Continuing");
                            Logger.Warning($"Only {validPoints} could be generated. Continuing");
                        }
                        return points;
                    }
                    currentNumPoints = nextNumPoints;
                }
                else
                {
                    // After excluding points below the horizon, we still have too many.
                    maxViableNumPoints = currentNumPoints - 1;
                    var nextNumPoints = Math.Max(minViableNumPoints + 1, currentNumPoints - (validPoints - numPoints));
                    if (nextNumPoints == currentNumPoints)
                    {
                        // Next run will be the last
                        currentNumPoints = nextNumPoints - 1;
                    }
                    else
                    {
                        currentNumPoints = nextNumPoints;
                    }
                }
            }
        }

        public List<ModelPoint> GenerateAutoGrid(double raSpacingDegrees, double decSpacingDegrees, CustomHorizon horizon)
        {
            if (raSpacingDegrees <= 0.0d || raSpacingDegrees > 360.0d)
            {
                throw new Exception("RA spacing must be between 0 and 360 degrees");
            }
            if (decSpacingDegrees <= 0.0d || decSpacingDegrees > 180.0d)
            {
                throw new Exception("Dec spacing must be between 0 and 180 degrees");
            }

            var ringDistances = BuildPolarRingDistances(decSpacingDegrees);
            var estimatedPoints = 0;
            for (var i = 0; i < ringDistances.Count; i++)
            {
                estimatedPoints += GetAutoGridRaValuesForRing(ringDistances[i], raSpacingDegrees, i).Count;
            }
            if (estimatedPoints > MAX_POINTS)
            {
                throw new Exception($"AutoGrid spacing produces {estimatedPoints} points, exceeding ASA limit of {MAX_POINTS}. Increase spacing.");
            }

            var points = new List<ModelPoint>(estimatedPoints);
            var latitudeDegrees = profileService.ActiveProfile.AstrometrySettings.Latitude;
            var isNorthernHemisphere = latitudeDegrees >= 0.0d;

            for (var i = 0; i < ringDistances.Count; i++)
            {
                var ringDistanceDegrees = ringDistances[i];
                var declinationDegrees = isNorthernHemisphere
                    ? 90.0d - ringDistanceDegrees
                    : -90.0d + ringDistanceDegrees;

                var localHourAngles = GetAutoGridRaValuesForRing(ringDistanceDegrees, raSpacingDegrees, i);
                var ringPoints = new List<ModelPoint>(localHourAngles.Count);
                var ringPointCount = localHourAngles.Count;
                for (var sequenceIndex = 0; sequenceIndex < localHourAngles.Count; sequenceIndex++)
                {
                    var hourAngleDegrees = localHourAngles[sequenceIndex];
                    var destination = ToHorizontalFromDeclinationHourAngle(latitudeDegrees, declinationDegrees, hourAngleDegrees);
                    var azimuthDegrees = AstroUtil.EuclidianModulus(540.0d - destination.azimuthDegrees, 360.0d);
                    var altitudeDegrees = destination.altitudeDegrees;

                    var horizonAltitude = horizon.GetAltitude(azimuthDegrees);
                    var creationState = DeterminePointState(altitudeDegrees, azimuthDegrees, horizonAltitude, applyAzimuthBounds: false);

                    ringPoints.Add(
                        new ModelPoint(telescopeMediator)
                        {
                            AutoGridBandIndex = i,
                            AutoGridBandSequence = sequenceIndex,
                            AutoGridBandPointCount = ringPointCount,
                            Altitude = altitudeDegrees,
                            Azimuth = azimuthDegrees,
                            ModelPointState = creationState
                        });
                }

                var eastToWestOrdered = ringPoints
                    .OrderBy(p => ClockwiseDistanceDegrees(90.0d, p.Azimuth))
                    .ThenByDescending(p => p.Altitude)
                    .ToList();

                for (var eastToWestIndex = 0; eastToWestIndex < eastToWestOrdered.Count; eastToWestIndex++)
                {
                    eastToWestOrdered[eastToWestIndex].AutoGridBandEastToWestOrder = eastToWestIndex;
                }

                points.AddRange(ringPoints);
            }

            var radialBandWidthDegrees = Math.Max(1.0d, decSpacingDegrees);
            var radialBands = points
                .GroupBy(p => (int)Math.Floor((90.0d - p.Altitude) / radialBandWidthDegrees + 1e-9d))
                .ToList();

            foreach (var radialBand in radialBands)
            {
                var radialBandIndex = radialBand.Key;
                var orderedEastToWest = radialBand
                    .OrderBy(p => CounterClockwiseDistanceDegrees(90.0d, p.Azimuth))
                    .ThenByDescending(p => p.Altitude)
                    .ToList();

                for (var eastToWestIndex = 0; eastToWestIndex < orderedEastToWest.Count; eastToWestIndex++)
                {
                    var point = orderedEastToWest[eastToWestIndex];
                    point.AutoGridRadialBandIndex = radialBandIndex;
                    point.AutoGridRadialBandEastToWestOrder = eastToWestIndex;
                }
            }

            return points;
        }

        public List<ModelPoint> GenerateAutoGridByPointCount(int desiredPointCount, CustomHorizon horizon)
        {
            if (desiredPointCount > MAX_POINTS)
            {
                throw new Exception($"ASA mounts do not support more than {MAX_POINTS} points");
            }
            if (desiredPointCount < 3)
            {
                throw new Exception("At least 3 points required for a viable model");
            }

            const double HEMISPHERE_SURFACE_DEGREES2 = 20626.48062470965d;
            var guessSpacing = Math.Sqrt(HEMISPHERE_SURFACE_DEGREES2 / desiredPointCount);
            guessSpacing = Math.Max(0.5d, Math.Min(90.0d, guessSpacing));

            var bestSpacing = guessSpacing;
            var maxGenerated = 0;
            var candidates = new List<(double spacing, int generated, int difference, int stabilityPenalty)>();

            void EvaluateScalar(double spacing)
            {
                if (spacing <= 0.0d || spacing > 90.0d)
                {
                    return;
                }

                var generatedCount = EstimateGeneratedAutoGridCount(spacing, spacing, horizon);
                if (generatedCount <= 0)
                {
                    return;
                }

                maxGenerated = Math.Max(maxGenerated, generatedCount);

                var difference = Math.Abs(generatedCount - desiredPointCount);
                var stabilityPenalty = EstimateAutoGridCountStabilityPenalty(spacing, spacing, horizon, generatedCount);
                candidates.Add((spacing, generatedCount, difference, stabilityPenalty));
            }

            for (var spacing = 0.5d; spacing <= 90.0d; spacing += 0.25d)
            {
                EvaluateScalar(spacing);
            }

            for (var spacing = Math.Max(0.5d, bestSpacing - 2.0d); spacing <= Math.Min(90.0d, bestSpacing + 2.0d); spacing += 0.05d)
            {
                EvaluateScalar(spacing);
            }

            if (candidates.Count == 0)
            {
                return GenerateAutoGrid(guessSpacing, guessSpacing, horizon);
            }

            var minDifference = candidates.Min(c => c.difference);
            var proximityWindow = Math.Max(4, (int)Math.Round(desiredPointCount * 0.12d));
            var allowedDifference = Math.Max(minDifference, proximityWindow);
            var minimumReasonableGenerated = maxGenerated >= desiredPointCount
                ? Math.Max(3, (int)Math.Round(desiredPointCount * 0.75d))
                : 0;

            var shortlist = candidates
                .Where(c => c.difference <= allowedDifference && c.generated >= minimumReasonableGenerated)
                .ToList();

            if (shortlist.Count == 0)
            {
                shortlist = candidates
                    .Where(c => c.difference == minDifference)
                    .ToList();
            }

            var bestCandidate = shortlist
                .OrderBy(c => c.stabilityPenalty)
                .ThenBy(c => c.difference)
                .ThenBy(c => c.generated > desiredPointCount ? 1 : 0)
                .ThenBy(c => c.spacing)
                .First();

            bestSpacing = bestCandidate.spacing;

            var points = GenerateAutoGrid(bestSpacing, bestSpacing, horizon);
            var generated = points.Count(p => p.ModelPointState == ModelPointStateEnum.Generated);
            if (generated != desiredPointCount)
            {
                Logger.Info($"AutoGrid requested {desiredPointCount} points; selected equidistant spacing {bestSpacing:F2}° with {generated} points (±{Math.Abs(generated - desiredPointCount)}). Max found during search: {maxGenerated}.");
            }
            return points;
        }

        private (double raSpacingDegrees, double decSpacingDegrees, int generatedCount)? FindBestQuantizedAutoGridCandidate(int desiredPointCount, CustomHorizon horizon)
        {
            (double raSpacingDegrees, double decSpacingDegrees, int generatedCount)? best = null;
            double bestScore = double.MaxValue;
            var maxDifference = Math.Max(12, (int)Math.Round(desiredPointCount * 0.20d));

            var preferredVisibleRingCount = Math.Max(6, (int)Math.Round(Math.Sqrt(desiredPointCount)) - 1);
            var preferredDecSpacing = 90.0d / Math.Max(1, preferredVisibleRingCount - 1);

            for (var ringSegments = 8; ringSegments <= 36; ringSegments++)
            {
                var decSpacing = 180.0d / ringSegments;
                for (var baseCount = 8; baseCount <= 144; baseCount++)
                {
                    var raSpacing = 360.0d / baseCount;
                    var generatedCount = EstimateGeneratedAutoGridCount(raSpacing, decSpacing, horizon);
                    if (generatedCount <= 0)
                    {
                        continue;
                    }

                    var difference = Math.Abs(generatedCount - desiredPointCount);
                    if (difference > maxDifference)
                    {
                        continue;
                    }

                    var stabilityPenalty = EstimateAutoGridCountStabilityPenalty(raSpacing, decSpacing, horizon, generatedCount);
                    var ringFamilyPenalty = Math.Abs(decSpacing - preferredDecSpacing);
                    var overTargetPenalty = generatedCount > desiredPointCount ? 1.0d : 0.0d;
                    var score =
                        (stabilityPenalty * 100.0d) +
                        (ringFamilyPenalty * 5.0d) +
                        (difference * 1.0d) +
                        (overTargetPenalty * 0.5d);

                    if (score < bestScore ||
                        (Math.Abs(score - bestScore) < 1e-9d && best.HasValue && generatedCount <= desiredPointCount && best.Value.generatedCount > desiredPointCount) ||
                        (Math.Abs(score - bestScore) < 1e-9d && !best.HasValue))
                    {
                        best = (raSpacing, decSpacing, generatedCount);
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        private int EstimateAutoGridCountStabilityPenalty(double raSpacingDegrees, double decSpacingDegrees, CustomHorizon horizon, int centerCount)
        {
            if (centerCount <= 0)
            {
                return int.MaxValue;
            }

            var penalty = 0;
            var offsets = new (double raOffset, double decOffset)[]
            {
                (-0.5d, 0.0d),
                (0.5d, 0.0d),
                (0.0d, -0.5d),
                (0.0d, 0.5d)
            };

            foreach (var (raOffset, decOffset) in offsets)
            {
                var candidateRaSpacing = Math.Max(0.5d, Math.Min(90.0d, raSpacingDegrees + raOffset));
                var candidateDecSpacing = Math.Max(0.5d, Math.Min(90.0d, decSpacingDegrees + decOffset));
                var candidateCount = EstimateGeneratedAutoGridCount(candidateRaSpacing, candidateDecSpacing, horizon);
                if (candidateCount <= 0)
                {
                    penalty += centerCount;
                }
                else
                {
                    penalty += Math.Abs(candidateCount - centerCount);
                }
            }

            return penalty;
        }

        private int EstimateGeneratedAutoGridCount(double raSpacingDegrees, double decSpacingDegrees, CustomHorizon horizon)
        {
            try
            {
                var points = GenerateAutoGrid(raSpacingDegrees, decSpacingDegrees, horizon);
                return points.Count(p => p.ModelPointState == ModelPointStateEnum.Generated);
            }
            catch
            {
                return 0;
            }
        }

        private List<double> BuildPolarRingDistances(double decSpacingDegrees)
        {
            var ringDistances = new List<double>();
            for (var ringDistanceDegrees = 0.0d; ringDistanceDegrees <= 180.0d; ringDistanceDegrees += decSpacingDegrees)
            {
                ringDistances.Add(Math.Min(180.0d, ringDistanceDegrees));
            }

            if (ringDistances.Count == 0 || Math.Abs(ringDistances[ringDistances.Count - 1] - 180.0d) > 0.0001d)
            {
                ringDistances.Add(180.0d);
            }

            return ringDistances;
        }

        private static double CounterClockwiseDistanceDegrees(double fromAzimuth, double toAzimuth)
        {
            return AstroUtil.EuclidianModulus(fromAzimuth - toAzimuth, 360.0d);
        }

        private static double ClockwiseDistanceDegrees(double fromAzimuth, double toAzimuth)
        {
            return AstroUtil.EuclidianModulus(toAzimuth - fromAzimuth, 360.0d);
        }

        private List<double> GetAutoGridRaValuesForRing(double ringDistanceDegrees, double raSpacingDegrees, int ringIndex)
        {
            if (Math.Abs(ringDistanceDegrees) < 0.0001d)
            {
                return new List<double>();
            }

            if (Math.Abs(ringDistanceDegrees - 180.0d) < 0.0001d)
            {
                return new List<double> { 0.0d };
            }

            var baseCount = Math.Max(1, (int)Math.Round(360.0d / raSpacingDegrees));
            var rawRingScale = Math.Abs(Math.Sin(Angle.ByDegree(ringDistanceDegrees).Radians));
            var ringScale = Math.Pow(rawRingScale, AUTO_GRID_RING_SCALE_EXPONENT);
            var ringCount = Math.Max(2, (int)Math.Round(baseCount * ringScale));

            var ringSpacing = 360.0d / ringCount;
            var ringPhase = AstroUtil.EuclidianModulus(
                (ringIndex * AUTO_GRID_RING_PHASE_STEP_DEGREES) +
                (ringIndex % 2 == 0 ? 0.0d : ringSpacing / 2.0d),
                360.0d);
            var startHourAngle = -180.0d + ringPhase;

            var raValues = new List<double>(ringCount);
            for (var i = 0; i < ringCount; i++)
            {
                var hourAngleDegrees = startHourAngle + (i * ringSpacing);
                if (hourAngleDegrees < -180.0d)
                {
                    hourAngleDegrees += 360.0d;
                }
                else if (hourAngleDegrees >= 180.0d)
                {
                    hourAngleDegrees -= 360.0d;
                }
                raValues.Add(hourAngleDegrees);
            }

            return raValues;
        }

        private (double altitudeDegrees, double azimuthDegrees) ToHorizontalFromDeclinationHourAngle(double latitudeDegrees, double declinationDegrees, double hourAngleDegrees)
        {
            var latitudeRadians = Angle.ByDegree(latitudeDegrees).Radians;
            var declinationRadians = Angle.ByDegree(declinationDegrees).Radians;
            var hourAngleRadians = Angle.ByDegree(hourAngleDegrees).Radians;

            var sinAltitude = (Math.Sin(latitudeRadians) * Math.Sin(declinationRadians)) +
                              (Math.Cos(latitudeRadians) * Math.Cos(declinationRadians) * Math.Cos(hourAngleRadians));
            var altitudeRadians = Math.Asin(Math.Max(-1.0d, Math.Min(1.0d, sinAltitude)));
            var cosAltitude = Math.Cos(altitudeRadians);

            double azimuthRadians;
            if (Math.Abs(cosAltitude) < 1e-12d)
            {
                azimuthRadians = 0.0d;
            }
            else
            {
                var sinAzimuth = -(Math.Cos(declinationRadians) * Math.Sin(hourAngleRadians)) / cosAltitude;
                var cosAzimuth = (Math.Sin(declinationRadians) - (Math.Sin(altitudeRadians) * Math.Sin(latitudeRadians))) /
                                 (cosAltitude * Math.Cos(latitudeRadians));
                azimuthRadians = Math.Atan2(sinAzimuth, cosAzimuth);
            }

            var altitudeDegrees = Angle.ByRadians(altitudeRadians).Degree;
            var azimuthDegrees = AstroUtil.EuclidianModulus(Angle.ByRadians(azimuthRadians).Degree, 360.0d);
            return (altitudeDegrees, azimuthDegrees);
        }

        private (double altitudeDegrees, double azimuthDegrees) MoveProjectedCircle(double poleProjectionOffsetDegrees, bool isNorthernHemisphere, double ringDistanceDegrees, double ringAngleDegrees)
        {
            var centerX = 0.0d;
            var centerY = isNorthernHemisphere ? poleProjectionOffsetDegrees : -poleProjectionOffsetDegrees;

            var ringAngleRadians = Angle.ByDegree(ringAngleDegrees).Radians;
            var x = centerX + (ringDistanceDegrees * Math.Sin(ringAngleRadians));
            var y = centerY + (ringDistanceDegrees * Math.Cos(ringAngleRadians));

            var zenithDistanceDegrees = Math.Sqrt((x * x) + (y * y));
            var altitudeDegrees = 90.0d - zenithDistanceDegrees;

            var azimuthDegrees = AstroUtil.EuclidianModulus(Angle.ByRadians(Math.Atan2(x, y)).Degree, 360.0d);
            return (altitudeDegrees, azimuthDegrees);
        }

        private (double altitudeDegrees, double azimuthDegrees) MoveGreatCircle(double startAltitudeDegrees, double startAzimuthDegrees, double distanceDegrees, double bearingDegrees)
        {
            var lat1 = Angle.ByDegree(startAltitudeDegrees).Radians;
            var lon1 = Angle.ByDegree(startAzimuthDegrees).Radians;
            var distance = Angle.ByDegree(distanceDegrees).Radians;
            var bearing = Angle.ByDegree(bearingDegrees).Radians;

            var sinLat2 = (Math.Sin(lat1) * Math.Cos(distance)) + (Math.Cos(lat1) * Math.Sin(distance) * Math.Cos(bearing));
            var lat2 = Math.Asin(Math.Max(-1.0d, Math.Min(1.0d, sinLat2)));

            var y = Math.Sin(bearing) * Math.Sin(distance) * Math.Cos(lat1);
            var x = Math.Cos(distance) - (Math.Sin(lat1) * Math.Sin(lat2));
            var lon2 = lon1 + Math.Atan2(y, x);

            var altitudeDegrees = Angle.ByRadians(lat2).Degree;
            var azimuthDegrees = AstroUtil.EuclidianModulus(Angle.ByRadians(lon2).Degree, 360.0d);
            return (altitudeDegrees, azimuthDegrees);
        }

        private TopocentricCoordinates ToTopocentric(Coordinates coordinates, DateTime dateTime)
        {
            var coordinatesAtTime = new Coordinates(Angle.ByHours(coordinates.RA), Angle.ByDegree(coordinates.Dec), coordinates.Epoch, new ConstantDateTime(dateTime));
            var latitude = Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude);
            var longitude = Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude);
            var elevation = profileService.ActiveProfile.AstrometrySettings.Elevation;

            var weatherDataInfo = weatherDataMediator.GetInfo();
            var pressurehPa = weatherDataInfo.Connected ? weatherDataInfo.Pressure : 0.0d;
            var temperature = weatherDataInfo.Connected ? weatherDataInfo.Temperature : 0.0d;
            var wavelength = weatherDataInfo.Connected ? 0.55d : 0.0d;
            var humidity = weatherDataInfo.Connected && !double.IsNaN(weatherDataInfo.Humidity) ? weatherDataInfo.Humidity : 0.0d;
            return coordinatesAtTime.Transform(
                latitude: latitude,
                longitude: longitude,
                elevation: elevation,
                pressurehPa: pressurehPa,
                tempCelcius: temperature,
                relativeHumidity: humidity,
                wavelength: wavelength);
        }

        private ModelPointStateEnum DeterminePointState(double altitudeDegrees, double azimuthDegrees, double horizonAltitude, bool applyAzimuthBounds)
        {
            if (altitudeDegrees < options.MinPointAltitude || altitudeDegrees > options.MaxPointAltitude)
            {
                return ModelPointStateEnum.OutsideAltitudeBounds;
            }
            if (applyAzimuthBounds && (azimuthDegrees < options.MinPointAzimuth || azimuthDegrees >= options.MaxPointAzimuth))
            {
                return ModelPointStateEnum.OutsideAzimuthBounds;
            }
            if (altitudeDegrees >= horizonAltitude)
            {
                return ModelPointStateEnum.Generated;
            }
            return ModelPointStateEnum.BelowHorizon;
        }

        public List<ModelPoint> GenerateSiderealPath(Coordinates coordinates, Angle raDelta, DateTime startTime, DateTime endTime, CustomHorizon horizon)
        {
            Logger.Debug($"Generating sidereal path for {coordinates.RA}, {coordinates.Dec}");
            if (endTime < startTime)
            {
                throw new Exception($"End time ({endTime}) comes before start time ({startTime})");
            }
            if (endTime > (startTime + TimeSpan.FromDays(1)))
            {
                throw new Exception($"End time ({endTime}) is more than 1 day beyond start time ({startTime})");
            }
            if (TimeSpan.FromHours(raDelta.Hours) <= TimeSpan.FromSeconds(1))
            {
                throw new Exception($"RA delta ({raDelta}) cannot be less than 1 arc second");
            }

            double timeToLimit = mount.TimeToLimit();
            Logger.Debug($"TimeToLimit={timeToLimit}");

            var meridianLimitDegrees = 180 + mount.MeridianFlipMaxAngle();
            Logger.Debug($"MeridianFlipMaxangle={meridianLimitDegrees}");

            //  var meridianLimitDegrees = 0.0d;
            Logger.Info($"Using meridian limit {meridianLimitDegrees:0.##}°");
            var toleranceDegrees = 0.1d; // Small buffer to avoid flip at exact limit
            var meridianUpperLimit = meridianLimitDegrees - toleranceDegrees;
            var meridianLowerLimit = 360.0d - meridianLimitDegrees + toleranceDegrees;

            var points = new List<ModelPoint>();
            var decJitterSigmaDegrees = 0;

            // Adjust startTime if the object is still below the horizon
            while (true)
            {
                var pointCoordinates = ToTopocentric(coordinates, startTime);
                var altitudeDegrees = pointCoordinates.Altitude.Degree;
                var horizonAltitude = horizon.GetAltitude(pointCoordinates.Azimuth.Degree);

                if (altitudeDegrees >= horizonAltitude)
                {
                    break;
                }

                //Logger.Info($"Object below horizon at {startTime}. Adjusting startTime.");
                startTime += TimeSpan.FromMinutes(1);
            }

            // Determine the time when the meridian flip is hit
            DateTime meridianFlipTime = endTime;
            var currentTime = startTime;

            /*
            var initialCoords = ToTopocentric(coordinates, startTime);
            double initialAzimuth = initialCoords.Azimuth.Degree;
            bool isWestOfMeridian = initialAzimuth < 180;

            while (currentTime < endTime)
            {
                var nextCoordinates = coordinates.Clone();
                var pointCoordinates = ToTopocentric(nextCoordinates, currentTime);
                var azimuthDegrees = pointCoordinates.Azimuth.Degree;
                var altitudeDegrees = pointCoordinates.Altitude.Degree;

                Logger.Debug($"Az={azimuthDegrees}, upperlimit={meridianUpperLimit}, lowerlimit={meridianLowerLimit}");

                var horizonAltitude = horizon.GetAltitude(azimuthDegrees);
                // For WEST targets: Only check upper limit (195°)
                if (isWestOfMeridian && (azimuthDegrees >= meridianUpperLimit || azimuthDegrees < 0))
                {
                    Logger.Debug($"Meridian flip triggered at {currentTime} (Az={azimuthDegrees:0.##}°)");
                    meridianFlipTime = currentTime;
                    break;
                }
                // For EAST targets: Only check lower limit (345°)
                else if (!isWestOfMeridian && (azimuthDegrees <= meridianLowerLimit || azimuthDegrees > 360))
                {
                    Logger.Debug($"Meridian flip triggered at {currentTime} (Az={azimuthDegrees:0.##}°)");
                    meridianFlipTime = currentTime;
                    break;
                }
                else if (azimuthDegrees < options.MinPointAzimuth || azimuthDegrees >= options.MaxPointAzimuth)
                {
                    //creationState = ModelPointStateEnum.OutsideAzimuthBounds;
                    Logger.Info($"Point Az={azimuthDegrees:0.##} hits azimuth limits at {currentTime}. Adjusting endTime.");
                    meridianFlipTime = currentTime;
                    break;
                }
                else if (altitudeDegrees < horizonAltitude)
                {
                    //below horizon
                    Logger.Info($"Point Alt={altitudeDegrees:0.##} hits horizon at {currentTime}. Adjusting endTime.");
                    meridianFlipTime = currentTime;
                    break;
                }

                //currentTime += TimeSpan.FromHours(raDelta.Hours);
                //increase currentTime in 1 minute steps
                currentTime += TimeSpan.FromMinutes(1);
            }

            // Adjust endTime to the meridian flip time
            endTime = meridianFlipTime;
            */

            var maxEndTime = currentTime + TimeSpan.FromMinutes(timeToLimit) - TimeSpan.FromMinutes(2);
            if (endTime > maxEndTime)
            {
                endTime = maxEndTime;
                Logger.Info($"Adjusted end time to {endTime}");
            }

            // Calculate the total duration and the number of intervals
            var totalDuration = endTime - startTime;
            var totalHours = totalDuration.TotalHours;
            var numIntervals = (int)(totalHours / raDelta.Hours);

            Logger.Debug($"Total duration: {totalDuration}, total hours: {totalHours}, numIntervals: {numIntervals}");

            if (numIntervals < 2)
            {
                numIntervals = 2;
                raDelta = Angle.ByHours(totalHours / numIntervals);
                Logger.Info("Adjusted to minimum of 3 points for sidereal path.");
            }

            // Adjust raDelta to ensure equidistant points
            if (numIntervals > 0)
            {
                raDelta = Angle.ByHours(totalHours / numIntervals);
                Logger.Debug($"Using RA delta of {raDelta}");
            }
            else
            {
                raDelta = Angle.ByHours(0);
                Logger.Info("No points found");
            }

            points.Clear();
            int validPoints = 0;

            for (int i = 0; i <= numIntervals; i++)
            {
                currentTime = startTime + TimeSpan.FromHours(raDelta.Hours * i);

                var nextCoordinates = coordinates.Clone();

                var decJitter = NormalDistribution.Random(mean: 0.0, stdDev: decJitterSigmaDegrees);
                decJitter = Math.Min(3.0d * decJitterSigmaDegrees, Math.Max(-3.0d * decJitterSigmaDegrees, decJitter));

                var nextDec = nextCoordinates.Dec + decJitter;
                nextCoordinates.Dec = Math.Min(90.0d, Math.Max(-90.0d, nextDec));

                var pointCoordinates = ToTopocentric(nextCoordinates, currentTime);
                var azimuthDegrees = pointCoordinates.Azimuth.Degree;
                var altitudeDegrees = pointCoordinates.Altitude.Degree;

                var horizonAltitude = horizon.GetAltitude(azimuthDegrees);
                var creationState = DeterminePointState(altitudeDegrees, azimuthDegrees, horizonAltitude, applyAzimuthBounds: false);
                if (creationState == ModelPointStateEnum.Generated)
                {
                    ++validPoints;
                }

                points.Add(
                    new ModelPoint(telescopeMediator)
                    {
                        Altitude = altitudeDegrees,
                        Azimuth = azimuthDegrees,
                        ModelPointState = creationState
                    });
            }

            return points;
        }
    }
}