@tool
extends Control
## SpawnWeaver editor dock: paste your project's public key and test the connection. Nothing else —
## the server URL is preconfigured, so this is all you need to go online.

const CONFIG_PATH := "res://addons/multiplayer_service/spawnweaver.cfg"
const SERVER_URL := "wss://spawnweaver.dev/connect"
const STARTER_TEMPLATE := "res://addons/multiplayer_service/templates/starter_game.gd"

var _key_edit: LineEdit
var _result: Label

# Edit-time connection test state.
var _socket: WebSocketPeer
var _testing := false
var _test_frames := 0


func _enter_tree() -> void:
	_build_ui()
	_load_key()


func _exit_tree() -> void:
	_stop_test()


func _process(_delta: float) -> void:
	if _testing and _socket != null:
		_poll_test()


# --- UI ---

func _build_ui() -> void:
	name = "SpawnWeaver"
	var root := VBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_theme_constant_override("separation", 8)
	add_child(root)

	var title := Label.new(); title.text = "SpawnWeaver"
	title.add_theme_font_size_override("font_size", 15)
	root.add_child(title)

	var hint := Label.new()
	hint.text = "Paste your project's public key, Save, then Test the connection."
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	root.add_child(hint)

	_key_edit = LineEdit.new(); _key_edit.placeholder_text = "pk_…"
	root.add_child(_key_edit)

	var save := Button.new(); save.text = "Save"
	save.pressed.connect(_on_save)
	root.add_child(save)

	var test := Button.new(); test.text = "Test connection"
	test.pressed.connect(_on_test)
	root.add_child(test)

	var generate := Button.new(); generate.text = "Generate starter scene"
	generate.tooltip_text = "Creates a ready-to-run multiplayer scene under res://spawnweaver/"
	generate.pressed.connect(_on_generate)
	root.add_child(generate)

	_result = Label.new()
	_result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	root.add_child(_result)


# --- Config ---

func _valid_key(key: String) -> bool:
	return key.begins_with("pk_") and key.length() >= 8


func _on_save() -> void:
	var key := _key_edit.text.strip_edges()
	if not _valid_key(key):
		_set_result("Enter your public key (it starts with pk_). Keep the secret key (sk_…) out of your game.")
		return
	var cfg := ConfigFile.new()
	cfg.load(CONFIG_PATH)   # preserve any other values already saved
	cfg.set_value("project", "public_key", key)
	cfg.set_value("project", "server_url", SERVER_URL)
	var err := cfg.save(CONFIG_PATH)
	_set_result("Saved." if err == OK else "Could not save (error %d)." % err)


func _load_key() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(CONFIG_PATH) == OK:
		_key_edit.text = str(cfg.get_value("project", "public_key", ""))


# --- Connection test (edit-time) ---

func _on_test() -> void:
	var key := _key_edit.text.strip_edges()
	if not _valid_key(key):
		_set_result("Enter your public key (pk_…) first.")
		return
	_stop_test()
	_socket = WebSocketPeer.new()
	var url := SERVER_URL + "?projectKey=" + key.uri_encode() + "&sdkVersion=editor"
	var err := _socket.connect_to_url(url)
	if err != OK:
		_set_result("Could not start the connection (error %d)." % err)
		_socket = null
		return
	_testing = true
	_test_frames = 0
	_set_result("Connecting…")


func _poll_test() -> void:
	_socket.poll()
	_test_frames += 1
	match _socket.get_ready_state():
		WebSocketPeer.STATE_OPEN:
			while _socket.get_available_packet_count() > 0:
				var text := _socket.get_packet().get_string_from_utf8()
				var msg = JSON.parse_string(text)
				if typeof(msg) == TYPE_DICTIONARY and str(msg.get("type", "")) == "connection.welcome":
					_set_result("✓ Connected — your key works.")
					_stop_test()
					return
		WebSocketPeer.STATE_CLOSED:
			_set_result("✗ Connection rejected — double-check your public key.")
			_stop_test()
			return
	if _test_frames > 600:   # ~10 seconds at 60 fps
		_set_result("Timed out — check your internet connection.")
		_stop_test()


func _stop_test() -> void:
	_testing = false
	if _socket != null:
		_socket.close()
		_socket = null


# --- Starter scene generator ---

func _on_generate() -> void:
	var source := FileAccess.get_file_as_string(STARTER_TEMPLATE)
	if source == "":
		_set_result("Could not read the starter template.")
		return

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://spawnweaver/scripts"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://spawnweaver/scenes"))

	var script_path := "res://spawnweaver/scripts/StarterGame.gd"
	var scene_path := "res://spawnweaver/scenes/StarterGame.tscn"
	if not _write_file(script_path, source):
		_set_result("Could not write " + script_path)
		return
	var scene_text := "[gd_scene load_steps=2 format=3]\n\n" \
		+ "[ext_resource type=\"Script\" path=\"%s\" id=\"1\"]\n\n" % script_path \
		+ "[node name=\"StarterGame\" type=\"Node2D\"]\nscript = ExtResource(\"1\")\n"
	if not _write_file(scene_path, scene_text):
		_set_result("Could not write " + scene_path)
		return

	if Engine.is_editor_hint():
		EditorInterface.get_resource_filesystem().scan()
	_set_result("Created %s — open it from the FileSystem dock and press Play." % scene_path)


func _write_file(path: String, contents: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(contents)
	file.close()
	return true


func _set_result(text: String) -> void:
	if _result != null:
		_result.text = text
