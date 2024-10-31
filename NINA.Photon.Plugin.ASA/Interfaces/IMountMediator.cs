#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Equipment.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Interfaces;

namespace NINA.Photon.Plugin.ASA.Interfaces
{
    public interface IMountMediator : IMediator<IMountVM>
    {
        CoordinateAngle GetMountReportedDeclination();

        AstrometricTime GetMountReportedRightAscension();

        AstrometricTime GetMountReportedLocalSiderealTime();

        bool SetTrackingRate(TrackingMode trackingMode);

        bool Shutdown();

        Task<bool> PowerOn(CancellationToken ct);

        MountInfo GetInfo();

        void RegisterConsumer(IMountConsumer consumer);

        void RemoveConsumer(IMountConsumer consumer);

        void Broadcast(MountInfo deviceInfo);
    }
}