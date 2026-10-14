# Pixel dojo song selection

The song browser now sits on the left of the dojo background, with a parchment information board on the right. Rows show an index, cover, title, artist, and favorite control. Selecting a row highlights its wood frame. The board shows the real selected song title, artist, BPM, play time, configured difficulty cards, and stored records. The red Start button and Back button sit along the bottom; mode and settings remain available.

`PixelSongSelectionStyle` is applied by the authoritative `SongSelectionLayoutBuilder`. `PixelDojoFrameGraphic` draws the paper and stepped wood borders directly in UI geometry, with no replacement image generation. All decorative frames have CanvasRenderers and disable raycasts. Existing view fields, chart-selection events, favorite/sort behavior, and navigation contracts are retained.

The supplied title background is reused. A song's custom cover takes priority. The legacy generic demo cover is treated as a placeholder and presented with the dojo backdrop. Difficulty card count follows the project's existing configured profiles; no extra fictional songs, levels, scores, artists, or charts are added. Easy/Standard/Hard display as 쉬움/보통/어려움.

Regenerate the scene with **Game Tools > Scenes > Build Song Selection**. Browser integration tests cover scrolling with different row heights, favorites, sorting, real song playback, records, modal blocking, and settings return. The layout test verifies the right-side board and nonblocking decorative frames.
