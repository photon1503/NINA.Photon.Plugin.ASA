 plugin for [N.I.N.A. (Nighttime Imaging 'N' Astronomy)](https://nighttime-imaging.eu/) that provides support for ASA (Astro Systeme Austria) mounts.

## Features

###  Sequence Instructions
- **Motor control**: Power on/off mount motors
- **Fan control**: Manage cooling fans on/off

### Model Building
Build an accurate sky model to improve your mount's pointing accuracy:
- Automatically captures images of the sky
- Plate solves each image position
- Generates a POX file compatible with Autoslew

### MLTP 
Advanced path modeling for precise tracking:
- Continuous sky imaging and plate solving
- Real-time data transmission to Autoslew (requires Autoslew 7.1.5.x or higher)

## Installation
1. Download the latest release from the [Releases page](https://github.com/photon1503/NINA.Photon.Plugin.ASA/releases)
2. Place the plugin DLL in N.I.N.A.'s Plugins directory
3. Restart N.I.N.A.

## Requirements
- N.I.N.A. version 3.2 or higher
- ASA DDM mount
- Autoslew 5.2.4.8 or higher
- Plate solving software configured in N.I.N.A.

## Acknowledgements & References
This project was forked from the excellent TenMicron Project by ghilios:
- Original project: [NINA.Joko.Plugin.TenMicron](https://github.com/ghilios/NINA.Joko.Plugin.TenMicron)
- Special thanks to Philipp Keller and Wolfgang Promper from ASA for their support