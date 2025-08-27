
# Changelog

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
