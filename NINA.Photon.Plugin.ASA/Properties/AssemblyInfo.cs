#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin
[assembly: Guid("68fda8ef-e631-4202-ae32-b660417b75c4")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin

[assembly: AssemblyVersion("3.2.1.4")]
[assembly: AssemblyFileVersion("3.2.1.4")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("ASA Tools")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("ASA model building")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Gerald Hitz (photon)")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("ASA Tools")]
[assembly: AssemblyCopyright("Copyright © 2024")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.1085")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/photon1503/NINA.Photon.Plugin.ASA")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Mount,Model Builder,ASA,ASA DDM")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/photon1503/NINA.Photon.Plugin.ASA/blob/master/CHANGELOG.md")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/photon1503/NINA.Photon.Plugin.ASA/blob/master/NINA.Photon.Plugin.ASA/Resources/asa.png?raw=true")]

//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "https://user-images.githubusercontent.com/14548927/277363713-5b8b6940-2fb1-4c91-9dcc-ca5618206fa0.png")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://user-images.githubusercontent.com/14548927/277363713-5b8b6940-2fb1-4c91-9dcc-ca5618206fa0.png")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This plugin provides building pointing models for ASA mounts.

This project is based on @ghilios TenMicron plugin (https://github.com/ghilios/NINA.Joko.Plugin.TenMicron)
Many thanks for the great work!

# Features #

* Build full sky models using Golden Spiral in an Imaging tab dock
* MW4 and NINA horizons supported. Load the horizon file in NINA Options -> General
* Dome optimization to reduce dome slews during model build
* Export and import grids (compatible for Sequence)
* Retry model build failures using only the failed points, unless a maximum number of failures is exceeded

# Usage #
* Configure the settings on the plugin options page
* Always sync your telescope to a know position before starting any model build. Can by easily done by using plate solve directly in N.I.N.A
* Also clear your old (current) configuration in Autoslew
* Start building your model using the plugin.
* Load the created POX file from %programdata%\ASA\Sequence\NINA-ASA-*.pox into AutoSlew and calculate the model

# Getting Help #

* ASA Tools is provided 'as is' under the terms of the [Mozilla Public License 2.0](https://github.com/photon1503/NINA.Photon.Plugin.ASA/blob/develop/LICENSE.txt)
* Source code for this plugin is available at this plugin's [source code repository](https://github.com/photon1503/NINA.Photon.Plugin.ASA)
")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]
[assembly: CLSCompliant(false)]