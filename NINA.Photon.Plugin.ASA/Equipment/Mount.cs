#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Antlr4.Runtime;
using NINA.Photon.Plugin.ASA.Grammars;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Utility;
using NINA.Core.Enum;
using NINA.Core.Utility;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using NINA.Photon.Plugin.ASA.Converters;
using NINA.Photon.Plugin.ASA.Model;
using System.Linq;

namespace NINA.Photon.Plugin.ASA.Equipment {

    public class ResponseBase {

        public ResponseBase(string rawResponse) {
            this.RawResponse = rawResponse;
        }

        public string RawResponse { get; private set; }
    }

    public class Response<T> : ResponseBase {

        public Response(T value, string rawResponse) : base(rawResponse) {
            this.Value = value;
        }

        public T Value { get; private set; }

        public static implicit operator T(Response<T> r) => r.Value;

        public override string ToString() {
            return Value.ToString();
        }
    }

    public static class LexerCreator<T> where T : Lexer {
        public static readonly Func<ICharStream, T> Construct = ConstructImpl(typeof(T));

        private static Func<ICharStream, T> ConstructImpl(Type type) {
            var parameters = new Type[] { typeof(ICharStream) };
            var constructorInfo = type.GetConstructor(parameters);
            var paramExpr = Expression.Parameter(typeof(ICharStream));
            var body = Expression.New(constructorInfo, paramExpr);
            var constructor = Expression.Lambda<Func<ICharStream, T>>(body, paramExpr);
            return constructor.Compile();
        }
    }

    public static class ParserCreator<T> where T : Parser {
        public static readonly Func<ITokenStream, T> Construct = ConstructImpl(typeof(T));

        private static Func<ITokenStream, T> ConstructImpl(Type type) {
            var parameters = new Type[] { typeof(ITokenStream) };
            var constructorInfo = type.GetConstructor(parameters);
            var paramExpr = Expression.Parameter(typeof(ITokenStream));
            var body = Expression.New(constructorInfo, paramExpr);
            var constructor = Expression.Lambda<Func<ITokenStream, T>>(body, paramExpr);
            return constructor.Compile();
        }
    }

    [TypeConverter(typeof(EnumStaticDescriptionTypeConverter))]
    public enum MountStatusEnum {

        [Description("Tracking")]
        Tracking = 0,

        [Description("Stopped")]
        Stopped = 1,

        [Description("Slewing to Park")]
        SlewingToPark = 2,

        [Description("Unparking")]
        Unparking = 3,

        [Description("Slewing to Home")]
        SlewingToHome = 4,

        [Description("Parked")]
        Parked = 5,

        [Description("Slewing")]
        Slewing = 6,

        [Description("Not Tracking")]
        TrackingOff = 7,

        [Description("Motors Inhibited by Low Temperature")]
        MotorsInhibitedLowTemp = 8,

        [Description("Tracking, but outside of mount limits")]
        TrackingOnOutsideLimits = 9,

        [Description("Following Satellite")]
        FollowingSatellite = 10,

        [Description("Needs User Intervention")]
        NeedsUserIntervention = 11,

        [Description("Unknown")]
        Unknown = 98,

        [Description("Error")]
        Error = 99,

        [Description("Not Connected")]
        NotConnected = 100
    }

    public static class MountResponseParser {

        private static int ParseIntOrDefault(string s, int defaultValue) {
            return s != null ? int.Parse(s) : defaultValue;
        }

