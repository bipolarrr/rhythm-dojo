# Dojo results screen

ResultsLayoutBuilder composes the results and detailed review with the same pixel-art timber, bronze fittings and paper textures used by Title and Song Selection. The backdrop reuses the enhanced open-pavilion SelectionBackground; the heading and buttons use SettingsControl; record cards use SettingsPanel; the result seal reuses the title's RoundPlaque. No new image generation or replacement of existing art is required.

The main view places a large song/album/judgment card on the left and the circular CLEAR/FAILED plaque on the right, with a wide reserved feedback card below and navigation along the bottom. The feedback region remains empty for future content. The review uses paper cards, wood controls and a dark warm chart surface that preserves judgment color contrast. All typography uses the shared bold Mona font.

Existing ResultsView and ResultsReviewView fields, result values, chart hit testing, paging, zoom, navigation and event subscriptions remain intact. Main panels use proportional anchors and the existing 1920 × 1080 CanvasScaler.
