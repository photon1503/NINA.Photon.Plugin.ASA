#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Equipment.Equipment;
using NINA.Photon.Plugin.ASA.Model;
using System.Collections.Immutable;

namespace NINA.Photon.Plugin.ASA.Equipment {

    public class MountModelInfo : DeviceInfo {
        public ImmutableList<string> ModelNames { get; set; }

        public LoadedAlignmentModel LoadedAlignmentModel { get; set; }
    }
}