        private static P GetParser<L, P>(string s)
            where L : Lexer
            where P : Parser {
            var inputStream = new AntlrInputStream(s);
            var lexer = LexerCreator<L>.Construct(inputStream);
            var commonTokenStream = new CommonTokenStream(lexer);
            var parser = ParserCreator<P>.Construct(commonTokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(ThrowingErrorListener.INSTANCE);
            return parser;
        }

        public static Response<CoordinateAngle> ParseCoordinateAngle(string s) {
            var parser = GetParser<AngleLexer, AngleParser>(s);
            var context = parser.angle();

            var sign = context.sign().GetText();
            var degrees = int.Parse(context.degrees().GetText(), CultureInfo.InvariantCulture);
            var minutes = int.Parse(context.minutes().GetText(), CultureInfo.InvariantCulture);
            var seconds = ParseIntOrDefault(context.seconds()?.GetText(), 0);
            var tenthSeconds = ParseIntOrDefault(context.tenth_seconds()?.GetText(), 0);
            var positive = sign != "-";
            return new Response<CoordinateAngle>(new CoordinateAngle(positive, degrees, minutes, seconds, (byte)(tenthSeconds * 10)), s);
        }

        public static Response<AstrometricTime> ParseAstrometricTime(string s) {
            var parser = GetParser<TimeLexer, TimeParser>(s);
            var context = parser.time();

            var hours = int.Parse(context.hours().GetText(), CultureInfo.InvariantCulture);
            var minutes = int.Parse(context.minutes().GetText(), CultureInfo.InvariantCulture);
            var tenthMinutes = ParseIntOrDefault(context.tenth_minutes()?.GetText(), 0);
            var seconds = ParseIntOrDefault(context.seconds()?.GetText(), 0);
            var tenthSeconds = ParseIntOrDefault(context.tenth_seconds()?.GetText(), 0);
            var hundredthSeconds = ParseIntOrDefault(context.hundredth_seconds()?.GetText(), 0) + 10 * tenthSeconds;
            return new Response<AstrometricTime>(new AstrometricTime(hours, minutes, seconds + 6 * tenthMinutes, hundredthSeconds + 10 * tenthSeconds), s);
        }

        public static Response<AlignmentStarInfo> ParseAlignmentStarInfo(string s) {
            var parser = GetParser<AlignmentStarInfoLexer, AlignmentStarInfoParser>(s);
            var context = parser.alignmentStarInfo();

            var localHourContext = context.time();
            var declinationAngleContext = context.angle();
            var errorContext = context.error();

            var localHours = int.Parse(localHourContext.hours().GetText(), CultureInfo.InvariantCulture);
            var localMinutes = int.Parse(localHourContext.minutes().GetText(), CultureInfo.InvariantCulture);
            var localSeconds = int.Parse(localHourContext.seconds().GetText(), CultureInfo.InvariantCulture);
            var raHundredthSeconds = int.Parse(localHourContext.hundredthSeconds().GetText(), CultureInfo.InvariantCulture);
            var rightAscension = new AstrometricTime(localHours, localMinutes, localSeconds, raHundredthSeconds);

            var decSign = declinationAngleContext.sign().GetText();
            var decDegrees = int.Parse(declinationAngleContext.degrees().GetText(), CultureInfo.InvariantCulture);
            var decMinutes = int.Parse(declinationAngleContext.minutes().GetText(), CultureInfo.InvariantCulture);
            var decSeconds = int.Parse(declinationAngleContext.seconds().GetText(), CultureInfo.InvariantCulture);
            var decTenthSeconds = int.Parse(declinationAngleContext.tenthSeconds().GetText(), CultureInfo.InvariantCulture);
            var declination = new CoordinateAngle(decSign != "-", decDegrees, decMinutes, decSeconds, decTenthSeconds * 10);

            var errorArcseconds = decimal.Parse(errorContext.GetText(), CultureInfo.InvariantCulture);
            return new Response<AlignmentStarInfo>(new AlignmentStarInfo(rightAscension, declination, errorArcseconds), s);
        }

        public static Response<AlignmentModelInfo> ParseAlignmentModelInfo(string s) {
            var parser = GetParser<AlignmentModelInfoLexer, AlignmentModelInfoParser>(s);
            var context = parser.alignmentModelInfo();

            var raAzimuth = decimal.Parse(context.raAzimuth().GetText(), CultureInfo.InvariantCulture);
            var raAltitude = decimal.Parse(context.raAltitude().GetText(), CultureInfo.InvariantCulture);
            var paError = decimal.Parse(context.paError().GetText(), CultureInfo.InvariantCulture);
            var raPositionAngle = decimal.Parse(context.raPositionAngle().GetText(), CultureInfo.InvariantCulture);
            var orthogonalityError = decimal.Parse(context.orthogonalityError().GetText(), CultureInfo.InvariantCulture);
            var azimuthTurns = decimal.Parse(context.azimuthTurns().GetText(), CultureInfo.InvariantCulture);
            var altitudeTurns = decimal.Parse(context.altitudeTurns().GetText(), CultureInfo.InvariantCulture);
            var modelTerms = int.Parse(context.modelTerms().GetText(), CultureInfo.InvariantCulture);
            var rmsError = decimal.Parse(context.rmsError().GetText(), CultureInfo.InvariantCulture);
            return new Response<AlignmentModelInfo>(
                new AlignmentModelInfo(
                    rightAscensionAzimuth: raAzimuth, rightAscensionAltitude: raAltitude, polarAlignErrorDegrees: paError,
                    rightAscensionPolarPositionAngleDegrees: raPositionAngle, orthogonalityErrorDegrees: orthogonalityError, azimuthAdjustmentTurns: azimuthTurns,
                    altitudeAdjustmentTurns: altitudeTurns, modelTerms: modelTerms, rmsError: rmsError),
                s);
        }

        private static string SanitizeIP(string ip) {
            var ipParts = ip.Trim().Split('.');
            return string.Join(".", ipParts.Select(s => int.Parse(s, CultureInfo.InvariantCulture)));
        }

        public static Response<MountIP> ParseIP(string s) {
            var splitResponse = s.TrimEnd('#').Split(',');
            if (splitResponse.Length != 4) {
                throw new ArgumentException($"IP response expected to have 4 parts, separated by commas. {s}");
            }

            var ipAddress = SanitizeIP(splitResponse[0]);
            var subnet = SanitizeIP(splitResponse[1]);
            var gateway = SanitizeIP(splitResponse[2]);
            var fromDHCP = splitResponse[3] == "D";
            return new Response<MountIP>(new MountIP(ip: ipAddress, subnet: subnet, gateway: gateway, fromDHCP: fromDHCP), s);
        }
    }

