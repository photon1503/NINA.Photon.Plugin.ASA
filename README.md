# ASA Model Builder NINA Plugin

A [N.I.N.A](https://nighttime-imaging.eu/) plugin for building a telescope model for ASA mounts.

This project was forked from the TenMicron plugin by [ghilios](https://github.com/ghilios) at https://github.com/ghilios/NINA.Joko.Plugin.TenMicron
Many thanks for the great work!

<img width="500" alt="image" src="https://github.com/photon1503/NINA.Photon.Plugin.ASA/assets/14548927/5b8b6940-2fb1-4c91-9dcc-ca5618206fa0">

## Setup

download end extract the archive to `%localappdata%\NINA\Plugins` from https://github.com/photon1503/NINA.Photon.Plugin.ASA/releases/latest

## Features

- Generate a point model based on the golden spiral method
- Fine-tuning of the model with additional high altitude points
- Creation of sync points to improve telescope alignment and reduce hysteresis errors
- Export import model points (i.e. for exchange with Sequence)

## Usage

- Configure the settings on the plugin options page
- Always sync your telescope to a know position before starting any model build. Can by easily done by using plate solve directly in N.I.N.A
- Start building your model using the plugin.
- Load the created POX file from `%programdata%\ASA\Sequence\NINA-ASA-*.pox` into AutoSlew and calculate the model

## Changelog
[See here](https://github.com/photon1503/NINA.Photon.Plugin.ASA/blob/master/CHANGELOG.md)

### Disclaimer

All trademarks, logos and brand names are the property of their respective owners. All company, product and service names used in this website are for identification purposes only. Use of these names,trademarks and brands does not imply endorsement.

This software is in not related to ASA in any way.
