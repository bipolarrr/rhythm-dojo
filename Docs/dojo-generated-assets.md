# Generated gameplay assets

Generated with the built-in image_gen tool. Originals copied to `Assets/Game/Art/DojoGameplay/` as PaperPanel.png, StrikeTile.png, and WoodPanel.png. Sprite assets crop the unused margins and define nine-slice borders; the PNG originals are preserved. Bilinear filtering avoids pixelated scaling. Noto Serif KR SemiBold headings use the bundled SIL OFL license; small labels use Noto Sans KR.

The gameplay builder applies the generated images to information cards, combo panel, buttons, the playfield frame, keycaps, and the note material. All lanes share the same ivory note material without per-lane tint. Judgment/held-state feedback remains distinct.

## Final prompts

### PaperPanel

Use case: stylized-concept. Asset type: production Unity game UI panel sprite, not a screenshot or mockup. Create one front-facing rectangular wooden-and-paper panel matching a Korean martial arts rhythm dojo: reddish walnut wood, aged brass thin trim, warm ivory hanji paper inset. Painterly 2D game illustration with smooth anti-aliased edges and subtle natural wood grain, NOT pixel art, NOT chunky retro blocks. Landscape 3:2 rectangle, straight parallel edges, modest rounded outer corners, centered and filling 94% of image, transparent outside. Frame roughly 8% of width, corners restrained with tiny brass joints. Large completely blank ivory paper center, uniform and quiet enough for dark Korean text placed by game engine. Orthographic straight-on, no perspective, no lettering, no symbols, no characters, no UI mockup, no surrounding scene, no drop shadow outside sprite. Palette matches traditional dojo art with dark reddish beams and amber lantern light. Nine-slice friendly: straight uninterrupted horizontal and vertical frame segments, detail only at corners, no center decoration.

### StrikeTile

Use case: stylized-concept. Asset type: Unity rhythm game note texture and keycap sprite. Generate a single wide low rectangular ivory-and-gold wooden striking tile, straight-on orthographic top view, no perspective. Traditional Korean dojo style: warm ivory lacquer center, slim aged brass edge, reddish walnut wooden bevel. Smooth hand-painted game art, subtle grain, soft polished highlights, absolutely not pixel art. Tile centered, aspect ratio approximately 4:1, fills width with minimal margins. The center is blank; no letters, characters, symbols, or divisions. It must read instantly at 80 pixels wide. Symmetric simple silhouette with slightly rounded corners. No other objects, no UI screenshot, no background scene, no external shadow. Transparent outside the tile. All four rhythm lanes will use this exact same ivory/gold color, so no rainbow or multiple variants.

### WoodPanel

Use case: stylized-concept. Asset type: production Unity nine-sliced game UI wooden panel sprite. One rectangular dark walnut panel with a narrow aged golden brass inner rim and reddish wooden outer frame. Front facing orthographic, absolutely no perspective. Korean martial arts dojo game, smooth painterly detailed wood grain, not pixel art, not chunky retro shapes. Entire large center is very dark brown lacquer wood with subtle grain, blank and quiet to support cream text drawn by the game. Rectangle 3:2, almost fills image. Four modest brass corner joints. Borders clean and straight, thin about 5 percent of size, all ornament confined to corners, center flat. No writing, no symbols, no characters, no UI mockup or scene. Transparent exterior, no cast shadow or glow outside. Material colors match reddish dojo beams and amber lantern light. Intended to reuse for combo display, buttons and a tall gameplay frame; nine-slice friendly borders.



### GameplayScroll

Generated with the built-in image_gen tool and saved as `Assets/Game/Art/DojoGameplay/GameplayScroll.png`. `GameplayScroll.asset` uses the full image. The central gameplay frame displays this sprite without slicing; the transparent world camera renders the notes above it.

Final prompt:

Use case: stylized-concept. Asset type: Unity rhythm game playfield background sprite. Generate a single tall vertical unrolled traditional Korean hanji paper scroll viewed straight-on orthographically, portrait composition approximately 2:3. Warm muted ivory parchment, subtle handmade paper fibers and softly aged tan edges. Simple rolled paper at top and bottom with restrained dark reddish wooden end caps. Large completely blank quiet paper center occupying 85 percent of width and 85 percent of height, suitable behind falling rhythm notes. Smooth painterly 2D game art, soft subdued texture, no pixel art. Entire scroll fully visible, centered with minimal exterior margins. Genuine transparent background outside scroll. No text, calligraphy, symbols, characters, illustrations, UI, scene, perspective, cast shadow outside sprite, watermark. Nine-slice friendly straight vertical sides and uninterrupted blank center. Match a warm Korean martial arts dojo aesthetic.


Gameplay typography now uses Mona12TextKR from [Mona Font release 2026.10.09](https://github.com/MonadABXY/mona-font/releases/tag/2026.10.09), including lane keycaps and results. The font is bundled in `Assets/Game/Fonts/Mona12TextKR.ttf`; upstream license notices are preserved in `Assets/Game/Fonts/Mona-Licenses/`.
