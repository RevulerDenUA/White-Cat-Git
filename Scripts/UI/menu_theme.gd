class_name WhiteCatTheme
extends RefCounted

const INK := Color("080e17")
const PANEL := Color("101923")
const WHITE := Color("eef0ec")
const MUTED := Color("a4b0b9")
const DIM := Color("738492")
const LINE := Color("2c3a47")
const ACCENT := Color("c6dae9")
static var _font: Font
static var _display: Font
static var FONT: Font:
	get:
		if _font == null:
			_font = load("res://ASS/UI/Fonts/Onest.ttf") as Font
		return _font
static var DISPLAY: Font:
	get:
		if _display == null:
			_display = load("res://ASS/UI/Fonts/BarlowCondensed-SemiBold.ttf") as Font
		return _display

static func style(fill: Color, border: Color = Color.TRANSPARENT, width: int = 0, padding: int = 16) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = fill
	box.border_color = border
	box.set_border_width_all(width)
	box.content_margin_left = padding
	box.content_margin_right = padding
	box.content_margin_top = 10
	box.content_margin_bottom = 10
	return box

static func create() -> Theme:
	var theme := Theme.new()
	theme.default_font = FONT
	theme.default_font_size = 22
	theme.set_color("font_color", "Label", WHITE)
	for type: String in ["Button", "OptionButton", "CheckButton"]:
		theme.set_color("font_color", type, WHITE)
		theme.set_color("font_hover_color", type, WHITE)
		theme.set_color("font_focus_color", type, WHITE)
		theme.set_color("font_pressed_color", type, ACCENT)
		theme.set_color("font_disabled_color", type, DIM)
		theme.set_stylebox("normal", type, style(Color("182330"), LINE, 1))
		theme.set_stylebox("hover", type, style(Color("243442"), Color("738492"), 1))
		theme.set_stylebox("pressed", type, style(Color("2b3540"), ACCENT, 1))
		theme.set_stylebox("disabled", type, style(Color("111922"), LINE, 1))
		theme.set_stylebox("focus", type, style(Color.TRANSPARENT, ACCENT, 2))
	theme.set_constant("h_separation", "OptionButton", 18)
	theme.set_stylebox("panel", "PopupMenu", style(PANEL, LINE, 1))
	theme.set_stylebox("hover", "PopupMenu", style(Color("33414b")))
	theme.set_color("font_color", "PopupMenu", WHITE)
	theme.set_color("font_hover_color", "PopupMenu", ACCENT)
	theme.set_constant("v_separation", "PopupMenu", 12)
	for part: String in ["slider", "grabber_area", "grabber_area_highlight"]:
		var track := style(LINE if part == "slider" else ACCENT, Color.TRANSPARENT, 0, 0)
		track.content_margin_top = 2
		track.content_margin_bottom = 2
		theme.set_stylebox(part, "HSlider", track)
	var grip := GradientTexture2D.new()
	var gradient := Gradient.new()
	gradient.colors = PackedColorArray([ACCENT, ACCENT])
	grip.gradient = gradient
	grip.width = 8
	grip.height = 22
	theme.set_icon("grabber", "HSlider", grip)
	theme.set_icon("grabber_highlight", "HSlider", grip)
	theme.set_stylebox("scroll", "VScrollBar", style(Color("18232e"), Color.TRANSPARENT, 0, 3))
	theme.set_stylebox("grabber", "VScrollBar", style(Color("596c7d"), Color.TRANSPARENT, 0, 3))
	theme.set_stylebox("grabber_highlight", "VScrollBar", style(ACCENT, Color.TRANSPARENT, 0, 3))
	theme.set_stylebox("panel", "TooltipPanel", style(PANEL, LINE, 1))
	theme.set_color("font_color", "TooltipLabel", WHITE)
	return theme

static func label(text: String, font_size: int = 22, color: Color = WHITE) -> Label:
	var item := Label.new()
	item.text = text
	item.add_theme_font_size_override("font_size", font_size)
	item.add_theme_color_override("font_color", color)
	item.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return item

static func spacer(height: float = 0.0, expand: bool = false) -> Control:
	var item := Control.new()
	item.custom_minimum_size.y = height
	item.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if expand:
		item.size_flags_vertical = Control.SIZE_EXPAND_FILL
		item.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return item

static func rule() -> HSeparator:
	var item := HSeparator.new()
	var line := style(LINE, Color.TRANSPARENT, 0, 0)
	line.content_margin_top = 0
	line.content_margin_bottom = 0
	item.add_theme_stylebox_override("separator", line)
	item.add_theme_constant_override("separation", 1)
	item.custom_minimum_size.y = 1
	return item

static func button(text: String, primary: bool = false) -> Button:
	var item := Button.new()
	item.text = text
	item.custom_minimum_size.y = 50
	item.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	if primary:
		item.add_theme_stylebox_override("normal", style(ACCENT))
		item.add_theme_stylebox_override("hover", style(Color("e1eaf0")))
		item.add_theme_stylebox_override("pressed", style(Color("a9bfd0")))
		item.add_theme_color_override("font_color", INK)
		item.add_theme_color_override("font_hover_color", INK)
		item.add_theme_color_override("font_pressed_color", INK)
		item.add_theme_color_override("font_focus_color", INK)
	return item