    public class Mount : IMount {
        private readonly IMountCommander mountCommander;

        public Mount(IMountCommander mountCommander) {
            this.mountCommander = mountCommander;
        }

        public Response<CoordinateAngle> GetDeclination() {
            const string command = ":GD#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseCoordinateAngle(rawResponse);
        }

        public Response<AstrometricTime> GetRightAscension() {
            const string command = ":GR#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseAstrometricTime(rawResponse);
        }

        public Response<AstrometricTime> GetLocalSiderealTime() {
            const string command = ":GS#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseAstrometricTime(rawResponse);
        }

        public Response<int> GetModelCount() {
            const string command = ":modelcnt#";

            // returns nnn#
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<int>(result, rawResponse);
        }

        public Response<string> GetModelName(int modelIndex) {
            if (modelIndex < 1 || modelIndex > 99) {
                throw new ArgumentException("modelIndex must be between 1 and 99 inclusive", "modelIndex");
            }
            string command = $":modelnam{modelIndex}#";
            // returns name#, or just # if it is not valid
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var name = rawResponse.TrimEnd('#');
            if (string.IsNullOrEmpty(name)) {
                throw new Exception($"{modelIndex} is not a valid model index");
            }
            return new Response<string>(name, rawResponse);
        }

        public Response<bool> LoadModel(string name) {
            string command = $":modelld0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public Response<bool> SaveModel(string name) {
            string command = $":modelsv0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public Response<bool> DeleteModel(string name) {
            string command = $":modeldel0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public void DeleteAlignment() {
            const string command = ":delalig#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            if (!string.IsNullOrWhiteSpace(rawResponse.TrimEnd('#'))) {
                throw new Exception($"Failed to delete alignment. {command} returned {rawResponse}");
            }
        }

