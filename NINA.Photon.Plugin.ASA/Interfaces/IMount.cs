#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Model;
using System;

namespace NINA.Photon.Plugin.ASA.Interfaces
{
    public interface IMount
    {
        Response<CoordinateAngle> GetDeclination();

        Response<AstrometricTime> GetRightAscension();

        Response<AstrometricTime> GetLocalSiderealTime();

        Response<int> GetModelCount();

        Response<string> GetModelName(int modelIndex);

        Response<bool> LoadModel(string name);

        Response<bool> SaveModel(string name);

        Response<bool> DeleteModel(string name);

        void DeleteAlignment();

        Response<bool> DeleteAlignmentStar(int alignmentStarIndex);

        Response<int> GetAlignmentStarCount();

        Response<AlignmentStarInfo> GetAlignmentStarInfo(int alignmentStarIndex);

        Response<AlignmentModelInfo> GetAlignmentModelInfo();

        Response<bool> StartNewAlignmentSpec();

        Response<bool> FinishAlignmentSpec();

        //Response<PierSide> GetSideOfPier();

        Response<int> AddAlignmentPointToSpec(
            double mountRightAscension,
            double mountDeclination,
            PierSide sideOfPier,
            double plateSolvedRightAscension,
            double plateSolvedDeclination,
            double localSiderealTime);

        Response<string> GetId();

        void SetUltraPrecisionMode();

        Response<ProductFirmware> GetProductFirmware();

        Response<int> GetMeridianSlewLimitDegrees();

        Response<decimal> GetSlewSettleTimeSeconds();

        Response<bool> SetSlewSettleTime(decimal seconds);

        Response<MountStatusEnum> GetStatus();

        Response<bool> GetUnattendedFlipEnabled();

        Response<decimal> GetTrackingRateArcsecsPerSec();

        void SetUnattendedFlip(bool enabled);

        void SetMaximumPrecision(ProductFirmware productFirmware);

        Response<bool> SetMeridianSlewLimit(int degrees);

        Response<decimal> GetPressure();

        Response<decimal> GetTemperature();

        Response<bool> GetRefractionCorrectionEnabled();

        Response<bool> Shutdown();

        Response<bool> PowerOn();
        Response<bool> PowerOff();

        Response<bool> GetDualAxisTrackingEnabled();

        Response<bool> SetDualAxisTracking(bool enabled);

        Response<DateTime> GetUTCTime();

        Response<MountIP> GetIPAddress();

        Response<string> GetMACAddress();

        void SetSiderealTrackingRate();

        void SetLunarTrackingRate();

        void SetSolarTrackingRate();

        void StopTracking();

        void StartTracking();

        Response<bool> SetRefractionCorrection(bool enabled);
    }
}