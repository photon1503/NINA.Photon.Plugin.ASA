Improve MLPT run-state tracking instead of relying mostly on local timing assumptions. Right now the new chart bar is driven from the send timestamp plus planned path duration, but the plugin already queries remaining controller time in MLPTifNecessary.cs:717. A stronger implementation would expose actual MLPT active/remaining state centrally and let the UI, triggers, and stop logic all read the same source.

Add an option to pause or disable guiding during MLPT builds. This is already the only item in TODO.md:1, and it makes operational sense because MLPT is itself building the tracking correction. This is probably the cleanest next feature because it is focused, user-visible, and already clearly wanted.

Save failed MLPT/model-build points and their solve images for diagnosis. That is explicitly called out in ASAPlugin.cs:38. For a plugin like this, debugging bad nights is a big deal, and a “failed points bundle” would make troubleshooting much easier than reading logs alone.

Fix the MLPT timestamps to use actual exposure midtime instead of the current placeholder path for payload generation. There is still a TODO in ModelBuilder.cs:647 saying UTCDate is not ideal. If the controller benefits from more accurate timestamps, this is one of the few changes that could directly improve model quality, not just UX.

Replace the current path-order heuristics with a true distance-optimized solver for model builds. That is also in ASAPlugin.cs:36. AutoGrid is already strong, but if you can reduce slew distance without hurting band logic or pier-side constraints, that would shorten builds and reduce dome churn.

Implement overlapping pipeline execution so image download does not block moving to the next point. That is the top long-term note in ASAPlugin.cs:35. This is probably the highest performance win, but also the riskiest because it touches capture, solver timing, mount sequencing, and failure handling.

Finish the mount model management surface instead of leaving stubs in MountModelVM.cs:643. There are multiple NotImplementedExceptions there, which suggests there is a partially-built feature area around model browsing/management. Completing that would make the plugin feel more cohesive.

Add richer MLPT diagnostics in the UI. The new vertical bar is a good first step, but it would also make sense to show planned duration, elapsed time, remaining controller time, and whether the bar is “predicted” or “live from controller.” That would make the charts much more actionable during a run.

--- 
For ASA mounts specifically, I’d look beyond MLPT and focus on features that reduce operator mistakes, shorten setup time, or exploit ASA-specific behavior that generic NINA plugins do not cover well.

A mount health panel would make sense: temperatures, motor state, tracking mode, current refraction inputs, active model name, active MLPT state, pier side, meridian margin, and any AutoSlew warning/fault state in one place. ASA systems expose enough mount-specific state that this would be more useful than a generic telescope summary.

A “preflight validator” for ASA nights would be valuable. Before a sequence starts, check things like AutoSlew version, active model loaded, refraction settings, JNow/J2000 mismatch, sync-as-alignment misuse, dome control conflicts, minimum flip distance, and whether MLPT-capable firmware is present. This is the kind of thing that prevents bad nights.

Model lifecycle tooling would be strong for ASA mounts: list models, show loaded model metadata, compare build dates, archive/export POX artifacts, flag stale models after site changes, and give a guided “replace current model safely” flow. ASA users care more about model management than most mount users.

A polar-alignment or collimation helper based on small targeted model runs could fit well. The current builder already has the mechanics for point acquisition and solving; a specialized workflow for “quick diagnostic model” versus “full model” would be practical.

Better dome-awareness would likely pay off. ASA users with domes often hit the messy edge cases: shutter width, slew ordering, unexpected dome lag, pier-side expectations. Features like dome conflict prediction, dome path preview, or “this planned build will cause a large dome reversal here” would be very ASA-specific and useful.

Refraction management could be expanded. Right now there is weather update support, but an ASA-focused feature could show whether current pressure/temperature/humidity data is stale, when refraction was last pushed, and whether conditions changed enough to justify a refresh before the next target.

A mount hysteresis / backlash diagnostic tool could be interesting even for direct-drive systems, especially if you already have the relax-slew concept. A short guided measurement routine could quantify repeatability after return slews, east/west asymmetry, or changes across altitude bands.

A “safe target handoff” workflow would help multi-target nights. When leaving one target, the plugin could optionally stop MLPT, clear/verify local corrections, perform a validation solve on the new target, then prompt or auto-run the next local correction workflow. ASA mounts benefit from explicit state transitions.

Better AutoSlew integration around faults and recovery would be worth it. For example: if the mount enters an error state, expose probable causes and one-click recovery actions where safe, or at least structured guidance rather than a raw driver error.

An ASA-specific performance report after a session or build would be useful: solve success rate, average RMS, failure clusters by altitude/azimuth, sync-point effect, dome wait overhead, median solve time, and how much time was lost to retries. That would make tuning much easier.