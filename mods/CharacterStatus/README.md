# Character Status

Every character loads, finished or not. This mod adds a status line to the bottom of each survivor's description on the
character select screen so friends know what to expect:

- **Green, COMPLETE:** finished and working.
- **Yellow, IN PROGRESS:** playable, but expect rough edges and changes.
- **Red, EARLY:** early or untested, may be unfinished or broken.

The same colour shows as a tag on each launcher card. The status lives in `launcher/roster.json` (and each mod's
`launcher/cards/<Mod>.json`) as `"status": "green" | "yellow" | "red"`. After changing one, run `python3 release/sync_status.py`
to rewrite `status.json` here, and commit both. A survivor with no status is left as it is (the log lists them).
