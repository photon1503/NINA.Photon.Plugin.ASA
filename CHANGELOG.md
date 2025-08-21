# Changelog

## 3.2.5.x
 - Sending MLPT data will now wait until the mount has restored its position.
 - Fixed bug where the progress bar was not closed correctly.
	
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

