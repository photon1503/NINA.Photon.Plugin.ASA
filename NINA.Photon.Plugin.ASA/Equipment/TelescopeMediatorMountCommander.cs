#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.Exceptions;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Core.Utility;
using System.Threading;

namespace NINA.Photon.Plugin.ASA.Equipment {

    public class TelescopeMediatorMountCommander : IMountCommander {
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IASAOptions options;
        private int commandNumber = 0;

        public TelescopeMediatorMountCommander(ITelescopeMediator telescopeMediator, IASAOptions options) {
            this.telescopeMediator = telescopeMediator;
            this.options = options;
        }

        public void SendCommandBlind(string command, bool raw) {
            var commandId = Interlocked.Increment(ref commandNumber);
            if (options.LogCommands) {
                Logger.Info($"{commandId} - Sending command: {command}, raw: {raw}");
            }
            telescopeMediator.SendCommandBlind(command, raw);
        }

        public bool SendCommandBool(string command, bool raw) {
            var commandId = Interlocked.Increment(ref commandNumber);
            if (options.LogCommands) {
                Logger.Info($"{commandId} - Sending command: {command}, raw: {raw}");
            }
            var result = telescopeMediator.SendCommandBool(command, raw);
            if (options.LogCommands) {
                Logger.Info($"{commandId} - BoolCommand result: {result}");
            }
            return result;
        }

        public string SendCommandString(string command, bool raw) {
            var commandId = Interlocked.Increment(ref commandNumber);
            if (options.LogCommands) {
                Logger.Info($"{commandId} - Sending command: {command}, raw: {raw}");
            }

            var result = telescopeMediator.SendCommandString(command, raw);
            if (result == null) {
                if (options.LogCommands) {
                    Logger.Info($"{commandId} - Command failed");
                }
                throw new CommandFailedException(command);
            }
            if (options.LogCommands) {
                Logger.Info($"{commandId} - Command response: {result}");
            }
            return result;
        }
    }
}