#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

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
using System.Collections.Generic;

namespace NINA.Photon.Plugin.ASA.Equipment
{
    public class ResponseBase
    {
        public ResponseBase(string rawResponse)
        {
            this.RawResponse = rawResponse;
        }

        public string RawResponse { get; private set; }
    }

    public class Response<T> : ResponseBase
    {
        public Response(T value, string rawResponse) : base(rawResponse)
        {
            this.Value = value;
        }

        public T Value { get; private set; }

        public static implicit operator T(Response<T> r) => r.Value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    [TypeConverter(typeof(EnumStaticDescriptionTypeConverter))]
    public enum MountStatusEnum
    {
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

    public static class MountResponseParser
    {
        private static int ParseIntOrDefault(string s, int defaultValue)
        {
            return s != null ? int.Parse(s) : defaultValue;
        }

        private static string SanitizeIP(string ip)
        {
            var ipParts = ip.Trim().Split('.');
            return string.Join(".", ipParts.Select(s => int.Parse(s, CultureInfo.InvariantCulture)));
        }

        public static Response<MountIP> ParseIP(string s)
        {
            var splitResponse = s.TrimEnd('#').Split(',');
            if (splitResponse.Length != 4)
            {
                throw new ArgumentException($"IP response expected to have 4 parts, separated by commas. {s}");
            }

            var ipAddress = SanitizeIP(splitResponse[0]);
            var subnet = SanitizeIP(splitResponse[1]);
            var gateway = SanitizeIP(splitResponse[2]);
            var fromDHCP = splitResponse[3] == "D";
            return new Response<MountIP>(new MountIP(ip: ipAddress, subnet: subnet, gateway: gateway, fromDHCP: fromDHCP), s);
        }
    }

    public class Mount : IMount
    {
        private readonly IMountCommander mountCommander;

        public Mount(IMountCommander mountCommander)
        {
            this.mountCommander = mountCommander;
            this.modelPoints = new List<ModelPoint>();
        }

        public List<ModelPoint> modelPoints { get; set; }

        public Response<bool> PowerOn()
        {
            this.mountCommander.Action("Telescope:MotorOn", "");
            return new Response<bool>(true, "");
        }

        public Response<bool> PowerOff()
        {
            this.mountCommander.Action("Telescope:MotorOff", "");
            return new Response<bool>(true, "");
        }

        public Response<bool> MLTPStop()
        {
            this.mountCommander.SendCommandBool("DelOldLpt", true);
            return new Response<bool>(true, "");
        }

        public Response<bool> MLTPSend(string json)
        {
            this.mountCommander.Action("telescope:sendmlptpointings", json);
            return new Response<bool>(true, "");
        }

        public Response<bool> CoverOpen()
        {
            this.mountCommander.Action("Telescope:OpenCover", "");
            return new Response<bool>(true, "");
        }

        public Response<bool> CoverClose()
        {
            this.mountCommander.Action("Telescope:CloseCover", "");
            return new Response<bool>(true, "");
        }

        public Response<string> ErrorString()
        {
            string rc = this.mountCommander.SendCommandString("GetTelStatus", true);
            return new Response<string>(rc, rc);
        }

        public Response<string> AutoslewVersion()
        {
            string rc = this.mountCommander.SendCommandString("GetVersion", true);
            return new Response<string>(rc, rc);
        }

