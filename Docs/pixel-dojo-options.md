# Pixel dojo options

SettingsLayoutBuilder applies PixelOptionsStyle to the shared Settings scene. Title options and selection settings use the same wooden frame, paper panel, dark ink, warm sliders and dropdown templates. The selected Sound / Graphics / Gameplay tab uses a red frame with cream lettering. Keep existing option values and persistence controllers unchanged.

PixelOptionsStyle also styles the Title quit confirmation and selection mode dialog, including the speed and timing sliders. Buttons retain their Image component for hit testing while targetGraphic points at the decorative frame. Runtime tab/mode refresh therefore reads the explicit Image component and updates the frame separately.

Generated layouts remain authoritative in the builders. Full foundation verification: 168 EditMode and 82 PlayMode tests passed; Windows player build succeeded.
