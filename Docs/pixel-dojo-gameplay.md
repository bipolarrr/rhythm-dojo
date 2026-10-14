# Pixel dojo gameplay

Gameplay uses the same supplied dojo backdrop and PixelDojoFrameGraphic as Title and SongSelection. The backdrop canvas renders through the gameplay camera at a distance of 100, behind the world playfield and notes, without a GraphicRaycaster. A dim scenery layer and dark central training frame preserve note contrast.

The authoritative GameplayHudLayoutBuilder calls PixelGameplayStyle.Apply. The song and judgment-count cards use paper and dark ink; combo, health and recent judgment use a dark wooden panel with warm cream, sage, amber and red feedback. RhythmHud selects that palette for pixelTheme views. Existing chart, camera, timing, note colors, hit feedback, input, health and restart behavior remain intact.
