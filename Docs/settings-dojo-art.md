# Settings dojo art

The settings popup and standalone Settings scene use the same red-brown timber, bronze hardware, amber light and detailed pixel shading as the title doorway and training dummies. TrainingInterior is reused as the standalone background. The title/settings modal keeps the existing scene visible behind its shade.

SettingsPanel.png supplies a walnut-and-bronze frame with a quiet pale parchment center. SettingsControl.png supplies the light maple face and timber rim used by tabs, Apply/Close, dropdowns, sliders and numeric fields. Both were generated with the built-in image_gen tool using EntranceFrame.png and TrainingDummy.png as material references; the panel also guided the control plate.

SettingsArtAssets preserves original PNG dimensions, point filtering, uncompressed alpha and stable sprite assets with 128-pixel source borders. PixelDojoFrameGraphic renders nine slices only when artwork is assigned, preserving legacy frame behavior elsewhere. Frame tint affects the center while bronze corners keep their original color. SettingsScreen retains red selected tabs and cream text, with natural wood for unselected tabs. Dropdown arrows use crisp stepped geometry and do not consume input.

Existing settings controls, dimensions, navigation and stored values are preserved. Diagnostic/status labels remain hidden. MainMenu settings return, SettingsFlowTests and Smoke verify interaction; no player build is needed for this style change.

## Generation prompts

Panel: Create one blank front-facing landscape settings panel matching the title's detailed pixel-art red walnut, bronze joinery and amber lighting. Use a rich beveled timber rim with compact bronze corner plates around a large quiet pale ivory/parchment center. Keep text and controls out of the image, ornament confined to the border, a symmetric layout suitable for nine-slice scaling, and an opaque full rectangular canvas. No scene, labels, logos or watermark.

Control: Create one blank front-facing 3:1 wood control plate matching the settings panel and the training dummy's golden signboard. Use a thin reddish walnut beveled rim with compact bronze corner pins and a broad quiet pale maple center with restrained horizontal grain. Use the same crisp detailed pixel shading and upper-left warm highlights. Keep the center free of hardware and writing so nine-slice scaling works for buttons, dropdowns and knobs. No text, arrows, icons, surrounding scene, exterior shadow or padding.
