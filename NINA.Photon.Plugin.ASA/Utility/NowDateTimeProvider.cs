#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.Utility.DateTimeProvider;
using System;

namespace NINA.Photon.Plugin.ASA.Utility {

    [JsonObject(MemberSerialization.OptIn)]
    public class NowDateTimeProvider : IDateTimeProvider {

        public NowDateTimeProvider(ICustomDateTime dateTime) {
            this.DateTime = dateTime;
        }

        public string Name { get; } = "Now";
        public ICustomDateTime DateTime { get; private set; }

        public DateTime GetDateTime(ISequenceEntity context) {
            return DateTime.Now;
        }
    }
}