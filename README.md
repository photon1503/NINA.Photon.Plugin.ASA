# ASA Model Builder NINA Plugin

This project was forked from the TenMicron plugin by ghilios at https://github.com/ghilios/NINA.Joko.Plugin.TenMicron


Many thanks for the great work!

## ASA specific modifications
- Removed 10u specific implementations. This version will basically connect to any telescope supported by NINA
- Removed Mount info
- Instead of writing the plate solved points directly to the mount, a POX file will be created

## Known issues

- Telescope needs to be connected before starting the model builder

## TODOs
- [ ] Remove Mount model, since this is 10u specific
- [ ] Add option for destination path of POX file
- [ ] Dynamically create filename
- [ ] Remove sidereal path option
- [ ] Fix errors :-)
