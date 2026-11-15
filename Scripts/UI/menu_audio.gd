extends Node

var _music: AudioStreamPlayer
var _hover: AudioStreamPlayer
var _confirm: AudioStreamPlayer
var _transition: AudioStreamPlayer

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_music = AudioStreamPlayer.new()
	_music.stream = load("res://ASS/UI/Audio/orbital_ambient.wav")
	if _music.stream is AudioStreamWAV:
		_music.stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
		_music.stream.loop_end = _music.stream.data.size() / 4
	_music.bus = "Music"
	_music.volume_db = -10.0
	add_child(_music)
	_hover = _player("res://ASS/UI/Audio/hover.wav")
	_confirm = _player("res://ASS/UI/Audio/confirm.wav")
	_transition = _player("res://ASS/UI/Audio/focus.wav")
	_transition.bus = "SFX"

func _player(path: String) -> AudioStreamPlayer:
	var player := AudioStreamPlayer.new()
	player.bus = "UI"
	player.stream = load(path)
	add_child(player)
	return player

func enter_menu() -> void:
	if not _music.playing:
		_music.volume_db = -10.0
		_music.play()

func leave_menu(seconds: float) -> void:
	var tween := create_tween()
	tween.tween_property(_music, "volume_db", -60.0, maxf(seconds, 0.05))
	tween.tween_callback(_music.stop)

func play_hover() -> void:
	if _hover != null and not _hover.playing:
		_hover.play()

func play_confirm() -> void:
	if _confirm != null:
		_confirm.play()

func play_transition() -> void:
	_transition.play()
