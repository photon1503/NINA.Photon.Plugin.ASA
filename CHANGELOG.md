# Changelog


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

