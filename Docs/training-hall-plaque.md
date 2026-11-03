# Training hall title update

Historical design: superseded by the wooden-plaque menu in pixel-dojo-title.md. The title no longer generates training dummies or the 수련장 plaque; their source art remains available.

The open doorway displays Play / Settings / Editor / Quit from left to right. All four use the existing TrainingDummy art at 176 × 336 (80% of the previous 220 × 420), spaced 190 units apart. The existing editor reference and action remain intact. Left/right navigation follows the row.

The menu's open position is (0, -95), placing all four dummies 50 reference UI units lower than the initial four-dummy layout, closer to the foreground floor.

TrainingHallPlaque.png is a decorative, non-raycasting Image attached to Training Dojo Interior at normalized position (0.5, 0.79), centered on the upstairs railing. It sits behind the sliding doors and entrance frame and follows the interior's approach animation. It has no Button or other action.

Generated with the built-in image_gen tool using the imagegen skill. Saved project asset: Assets/Game/Art/DojoEntrance/TrainingHallPlaque.png. Style references: TrainingInterior.png and PlayBoard.png. Existing artwork is unchanged.

Final prompt:

> Use case: stylized-concept. Asset type: isolated transparent game UI wooden hall sign sprite. Reference images: TrainingInterior.png is the style reference for detailed pixel-art reddish timber, deep brown shadows, antique bronze fittings and warm amber lighting. PlayBoard.png is a supporting style reference for carved Korean lettering and grain. Create a new horizontal rectangular wooden name plaque for the upstairs central railing of that dojo. Front orthographic view, centered, entire plaque visible, approximately 3.5:1 width:height. Rich dark reddish brown wood, finely layered pixel-art grain, beveled carved border, subtle warm golden edge highlights, small restrained antique bronze corner fittings. Exact centered Korean text: "수련장" (수 련 장), three large readable warm ivory-gold brush-carved characters, no other text. Premium 2D pixel game artwork matching the references, crisp pixel edges with detailed shading. No room, no wall, no hanging ropes, no stand, no buttons, no effects outside the plaque. Genuinely transparent background; tightly composed with a small transparent margin. Output just the plaque asset.
