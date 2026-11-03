# Unified start-button typography

The shared default for all UI Text components is Dokdo-Regular.ttf with FontStyle.Bold. Title, Song Selection, Settings, Gameplay, Results, detailed review and Chart Editor use the same Dokdo lettering. UiFontSettings.Current keeps its existing resource interface and now resolves Dokdo; UiFontSettings.Style, Apply and ApplyTo provide the common style for scene builders and dynamic control factories, respecting the nearest font scope.

Title, Song Selection, Settings, Gameplay, Results and Chart Editor builders apply the common typography after composing their controls, including inactive popup and dropdown templates. UiElements and AudioTimelineUi apply it when creating runtime labels, buttons, fields and dropdowns; entrance recovery messages and lane key labels use the same style. Font size, color, positioning, control references and behavior are retained.

The final unification updates all saved scenes and SettingsPopup through Unity APIs without regenerating their layouts. Existing font fields, scopes, hidden controls and runtime defaults all reference Dokdo. Bootstrap has no text. Mona remains only as the existing fallback for glyphs absent from Dokdo and is never configured as Dokdo's own fallback through the UI default.

UiTypographyTests checks every saved UI scene, the common popup and dynamically created controls, including inactive text and input placeholders. It also checks scoped brush-font controls, common Korean/digit glyphs, embedded font data and the Mona fallback reference.
