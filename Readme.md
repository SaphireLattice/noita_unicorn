A util to mess with PRNG of Noita and unpack the `data.wak` file.

Usage (add `beta` before command to use beta compatible prng, e.g. `beta wak data/data.wak`):
 - `wak [file]` - unpack a data.wak file into current directory. **Drag and drop supported!** Just drop the file on the `.exe`!
 - `recipe [seed]` - get AP and LC recipes, might not work with `beta` option
 - `rng [seed]` - get next rng value for given (internal) state, plus IV for given counter

Application should be OS agnostic as it only passes paths to C#. Build with `dotnet build`.
