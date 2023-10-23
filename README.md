# ASA Model Builder NINA Plugin

This project was forked from the TenMicron plugin by [ghilios](https://github.com/ghilios) at https://github.com/ghilios/NINA.Joko.Plugin.TenMicron


Many thanks for the great work!

<img width="500" alt="image" src="https://github.com/photon1503/NINA.Photon.Plugin.ASA/assets/14548927/5b8b6940-2fb1-4c91-9dcc-ca5618206fa0">

## ASA specific modifications
- Removed 10u specific implementations. This version will basically connect to any telescope supported by NINA
- Removed Mount info
- Instead of writing the plate solved points directly to the mount, a POX file will be created


## Setup

download end extract the archive to %localappdata%\NINA\Plugins from https://github.com/photon1503/NINA.Photon.Plugin.ASA/releases/latest

## Usage

- Start building your model using the plugin.
- Load the created POX file from %programdata%\ASA\Sequence\NINA-ASA-*.pox into AutoSlew

## Changelog
[See here](https://github.com/photon1503/NINA.Photon.Plugin.ASA/blob/master/CHANGELOG.md)


## Known issues

- Azimuth graph does not update - need to switch between (NINA) tabs to refresh
- Scope position not shown in graphics
- Telescope needs to be connected before starting the model builder

## TODOs
- [x] Remove Mount model, since this is 10u specific
- [x] ~~Add option for destination path of POX file~~ File will be generated at standard ASA location.
- [x] Dynamically create filename
- [x] Remove sidereal path option
- [ ] Remove "Point Max RMS" option
- [ ] Fix errors :-)

### Disclaimer

All trademarks, logos and brand names are the property of their respective owners. All company, product and service names used in this website are for identification purposes only. Use of these names,trademarks and brands does not imply endorsement.

This software is in not related to ASA in any way.