        public Response<int> GetAlignmentStarCount() {
            const string command = ":getalst#";

            // Returns count followed by #
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<int>(result, rawResponse);
        }

        public Response<AlignmentStarInfo> GetAlignmentStarInfo(int alignmentStarIndex) {
            if (alignmentStarIndex < 1) {
                throw new ArgumentException("alignmentStarIndex must be >= 1", "alignmentStarIndex");
            }
            string command = $":getali{alignmentStarIndex}#";

            // returns HH:MM:SS.SS,+dd*mm:ss.s,eeee.e#
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseAlignmentStarInfo(rawResponse);
        }

        public Response<AlignmentModelInfo> GetAlignmentModelInfo() {
            const string command = ":getain#";

            // returns ZZZ.ZZZZ,+AA.AAAA,EE.EEEE,PPP.PP,+OO.OOOO,+aa.aa,+bb.bb,NN,RRRRR.R#
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseAlignmentModelInfo(rawResponse);
        }

        public Response<bool> StartNewAlignmentSpec() {
            const string command = ":newalig#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var success = rawResponse == "V#";
            return new Response<bool>(success, rawResponse);
        }

        public Response<bool> FinishAlignmentSpec() {
            const string command = ":endalig#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var success = rawResponse == "V#";
            return new Response<bool>(success, rawResponse);
        }

        public Response<bool> Shutdown() {
            const string command = ":shutdown#";

            var success = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(success, "");
        }

        public Response<PierSide> GetSideOfPier() {
            const string command = ":pS#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            PierSide sideOfPier;
            if (StringComparer.OrdinalIgnoreCase.Equals(rawResponse, "East#")) {
                sideOfPier = PierSide.pierEast;
            } else if (StringComparer.OrdinalIgnoreCase.Equals(rawResponse, "West#")) {
                sideOfPier = PierSide.pierWest;
            } else {
                throw new Exception($"Unexpected pier side {rawResponse} returned by {command}");
            }
            return new Response<PierSide>(sideOfPier, rawResponse);
        }

        public Response<int> AddAlignmentPointToSpec(
            AstrometricTime mountRightAscension,
            CoordinateAngle mountDeclination,
            PierSide sideOfPier,
            AstrometricTime plateSolvedRightAscension,
            CoordinateAngle plateSolvedDeclination,
            AstrometricTime localSiderealTime) {
            var mountRightAscensionRounded = mountRightAscension.RoundTenthSecond();
            var mountDeclinationRounded = mountDeclination.RoundSeconds();
            var plateSolvedRightAscensionRounded = plateSolvedRightAscension.RoundTenthSecond();
            var plateSolvedDeclinationRounded = plateSolvedDeclination.RoundSeconds();
            var siderealTimeRounded = localSiderealTime.RoundTenthSecond();

            var commandBuilder = new StringBuilder();
            commandBuilder.Append(":newalpt");
            commandBuilder.Append($"{mountRightAscensionRounded.Hours:00}:{mountRightAscensionRounded.Minutes:00}:{mountRightAscensionRounded.Seconds:00}.{mountRightAscensionRounded.HundredthSeconds / 10:0},");
            commandBuilder.Append($"{(mountDeclinationRounded.Positive ? "+" : "-")}{mountDeclinationRounded.Degrees:00}:{mountDeclinationRounded.Minutes:00}:{mountDeclinationRounded.Seconds:00},");
            switch (sideOfPier) {
                case PierSide.pierEast:
                    commandBuilder.Append("E,");
                    break;

                case PierSide.pierWest:
                    commandBuilder.Append("W,");
                    break;

                default:
                    throw new ArgumentException($"Unexpected side of pier {sideOfPier}", "sideOfPier");
            }
            commandBuilder.Append($"{plateSolvedRightAscensionRounded.Hours:00}:{plateSolvedRightAscensionRounded.Minutes:00}:{plateSolvedRightAscensionRounded.Seconds:00}.{plateSolvedRightAscensionRounded.HundredthSeconds / 10:0},");
            commandBuilder.Append($"{(plateSolvedDeclinationRounded.Positive ? "+" : "-")}{plateSolvedDeclinationRounded.Degrees:00}:{plateSolvedDeclinationRounded.Minutes:00}:{plateSolvedDeclinationRounded.Seconds:00},");
            commandBuilder.Append($"{siderealTimeRounded.Hours:00}:{siderealTimeRounded.Minutes:00}:{siderealTimeRounded.Seconds:00}.{siderealTimeRounded.HundredthSeconds / 10:0}#");
            var command = commandBuilder.ToString();

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            if (rawResponse == "E#") {
                throw new Exception($"Failed to add alignment point using {command}");
            }

            var numPoints = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<int>(numPoints, rawResponse);
        }

