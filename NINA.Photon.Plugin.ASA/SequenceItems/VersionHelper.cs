#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems
{
    using System;

    public class VersionHelper
    {
        public static string VersionString { get; set; }

        public VersionHelper()
        {
            VersionString = string.Empty;
        }

        public static bool IsOlderVersion(string versionA, string versionB)
        {
            Version vA = ParseVersion(versionA);
            Version vB = ParseVersion(versionB);
            return vA < vB;
        }

        private static Version ParseVersion(string version)
        {
            string[] parts = version.Split('.');
            if (parts.Length > 4)
            {
                throw new ArgumentException("Version cannot have more than 4 components.");
            }

            int[] intParts = new int[4] { 0, 0, 0, 0 }; // Initialize with zeros
            for (int i = 0; i < parts.Length; i++)
            {
                intParts[i] = int.Parse(parts[i]);
            }

            return new Version(intParts[0], intParts[1], intParts[2], intParts[3]);
        }
    }
}