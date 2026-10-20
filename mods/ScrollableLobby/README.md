# ScrollableLobby

Lots of modded survivors? The character select screen normally keeps adding icons until they spill over the
rest of the lobby. This mod keeps the survivor list in a tidy grid of a fixed size and lets you scroll through it.

- Shows **3 rows** at a time (configurable); scroll with the **mouse wheel** one row per notch, or drag the thin scrollbar.
- **Keyboard / gamepad**: moving the selection off the visible rows scrolls the list to follow it.
- Opens scrolled to the survivor you last played.
- Empty filler slots are trimmed so only the last row gets padded.
- Client side only. Friends in your lobby don't need it.

## Settings

`BepInEx/config/com.VincentMacaluso.ScrollableLobby.cfg` (or r2modman's Config editor):

| Setting | Default | What it does |
|---|---|---|
| Columns | 0 | Icons per row. 0 = keep the game's own (8 if the game lays icons out by rows). |
| Visible Rows | 3 | Rows shown at once. |
| Icon Scale | 1.0 | Icon size, 0.5 to 1.5. Smaller fits more on screen. |
| Rows Per Scroll | 1 | Rows moved per wheel notch. |
| Smooth Scroll | true | Glide between rows instead of jumping. |
| Show Scrollbar | true | Thin scrollbar beside the icons when needed. |
