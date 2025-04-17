#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.WPF.Base.Mediator;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Core.Enum;
using System;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Equipment.Interfaces;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace NINA.Photon.Plugin.ASA.Equipment
{
    public class MountMediator : Mediator<IMountVM>, IMountMediator
    {
        protected List<IMountConsumer> consumers = new List<IMountConsumer>();

        public void RegisterConsumer(IMountConsumer consumer)
        {
            lock (consumers)
            {
                consumers.Add(consumer);
            }
            if (handler != null)
            {
                var info = handler.GetDeviceInfo();
                consumer.UpdateDeviceInfo(info);
            }
        }

        public void RemoveConsumer(IMountConsumer consumer)
        {
            lock (consumers)
            {
                consumers.Remove(consumer);
            }
        }

        public CoordinateAngle GetMountReportedDeclination()
        {
            return handler.GetMountReportedDeclination();
        }

        public AstrometricTime GetMountReportedLocalSiderealTime()
        {
            return handler.GetMountReportedLocalSiderealTime();
        }

        public AstrometricTime GetMountReportedRightAscension()
        {
            return handler.GetMountReportedRightAscension();
        }

        public bool SetTrackingRate(TrackingMode trackingMode)
        {
            return handler.SetTrackingRate(trackingMode);
        }

        public bool Shutdown()
        {
            return handler.Shutdown();
        }

        public Task<bool> PowerOn(CancellationToken ct)
        {
            return handler.PowerOn(ct);
        }

        public MountInfo GetInfo()
        {
            if (handler == null)
            {
                return null;
            }
            return handler.GetDeviceInfo();
        }

        public void Broadcast(MountInfo deviceInfo)
        {
            lock (consumers)
            {
                foreach (IMountConsumer c in consumers)
                {
                    c.UpdateDeviceInfo(deviceInfo);
                }
            }
        }
    }
}