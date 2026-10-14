# Pixel dojo title assets

The title uses the supplied artwork with a full-screen dojo background, an upper-center logo, and three vertically stacked image buttons. The menu is visible immediately. The old circle graphic is absent from the generated Title scene. Start opens song selection, Options opens settings, and Quit opens the existing confirmation dialog.

## Asset mapping

- `Assets/Game/Art/PixelDojo/TitleBackground.png`: supplied background (image 5).
- `TitleLogo.png`: supplied logo (image 4).
- `StartButton.png`, `OptionsButton.png`, `QuitButton.png`: images 1–3.
- Matching `.asset` Sprite files reference the original PNG textures with a rectangle excluding transparent outer padding. The PNG pixels are unchanged.
- `Assets/Game/Resources/UI/PixelDojoCursor.png`: image 6, the normal pointer.
- `PixelDojoCursorPressed.png`: image 7, displayed only while the left mouse button is held.

`MainMenuLayoutBuilder` owns the layout. `MainMenuView.alwaysVisibleMenu` keeps this image menu open; the existing view fields remain compatible with navigation and title beat animation. Button text is baked into the supplied images, so it is not drawn again. Background aspect is preserved when filling the window. Point filtering retains pixel edges. Buttons support mouse interaction and explicit up/down keyboard navigation.

`DojoCursor` reads the original alpha bounds once and prepares two readable RGBA32 64×64 native cursor textures using nearest-neighbor sampling. It retains the existing click ripple and click sound. The cursor restores the normal image on release and the system pointer when the app loses focus.

Regenerate the Title scene with **Game Tools > Scenes > Build Title**.
