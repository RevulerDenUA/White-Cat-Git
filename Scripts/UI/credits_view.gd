class_name CreditsView
extends VBoxContainer

signal license_requested(title: String, text: String)
signal document_requested(title: String, text: String)

const CATALOGUE := "res://ASS/UI/credits.json"
var _notice: Label

func _ready() -> void:
	add_theme_constant_override("separation", 0)
	var catalogue: Variant = JSON.parse_string(FileAccess.get_file_as_string(CATALOGUE))
	if not catalogue is Dictionary or not catalogue.get("sections") is Array:
		push_error("Invalid credits catalogue: %s" % CATALOGUE)
		add_child(WhiteCatTheme.label("Не вдалося прочитати перелік джерел.", 21, WhiteCatTheme.MUTED))
		return
	for section: Dictionary in catalogue["sections"]:
		add_child(WhiteCatTheme.label(section["title"], 25, WhiteCatTheme.ACCENT))
		add_child(WhiteCatTheme.spacer(20))
		for entry: Dictionary in section["entries"]:
			_entry(entry)
		add_child(WhiteCatTheme.rule())
		add_child(WhiteCatTheme.spacer(28))
	_notice = _paragraph(catalogue.get("note", ""), 19, WhiteCatTheme.MUTED)

func _paragraph(text: String, font_size: int = 21, color: Color = WhiteCatTheme.MUTED) -> Label:
	var label := WhiteCatTheme.label(text, font_size, color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(label)
	return label

func _entry(entry: Dictionary) -> void:
	_paragraph(entry["title"], 23, WhiteCatTheme.WHITE)
	add_child(WhiteCatTheme.spacer(6))
	if entry.has("usage"):
		_paragraph(entry["usage"], 18, WhiteCatTheme.ACCENT)
	_paragraph(entry["credit"])
	if entry.has("detail"):
		add_child(WhiteCatTheme.spacer(5))
		_paragraph(entry["detail"], 19)
	# Each link is its own line so long source names still fit narrow screens.
	for link: Dictionary in entry.get("links", []):
		var url: String = link["url"]
		var button := _link(link["label"])
		button.tooltip_text = url
		button.pressed.connect(_open_source.bind(url))
	if entry.has("document_file"):
		var button := _link(entry.get("document_label", "Повний перелік матеріалів"))
		button.pressed.connect(_request_document.bind(entry))
	if entry.has("license_file") or entry.has("runtime_license"):
		var button := _link("Текст ліцензії" if entry.get("runtime_license") != "godot_components" else "Автори та ліцензії")
		button.pressed.connect(_request_license.bind(entry))
	add_child(WhiteCatTheme.spacer(26))

func _link(title: String) -> LinkButton:
	var button := LinkButton.new()
	button.text = title
	button.custom_minimum_size.y = 44
	button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	button.focus_mode = Control.FOCUS_ALL
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.underline = LinkButton.UNDERLINE_MODE_ON_HOVER
	button.add_theme_font_size_override("font_size", 20)
	button.add_theme_color_override("font_color", WhiteCatTheme.ACCENT)
	button.add_theme_color_override("font_hover_color", WhiteCatTheme.WHITE)
	button.add_theme_color_override("font_focus_color", WhiteCatTheme.WHITE)
	button.add_theme_stylebox_override("focus", WhiteCatTheme.style(Color.TRANSPARENT, WhiteCatTheme.ACCENT, 2, 4))
	add_child(button)
	return button

func _open_source(url: String) -> void:
	# The catalogue only opens web sources; local files and executable URLs are not links.
	if not url.begins_with("https://"):
		return
	if OS.shell_open(url) != OK:
		_notice.text = "Не вдалося відкрити джерело в браузері. Посилання: %s" % url

func _request_license(entry: Dictionary) -> void:
	var text := ""
	match entry.get("runtime_license", ""):
		"godot":
			text = Engine.get_license_text()
		"godot_components":
			text = component_licenses()
		_:
			text = FileAccess.get_file_as_string(entry.get("license_file", ""))
	if text.is_empty():
		_notice.text = "Не вдалося прочитати текст ліцензії."
		return
	license_requested.emit(entry["title"], text)

func _request_document(entry: Dictionary) -> void:
	var path: String = entry.get("document_file", "")
	if not path.begins_with("res://ASS/UI/SourceRecords/"):
		return
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		_notice.text = "Не вдалося прочитати перелік джерел."
		return
	document_requested.emit(entry["title"], text)

static func component_licenses() -> String:
	var paragraphs := PackedStringArray()
	for component: Dictionary in Engine.get_copyright_info():
		paragraphs.append(component["name"])
		for part: Dictionary in component["parts"]:
			paragraphs.append("\n".join(part["copyright"]))
			paragraphs.append("Files: " + ", ".join(part["files"]))
			paragraphs.append("License: " + part["license"])
		paragraphs.append("")
	var licenses := Engine.get_license_info()
	for name: String in licenses:
		paragraphs.append(name + "\n\n" + licenses[name])
	return "\n\n".join(paragraphs)
