# ASA Model Builder NINA Plugin

This project was forked from the TenMicron plugin by [ghilios](https://github.com/ghilios) at https://github.com/ghilios/NINA.Joko.Plugin.TenMicron


Many thanks for the great work!

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

### 2023-10-22
-  Added option for non-legacy mounts. The generated POX file looks slightly different.

## Known issues

- Azimuth graph does not update - need to switch between (NINA) tabs to refresh
- Telescope needs to be connected before starting the model builder

## TODOs
- [x] Remove Mount model, since this is 10u specific
- [ ] Add option for destination path of POX file
- [x] Dynamically create filename
- [x] Remove sidereal path option
- [ ] Fix errors :-)
