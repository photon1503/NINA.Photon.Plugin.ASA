
# Changelog

## 3.2.7.x

### POX / Export

- Fixed POX numbering text for non-legacy DDM output to increment correctly per exported point.

### MLPT Diagnostics Charts

- Added MLPT diagnostics chart option: **Relative** mode (anchors first solved point at 0 and shows subsequent RA/DE offsets relative to that baseline).
- Added MLPT diagnostics chart option: **Match Y-axis scale** (forces RA/DE charts to use the same Y-axis limit based on the larger error range).

### AutoGrid / Pathing / Pier Side

- AutoGrid now generates dual-side coverage around the meridian (ASA-style overlap), storing a desired pier side per point and duplicating overlap points for both east/west pier solutions.
- Dual-side overlap points are generated via separate east/west sampling in the meridian overlap zone (ASA-style), improving distribution.
- Improved AutoGrid near-horizon coverage for sparse rings by anchoring candidate placement to the meridian and selecting a horizon-aware phase with best visible distribution.
- Added ASA pier-side aware slew support in model building by calling ASCOM action `forcenextpierside` before slews (`0` east, `1` west, `-1` auto/reset), with automatic fallback if unsupported by the active driver.
- Model slews now enforce each point's stored desired pier side via ASCOM `forcenextpierside` before slewing.

### Chart UI

- Added east/west pier-side coloring for generated points in both model builder charts.
- Added celestial pole target marker (**NCP/SCP**) to both charts with hemisphere-aware label.
- Added chart option to show/hide celestial pole marker.
- Updated mount position marker styling to a bright orange target-style marker (circle + crosshair) in both charts.
- Added chart option to show/hide meridian limits in charts.
- Made meridian limits chart visibility option persistent in plugin settings.

### Horizon Constraint

- Added a global **Minimum distance to horizon (°)** setting to keep generated points safely above the horizon.
- Applied this limit consistently in point visibility/state checks and exposed it directly in chart options.


## 3.2.7.1

 - Fixed UI freeze when starting full sky model build
  
## 3.2.7.0

- Added **AutoGrid** as a first-class generator option and reordered generator list to: AutoGrid, MLPT, Golden Spiral.
- Added selectable AutoGrid path ordering modes (legacy sweep vs ASA-style band traversal).
- Added AutoGrid path preview toggle (**Show path**) with dotted path rendering in both main and radial/top-view plots.
- Added AutoGrid input mode UX refinements:


- Added MLPT diagnostics charts for RA/DE error progression (Image # vs arcsec).
- Selecting MLPT generator now auto-imports current scope RA/Dec coordinates (same behavior as the scope import button).

## 3.2.6.4 (2025-11-29)

- Added new trigger *Mount Dither*. This trigger always uses direct guider, even if another guider is selected in NINA.

## 3.2.6.3 (2025-11-27)

 - Updated dependencies to NINA 3.2
 - Fixed *MLPT If Exceeds* Trigger not working properly in some conditions
- Removed *Solve Folder*

## 3.2.6.1 (2025-09-06)
- Added Triger to flip the ASA rotator by 180° if it is approaching the hardware limits.

## 3.2.5.12 (2025-09-01)
- Fixed crashing NINA when using MLPT Start with invalid coordinates in the sequence
- Sync options are now collapsed by default in the ASA Model Builder UI
- Grouped all sequence items and triggers by ASA Tools (MLPT) and others
- Added info to tooltips for Fans and Covers that these are only for Autoslew, not ACC


## 3.2.5.10 (2025-08-28)
 - MLPT After Flip does not work on all NINA version.
   On system where we do not get an event after a meridian flip, we now use a workaround to manually detect meridan flips.
	
	
## 3.2.5.9 (2025-08-27)
 - Removed unecessary notification when using model builder with a dome.

## 3.2.5.8
 - Fixed MLPT After Flip trigger not showing up on some systems
 - MLPT will now always get the DSO target coordinates instead of the current mount position.
 - Avoid unnessary MLPT calculation and logs

## 3.2.5.7
 - Ensure we always get at least 3 points for MLPT by decreasing the RA interval.
This helps to get a valid MLPT path also when the mount is near a limit (meridian, horizon).
	
## 3.2.5.6
 - Fixed model builder not waiting for dome to finish slew

## 3.2.5.1
 - **NEW** Added Relax Slew trigger
	
	- Declination (Dec) Relaxation
			
	The direction of Dec adjustment is based on the current altitude and the observatory's hemisphere.
    If the current altitude is below 45�, Dec is adjusted towards the zenith (increased in the northern hemisphere, decreased in the southern hemisphere).
    If the current altitude is above 45�, Dec is adjusted away from the zenith (decreased in the northern hemisphere, increased in the southern hemisphere).
    Dec is always clamped to a safe range to avoid zenith/gimbal lock.
	
	- Right Ascension (RA) Relaxation
	
    The RA adjustment is based on the current hour angle (HA), ensuring the slew always moves the telescope further from the meridian (HA = 0h) and never crosses the meridian or anti-meridian (HA = �12h).
    The new hour angle is calculated by moving further from the meridian, but is clamped to stay within (-12h, +12h).
    The new RA is then computed from the local sidereal time and the new hour angle, and normalized to the [0h, 24h) range.

	- Hemisphere Independence
	
    The logic automatically adapts to both northern and southern hemispheres by using the sign of the site latitude for Dec and geometric hour angle for RA.

	- Safety Checks
	
	The resulting relax point is checked to ensure it remains above a minimum altitude (e.g., 5� above the horizon) before slewing
	
 - **NEW** Added Re-center option to all MLPT triggers. This will start a Solve & Center after MLPT.
 - Sending MLPT data will now wait until the mount has restored its position.
 - Redesigned UI elements for all triggers to improve consistency.
 - Removed number of estimated points for all MLPT triggers
 - Fixed bug where the progress bar was not closed correctly.
 - Fixed MLPT After Flip trigger 
	
	
## 3.2.4.3
 - Added sequence item to send weather data to Autoslew for refraction correction
 - Added option to define indivdual path for POX file generation (bastiaigner)
	
## 3.2.4.2
 - Disable (and re-enable) autoguiding during MLPT building process
   
## 3.2.4.1
 - Added sequence item to send weather data to Autoslew for refraction correction
   
## 3.2.4.0
 - Added trigger to send weather data to Autoslew for refraction correction

## 3.2.x

 - Added support for MLPT for upcoming Autoslew 7
 - Fixed dome azimut calculation for EQ piers
 - renamed to ASA Tools

## 3.1.3.0
 - Added instructions for ASA fans and cover
  
## 3.1.2.0
 - Added Sequence items to power the ASA DDM motors on and off

## 3.1.1.0
 - Fixed POX file generation
	
## 3.1.0.0
 - Transform points for POX file to J2000
	
## 3.0.2.2
 - Added reference points
	
## 3.0.2.1

- Fixed number of total points in the .pox file
- Added option to create sync points
- Added option to create additional points for high altitudes

## [Beta 4] - 2023-10-23

Changed:
- Increased MAX_POINTS to allow more than 100 points
- Removed disableRefractionCorrection option from settings. ASA DDM has RefractionCorrection enabled permanently.
 
Fix:
- Fixed Progress status bar
- Fixed Telescope position in plot
- Fixed live plot update
- Fixed IsLegacyDDM option
- Fixed Error "Object reference not set to an instance ..." after successful model build

### [Beta 3] - 2023-10-22

Features:
- Added option for non-legacy mounts. The generated POX file looks slightly different.

Fix:
- Convert plate solved result to JNOW
