# ASA Tools Plugin for N.I.N.A.

This plugin extends [N.I.N.A. (Nighttime Imaging 'N' Astronomy)](https://nighttime-imaging.eu/) with ASA (Astro Systeme Austria) mount workflows.

## Purpose

The plugin focuses on two core tasks:

- **All-sky model building** to improve pointing accuracy through automated image capture and plate solving.
- **MLPT workflows** for continuous tracking refinement and alignment support with Autoslew.

It also provides ASA-focused operational helpers inside N.I.N.A. for day-to-day setup and sequencing.

## Installation

1. Download the latest release from the [Releases page](https://github.com/photon1503/NINA.Photon.Plugin.ASA/releases).
2. Place the plugin DLL in the N.I.N.A. plugins directory.
3. Restart N.I.N.A.

## Requirements

- N.I.N.A. 3.2 or newer
- ASA DDM mount
- Autoslew (version compatible with your MLPT workflow)
- Plate solving configured in N.I.N.A.

## Acknowledgements

This project was forked from [NINA.Joko.Plugin.TenMicron](https://github.com/ghilios/NINA.Joko.Plugin.TenMicron).

Special thanks to Philipp Keller and Wolfgang Promper from ASA for their support.