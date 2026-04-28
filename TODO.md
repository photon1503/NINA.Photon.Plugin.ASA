
1 full-sky-model: add option to slew to correct pierside before start

  The build logic now runs a one-time startup phase before normal traversal in ModelBuilder.cs:337. That startup phase reuses the existing traversal ordering to pick the first real build point, optionally forces its pier side, and optionally slews to the matching Sync East/West coordinates, captures once, solves once, and adds that result through the existing alignment-build path in ModelBuilder.cs:887. Failure behavior is warn-and-continue.
  
2 full-sky-model: add option to platesolve & sync before start
3 full-sky-model: add option to enable "coordinates sync" in NINA (in order to make a real sync to the mount)
4 add option to use dedicated plate solve settings (exp.time, binning, gain, offset) for full-sky-model build and MLPT