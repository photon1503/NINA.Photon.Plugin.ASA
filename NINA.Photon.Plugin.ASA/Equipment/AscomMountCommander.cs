#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Com.DriverAccess;
using NINA.Photon.Plugin.ASA.Exceptions;
using NINA.Photon.Plugin.ASA.Interfaces;

namespace NINA.Photon.Plugin.ASA.Equipment
{
    public class AscomMountCommander : IMountCommander
    {
        private readonly Telescope ascomTelescope;

        public AscomMountCommander(Telescope ascomTelescope)
        {
            this.ascomTelescope = ascomTelescope;
        }

        public void Action(string action, string parameters)
        {
            ascomTelescope.Action(action, parameters);
        }

        public void SendCommandBlind(string command, bool raw)
        {
            ascomTelescope.CommandBlind(command, raw);
        }

        public bool SendCommandBool(string command, bool raw)
        {
            return ascomTelescope.CommandBool(command, raw);
        }

        public string SendCommandString(string command, bool raw)
        {
            var result = ascomTelescope.CommandString(command, raw);
            if (result == null)
            {
                throw new CommandFailedException(command);
            }
            return result;
        }
    }
}