        public Response<string> GetId() {
            const string command = ":GETID#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return new Response<string>(rawResponse.TrimEnd('#'), rawResponse);
        }

        public void SetUltraPrecisionMode() {
            const string command = ":U2#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public Response<int> GetMeridianSlewLimitDegrees() {
            const string command = ":Glms#";

            // Returns limit followed by #
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<int>(result, rawResponse);
        }

        public Response<bool> SetMeridianSlewLimit(int degrees) {
            string command = $":Slms{degrees:00}#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<decimal> GetSlewSettleTimeSeconds() {
            const string command = ":Gstm#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result, rawResponse);
        }

        public Response<bool> SetSlewSettleTime(decimal seconds) {
            if (seconds < 0 || seconds > 99999) {
                return new Response<bool>(false, "");
            }

            var command = $":Sstm{seconds:00000.000}#";
            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<MountStatusEnum> GetStatus() {
            const string command = ":Gstat#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<MountStatusEnum>((MountStatusEnum)result, rawResponse);
        }

        public Response<bool> GetUnattendedFlipEnabled() {
            const string command = ":Guaf#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<decimal> GetTrackingRateArcsecsPerSec() {
            const string command = ":GT#";

            // Needs to be divided by 4 to get arcsecs/sec, according to spec
            var rawResponse = this.mountCommander.SendCommandString(command, true);

            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result / 4, rawResponse);
        }

        public void SetUnattendedFlip(bool enabled) {
            var command = $":Suaf{(enabled ? 1 : 0)}#";

            this.mountCommander.SendCommandBlind(command, true);
        }

        private static readonly Version ultraPrecisionMinimumVersion = new Version(2, 10, 0);

        public void SetMaximumPrecision(ProductFirmware productFirmware) {
            if (productFirmware.Version > ultraPrecisionMinimumVersion) {
                this.SetUltraPrecisionMode();
            } else {
                // The ASA ASCOM driver uses this logic
                Logger.Warning($"Firmware {productFirmware.Version} too old to support ultra precision. Falling back to AP emulation mode");
                this.mountCommander.SendCommandBlind(":EMUAP#:U#", true);
            }
        }

        public Response<ProductFirmware> GetProductFirmware() {
            const string productCommand = ":GVP#";
            const string firmwareDateCommand = ":GVD#";
            const string firmwareVersionCommand = ":GVN#";
            const string firmwareTimeCommand = ":GVT#";

            var productRawResponse = this.mountCommander.SendCommandString(productCommand, true);
            var productName = productRawResponse.TrimEnd('#');

            // mmm dd yyyy
            var firmwareDateRawResponse = this.mountCommander.SendCommandString(firmwareDateCommand, true);

            // HH:MM:SS
            var firmwareTimeRawResponse = this.mountCommander.SendCommandString(firmwareTimeCommand, true);
            var firmwareTimestampString = $"{firmwareDateRawResponse.TrimEnd('#')} {firmwareTimeRawResponse.TrimEnd('#')}";

            const string firmwareDateTimeFormat = "MMM dd yyyy HH:mm:ss";
            if (!DateTime.TryParseExact(firmwareTimestampString, firmwareDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var firmwareTimestamp)) {
                throw new Exception($"Failed to parse firmware timestamp {firmwareTimestampString}");
            }

            var firmwareVersionResponse = this.mountCommander.SendCommandString(firmwareVersionCommand, true);
            if (!Version.TryParse(firmwareVersionResponse.TrimEnd('#'), out var firmwareVersion)) {
                throw new Exception($"Failed to parse firmware version {firmwareVersionResponse}");
            }

            return new Response<ProductFirmware>(
                new ProductFirmware(productName, firmwareTimestamp.ToUniversalTime(), firmwareVersion),
                $"{productRawResponse}\n{firmwareDateRawResponse}\n{firmwareTimeRawResponse}\n{firmwareVersionResponse}"
            );
        }