        public Response<double> MLPTTimeLeft()
        {
            string rc = this.mountCommander.SendCommandString("MLPTTimeLeft", true);
            double result = 0;
            rc = rc.Replace(',', '.');
            if (double.TryParse(rc, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return new Response<double>(result, rc);
            }
            return new Response<double>(0, rc);
        }

        public Response<double> TimeToLimit()
        {
            string rc = "0";
            try
            {
                rc = this.mountCommander.SendCommandString("TimeToLimit", true);
            }
            catch (Exception ex)
            {
                Logger.Error($"CommandString TimeToLimit: {ex.Message}");
            }
            Logger.Debug($"TimeToLimit: {rc}");
            double result = 0;
            rc = rc.Replace(',', '.');
            if (double.TryParse(rc, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return new Response<double>(result, rc);
            }
            return new Response<double>(0, rc);
        }

        public Response<bool> FansOn(int strength = 9)
        {
            this.mountCommander.Action("Telescope:StartFans", strength.ToString());
            return new Response<bool>(true, "");
        }

        public Response<bool> FansOff()
        {
            this.mountCommander.Action("Telescope:StopFans", "");
            return new Response<bool>(true, "");
        }

        public Response<bool> SetHumidity(double humidity)
        {
            // standard is 0.7. Expect move of mount because change is updated immediatly in refraction model
            this.mountCommander.Action("refracthumidity", (humidity / 100).ToString());
            return new Response<bool>(true, "");
        }

        public Response<bool> SetTemperature(double temperature)
        {
            // Expect move of mount because change is updated immediatly in refraction model
            this.mountCommander.Action("refracttemperature", (temperature).ToString());
            return new Response<bool>(true, "");
        }

        public Response<bool> SetPressure(double pressure)
        {
            // this is the real pressure at the site, not the reduced MSL pressure !
            this.mountCommander.Action("refractpressure", (pressure).ToString());
            return new Response<bool>(true, "");
        }

        public Response<double> MeridianFlipMaxAngle()
        {
            string rc = "0";
            try
            {
                rc = this.mountCommander.SendCommandString("MeridianFlipMaxAngle", true);
            }
            catch (Exception ex)
            {
                Logger.Error($"CommandString MeridianFlipMaxAngle: {ex.Message}");
            }

            Logger.Info($"MeridianFlipMaxAngle: {rc}");
            
            double result = 0;
            var normalizedResponse = rc?.Trim().TrimEnd('#').Replace(',', '.');
            if (double.TryParse(normalizedResponse, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return new Response<double>(result, rc);
            }

            Logger.Warning($"Failed to parse MeridianFlipMaxAngle from response '{rc}'");
            return new Response<double>(0, rc);
        }

        public Response<bool> LoadModel(string name)
        {
            string command = $":modelld0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public Response<bool> SaveModel(string name)
        {
            string command = $":modelsv0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public Response<bool> DeleteModel(string name)
        {
            string command = $":modeldel0{name}#";
            // returns 1# on success, and 0# on failure
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = rawResponse == "1#";
            return new Response<bool>(result, rawResponse);
        }

        public void DeleteAlignment()
        {
            /*
            const string command = ":delalig#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            if (!string.IsNullOrWhiteSpace(rawResponse.TrimEnd('#')))
            {
                throw new Exception($"Failed to delete alignment. {command} returned {rawResponse}");
            }
            */
        }

        public Response<bool> StartNewAlignmentSpec()
        {
            return new Response<bool>(true, String.Empty);
        }

        public Response<bool> FinishAlignmentSpec()
        {
            return new Response<bool>(true, String.Empty);
        }

        public Response<bool> Shutdown()
        {
            return new Response<bool>(true, "");
        }

        public Response<int> AddAlignmentPointToSpec(
            double mountRightAscension,
            double mountDeclination,
            PierSide sideOfPier,
            double plateSolvedRightAscension,
            double plateSolvedDeclination,
            double localSiderealTime)
        {
            /*
            var mountRightAscensionRounded = mountRightAscension.RoundTenthSecond();
            var mountDeclinationRounded = mountDeclination.RoundSeconds();
            var plateSolvedRightAscensionRounded = plateSolvedRightAscension.RoundTenthSecond();
            var plateSolvedDeclinationRounded = plateSolvedDeclination.RoundSeconds();
            var siderealTimeRounded = localSiderealTime.RoundTenthSecond();

            var commandBuilder = new StringBuilder();
            commandBuilder.Append(":newalpt");
            commandBuilder.Append($"{mountRightAscensionRounded.Hours:00}:{mountRightAscensionRounded.Minutes:00}:{mountRightAscensionRounded.Seconds:00}.{mountRightAscensionRounded.HundredthSeconds / 10:0},");
            commandBuilder.Append($"{(mountDeclinationRounded.Positive ? "+" : "-")}{mountDeclinationRounded.Degrees:00}:{mountDeclinationRounded.Minutes:00}:{mountDeclinationRounded.Seconds:00},");
            switch (sideOfPier)
            {
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
            if (rawResponse == "E#")
            {
                throw new Exception($"Failed to add alignment point using {command}");
            }

            var numPoints = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            */

            //TODO Add to list

            //            this.modelPoints.Add();

            return new Response<int>(1, String.Empty);
        }

        public Response<string> GetId()
        {
            const string command = ":GETID#";
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            return new Response<string>(rawResponse.TrimEnd('#'), rawResponse);
        }

        public void SetUltraPrecisionMode()
        {
            const string command = ":U2#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public Response<int> GetMeridianSlewLimitDegrees()
        {
            const string command = ":Glms#";

            // Returns limit followed by #
            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<int>(result, rawResponse);
        }

        public Response<bool> SetMeridianSlewLimit(int degrees)
        {
            string command = $":Slms{degrees:00}#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<decimal> GetSlewSettleTimeSeconds()
        {
            const string command = ":Gstm#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result, rawResponse);
        }

        public Response<bool> SetSlewSettleTime(decimal seconds)
        {
            if (seconds < 0 || seconds > 99999)
            {
                return new Response<bool>(false, "");
            }

            var command = $":Sstm{seconds:00000.000}#";
            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<MountStatusEnum> GetStatus()
        {
            const string command = ":Gstat#";

            var rawResponse = this.mountCommander.SendCommandString(command, true);
            var result = int.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<MountStatusEnum>((MountStatusEnum)result, rawResponse);
        }

        public Response<bool> GetUnattendedFlipEnabled()
        {
            const string command = ":Guaf#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<decimal> GetTrackingRateArcsecsPerSec()
        {
            const string command = ":GT#";

            // Needs to be divided by 4 to get arcsecs/sec, according to spec
            var rawResponse = this.mountCommander.SendCommandString(command, true);

            var result = decimal.Parse(rawResponse.TrimEnd('#'), CultureInfo.InvariantCulture);
            return new Response<decimal>(result / 4, rawResponse);
        }

        public void SetUnattendedFlip(bool enabled)
        {
            var command = $":Suaf{(enabled ? 1 : 0)}#";

            this.mountCommander.SendCommandBlind(command, true);
        }

        private static readonly Version ultraPrecisionMinimumVersion = new Version(2, 10, 0);

        public void SetMaximumPrecision(ProductFirmware productFirmware)
        {
            if (productFirmware.Version > ultraPrecisionMinimumVersion)
            {
                this.SetUltraPrecisionMode();
            }
            else
            {
                // The ASA ASCOM driver uses this logic
                Logger.Warning($"Firmware {productFirmware.Version} too old to support ultra precision. Falling back to AP emulation mode");
                this.mountCommander.SendCommandBlind(":EMUAP#:U#", true);
            }
        }

        public Response<ProductFirmware> GetProductFirmware()
        {
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
            if (!DateTime.TryParseExact(firmwareTimestampString, firmwareDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var firmwareTimestamp))
            {
                throw new Exception($"Failed to parse firmware timestamp {firmwareTimestampString}");
            }

            var firmwareVersionResponse = this.mountCommander.SendCommandString(firmwareVersionCommand, true);
            if (!Version.TryParse(firmwareVersionResponse.TrimEnd('#'), out var firmwareVersion))
            {
                throw new Exception($"Failed to parse firmware version {firmwareVersionResponse}");
            }

            return new Response<ProductFirmware>(
                new ProductFirmware(productName, firmwareTimestamp.ToUniversalTime(), firmwareVersion),
                $"{productRawResponse}\n{firmwareDateRawResponse}\n{firmwareTimeRawResponse}\n{firmwareVersionResponse}"
            );
        }

        public Response<bool> DeleteAlignmentStar(int alignmentStarIndex)
        {
            var rawResponse = "";
            var result = true;
            return new Response<bool>(result, rawResponse);
        }

        public Response<decimal> GetPressure()
        {
            decimal result = 1000;
            string rawResponse = "1000";
            return new Response<decimal>(result, rawResponse);
        }

        public Response<decimal> GetTemperature()
        {
            const string command = ":GRTMP#";

            var rawResponse = "10";
            decimal result = 10;
            return new Response<decimal>(result, rawResponse);
        }

        public Response<bool> GetRefractionCorrectionEnabled()
        {
            bool response = true;
            return new Response<bool>(response, "");
        }

        private static readonly string[] DATE_FORMATS = {
            "yyyy-MM-dd",
            "MM/dd/yy",
            "MM:dd:yy"
        };

        public Response<DateTime> GetUTCTime()
        {
            DateTime result = DateTime.UtcNow;
            var rawResponse = String.Empty;
            return new Response<DateTime>(result, rawResponse);
        }

        public void SetSiderealTrackingRate()
        {
            //   const string command = ":TQ#";
            //   this.mountCommander.SendCommandBlind(command, true);
        }

        public void SetLunarTrackingRate()
        {
            //const string command = ":TL#";
            //this.mountCommander.SendCommandBlind(command, true);
        }

        public void SetSolarTrackingRate()
        {
            //   const string command = ":TSOLAR#";
            //   this.mountCommander.SendCommandBlind(command, true);
        }

        public void StopTracking()
        {
            const string command = ":AL#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public void StartTracking()
        {
            const string command = ":AP#";
            this.mountCommander.SendCommandBlind(command, true);
        }

        public Response<bool> SetRefractionCorrection(bool enabled)
        {
            var command = $":SREF{(enabled ? 1 : 0)}#";
            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<MountIP> GetIPAddress()
        {
            const string command = ":GIP#";

            var response = this.mountCommander.SendCommandString(command, true);
            return MountResponseParser.ParseIP(response);
        }

        public Response<string> GetMACAddress()
        {
            const string command = ":GMAC#";

            var response = this.mountCommander.SendCommandString(command, true);
            return new Response<string>(response.TrimEnd('#'), response);
        }

        public Response<bool> GetDualAxisTrackingEnabled()
        {
            const string command = ":Gdat#";

            var response = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(response, "");
        }

        public Response<bool> SetDualAxisTracking(bool enabled)
        {
            var command = $":Sdat{(enabled ? 1 : 0)}#";

            var result = this.mountCommander.SendCommandBool(command, true);
            return new Response<bool>(result, "");
        }

        public Response<bool> ForceNextPierSide(PierSide desiredPierSide)
        {
            int parameter = desiredPierSide switch
            {
                PierSide.pierEast => 0,
                PierSide.pierWest => 1,
                _ => -1
            };

            var rawParameter = parameter.ToString(CultureInfo.InvariantCulture);
            try
            {
                this.mountCommander.Action("forcenextpierside", rawParameter);
                return new Response<bool>(true, rawParameter);
            }
            catch (Exception ex)
            {
                Logger.Warning($"ASCOM action forcenextpierside({rawParameter}) failed: {ex.Message}");
                return new Response<bool>(false, ex.Message);
            }
        }
    }
}