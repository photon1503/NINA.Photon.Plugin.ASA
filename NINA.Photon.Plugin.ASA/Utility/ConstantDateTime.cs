﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;

namespace NINA.Photon.Plugin.ASA.Utility {

    public class ConstantDateTime : ICustomDateTime {
        private readonly DateTime constant;

        public ConstantDateTime(DateTime constant) {
            this.constant = constant;
        }

        public DateTime Now => constant;

        public DateTime UtcNow => constant.ToUniversalTime();
    }
}