        public Response<bool> DeleteAlignmentStar(int alignmentStarIndex) {
            var command = $":delalst{alignmentStarIndex}#";

            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public Response<decimal> GetPressure() {
            const string command = ":GRPRS#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result, rawResponse);
        }

        public Response<decimal> GetTemperature() {
            const string command = ":GRTMP#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result, rawResponse);
        }

        public Response<bool> GetRefractionCorrectionEnabled() {
            const string command = ":GREF#";

            var response = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(response, "");
        }

        private static readonly string[] DATE_FORMATS = {
            "yyyy-MM-dd",
            "MM/dd/yy",
            "MM:dd:yy"
        };

        public Response<DateTime> GetUTCTime() {
            const string command = ":GUDT#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var responseParts = rawResponse.Split(new char[] { ',' }, 2);
            var datePartString = responseParts[0];
            var timePartString = responseParts[1].TrimEnd('#');

            var datePart = DateTime.ParseExact(datePartString, DATE_FORMATS, null, DateTimeStyles.None);
            int hours = int.Parse(timePartString.Substring(0, 2), CultureInfo.InvariantCulture);
            int minutes = int.Parse(timePartString.Substring(3, 2), CultureInfo.InvariantCulture);
            int seconds;
            if (timePartString.Length == 7) {
                seconds = int.Parse(timePartString.Substring(6, 1), CultureInfo.InvariantCulture) * 6;
            } else {
                seconds = int.Parse(timePartString.Substring(6, 2), CultureInfo.InvariantCulture);
            }
            int hundredthSeconds = 0;
            if (timePartString.Length == 10) {
                hundredthSeconds = int.Parse(timePartString.Substring(9, 1), CultureInfo.InvariantCulture) * 10;
            } else if (timePartString.Length == 11) {
                hundredthSeconds = int.Parse(timePartString.Substring(9, 2), CultureInfo.InvariantCulture);
            }

            var result = new DateTime(datePart.Year, datePart.Month, datePart.Day, hours, minutes, seconds, hundredthSeconds * 10, DateTimeKind.Utc);
            return new Response<DateTime>(result, rawResponse);
        }

        public void SetSiderealTrackingRate() {
            const string command = ":TQ#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public void SetLunarTrackingRate() {
            const string command = ":TL#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public void SetSolarTrackingRate() {
            const string command = ":TSOLAR#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public void StopTracking() {
            const string command = ":AL#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public void StartTracking() {
            const string command = ":AP#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public Response<bool> SetRefractionCorrection(bool enabled) {
            var command = $":SREF{(enabled ? 1 : 0)}#";
            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<MountIP> GetIPAddress() {
            const string command = ":GIP#";

            var response = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseIP(response);
        }

        public Response<string> GetMACAddress() {
            const string command = ":GMAC#";

            var response = this.mountCommander.SendCommandString(command, true);
            return new Response<string>(response.TrimEnd('#'), response);
        }

        public Response<bool> GetDualAxisTrackingEnabled() {
            const string command = ":Gdat#";

            var response = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(response, "");
        }

        public Response<bool> SetDualAxisTracking(bool enabled) {
            var command = $":Sdat{(enabled ? 1 : 0)}#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }
    }
}