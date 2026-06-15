@tool
extends Control
## SpawnWeaver editor dock (Milestone 24): configure credentials, test the connection, generate
## starter scenes, and open the dashboard/docs — all inside the Godot editor.

const CONFIG_PATH := "res://addons/multiplayer_service/spawnweaver.cfg"
const TEMPLATE_DIR := "res://addons/multiplayer_service/templates"

# label -> { template, root, name }
const TEMPLATES := {
	"Starter Game (recommended)": {"file": "starter_game.gd", "root": "Node2D", "name": "StarterGame"},
	"Room Chat": {"file": "room_chat.gd", "root": "Control", "name": "RoomChat"},
	"Lobby": {"file": "lobby.gd", "root": "Control", "name": "Lobby"},
	"Matchmaking": {"file": "matchmaking.gd", "root": "Control", "name": "Matchmaking"},
	"State Sync Player": {"file": "state_sync_player.gd", "root": "Node2D", "name": "StateSyncPlayer"},
}

var _key_edit: LineEdit
var _url_edit: LineEdit
var _env_option: OptionButton
var _debug_check: CheckBox
var _scene_option: OptionButton
var _result: Label
var _log: TextEdit

# Edit-time connection test state.
var _socket: WebSocketPeer
var _testing := false
var _test_frames := 0


func _enter_tree() -> void:
	_build_ui()
	_load_into_ui()


func _exit_tree() -> void:
	_stop_test()


func _process(_delta: float) -> void:
	if _testing and _socket != null:
		_poll_test()


# --- UI ---

func _build_ui() -> void:
	name = "SpawnWeaver"
	var scroll := ScrollContainer.new()
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(scroll)

	var root := VBoxContainer.new()
	root.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root.add_theme_constant_override("separation", 6)
	scroll.add_child(root)

	_heading(root, "SpawnWeaver")

	# Configuration — the only thing you normally need is your project's public key.
	_heading(root, "Configuration")
	root.add_child(_labeled("Public key — paste it from your project page"))
	_key_edit = LineEdit.new(); _key_edit.placeholder_text = "pk_…"
	root.add_child(_key_edit)
	var save := Button.new(); save.text = "Save"
	save.pressed.connect(_on_save)
	root.add_child(save)

	# Advanced (optional) — hidden by default; the defaults already point at SpawnWeaver.
	var adv_toggle := CheckBox.new(); adv_toggle.text = "Advanced (optional)"
	root.add_child(adv_toggle)
	var adv := VBoxContainer.new(); adv.visible = false
	adv.add_theme_constant_override("separation", 6)
	adv_toggle.toggled.connect(func(on: bool): adv.visible = on)
	root.add_child(adv)
	adv.add_child(_labeled("Server URL"))
	_url_edit = LineEdit.new(); _url_edit.text = "wss://spawnweaver.dev/connect"
	adv.add_child(_url_edit)
	adv.add_child(_labeled("Environment"))
	_env_option = OptionButton.new()
	_env_option.add_item("Development")
	_env_option.add_item("Production")
	adv.add_child(_env_option)
	_debug_check = CheckBox.new(); _debug_check.text = "Enable SDK debug logging"
	adv.add_child(_debug_check)

	# Test.
	_heading(root, "Test")
	var test_row := HBoxContainer.new()
	var test_conn := Button.new(); test_conn.text = "Test connection"
	test_conn.pressed.connect(_on_test_connection)
	test_row.add_child(test_conn)
	var test_login := Button.new(); test_login.text = "Test guest login"
	test_login.pressed.connect(_on_test_connection)
	test_row.add_child(test_login)
	root.add_child(test_row)
	_result = _labeled("")
	root.add_child(_result)

	# Scene generator.
	_heading(root, "Generate a starter scene")
	_scene_option = OptionButton.new()
	for label in TEMPLATES:
		_scene_option.add_item(label)
	root.add_child(_scene_option)
	var generate := Button.new(); generate.text = "Generate scene"
	generate.pressed.connect(_on_generate)
	root.add_child(generate)

	# Multi-client.
	_heading(root, "Test with multiple players")
	root.add_child(_labeled(
		"Use Debug → Run Multiple Instances (set 2+) then run your scene to play as several\n"
		+ "guest players locally. Generated scenes auto-connect using the saved credentials."))

	# Links.
	_heading(root, "Open")
	var links := HBoxContainer.new()
	_link_button(links, "Dashboard", "/dashboard")
	_link_button(links, "Debugger", "/dashboard/debugger")
	_link_button(links, "Docs", "/dashboard/getting-started")
	root.add_child(links)
	var examples := Button.new(); examples.text = "Open examples folder"
	examples.pressed.connect(_on_open_examples)
	root.add_child(examples)

	# Log.
	_heading(root, "Log")
	_log = TextEdit.new()
	_log.editable = false
	_log.custom_minimum_size = Vector2(0, 120)
	_log.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root.add_child(_log)


func _heading(parent: Node, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 15)
	parent.add_child(label)


func _labeled(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _link_button(parent: Node, text: String, path: String) -> void:
	var button := Button.new(); button.text = text
	button.pressed.connect(func(): OS.shell_open(_http_base() + path))
	parent.add_child(button)


# --- Config ---

## A public key looks like "pk_…"; the secret key (sk_…) must never ship in a client.
func _valid_public_key(key: String) -> bool:
	return key.begins_with("pk_") and key.length() >= 8


func _on_save() -> void:
	var key := _key_edit.text.strip_edges()
	if key != "" and not _valid_public_key(key):
		_set_result("Won't save: a public key starts with 'pk_'. Don't paste the secret (sk_…) key here.")
		return
	var cfg := ConfigFile.new()
	cfg.set_value("project", "public_key", _key_edit.text.strip_edges())
	cfg.set_value("project", "server_url", _url_edit.text.strip_edges())
	cfg.set_value("project", "environment", _env_option.get_item_text(_env_option.selected))
	cfg.set_value("project", "debug_enabled", _debug_check.button_pressed)
	var err := cfg.save(CONFIG_PATH)
	_set_result("Saved." if err == OK else "Could not save (error %d)." % err)


func _load_into_ui() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(CONFIG_PATH) != OK:
		return
	_key_edit.text = str(cfg.get_value("project", "public_key", ""))
	_url_edit.text = str(cfg.get_value("project", "server_url", _url_edit.text))
	_debug_check.button_pressed = bool(cfg.get_value("project", "debug_enabled", false))
	var env := str(cfg.get_value("project", "environment", "Development"))
	_env_option.selected = 1 if env == "Production" else 0


# --- Connection test (edit-time) ---

func _on_test_connection() -> void:
	var key := _key_edit.text.strip_edges()
	var url := _url_edit.text.strip_edges()
	if key == "" or url == "":
		_set_result("Enter a public key and server URL first.")
		return
	if not _valid_public_key(key):
		_set_result("That doesn't look like a public key — it should start with 'pk_'. The "
			+ "secret key (sk_…) must stay server-side, never in the game client.")
		return

	_stop_test()
	_log_line("Connecting to " + url + " …")
	_socket = WebSocketPeer.new()
	var full := url + ("&" if url.contains("?") else "?") + "projectKey=" + key.uri_encode() \
		+ "&sdkVersion=editor&engine=" + ("Godot " + str(Engine.get_version_info().get("string", ""))).uri_encode()
	var err := _socket.connect_to_url(full)
	if err != OK:
		_set_result("Could not start connection (error %d)." % err)
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
					var payload: Dictionary = msg.get("payload", {})
					_log_line("✓ Connected as " + str(payload.get("playerId", "")))
					_set_result("Success — guest login works.")
					_stop_test()
					return
		WebSocketPeer.STATE_CLOSED:
			_log_line("✗ Connection rejected (check the key and URL).")
			_set_result("Connection failed.")
			_stop_test()
			return
	if _test_frames > 600:   # ~10 seconds at 60 fps
		_log_line("✗ Timed out.")
		_set_result("Timed out — is the backend running?")
		_stop_test()


func _stop_test() -> void:
	_testing = false
	if _socket != null:
		_socket.close()
		_socket = null


# --- Scene generator ---

func _on_generate() -> void:
	var label := _scene_option.get_item_text(_scene_option.selected)
	var template: Dictionary = TEMPLATES[label]
	var template_path := TEMPLATE_DIR + "/" + str(template["file"])
	var source := FileAccess.get_file_as_string(template_path)
	if source == "":
		_set_result("Could not read template: " + template_path)
		return

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://spawnweaver/scripts"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://spawnweaver/scenes"))

	var script_path := "res://spawnweaver/scripts/%s.gd" % str(template["name"])
	var scene_path := "res://spawnweaver/scenes/%s.tscn" % str(template["name"])

	if not _write_file(script_path, source):
		_set_result("Could not write " + script_path)
		return
	if not _write_file(scene_path, _build_scene(script_path, str(template["root"]), str(template["name"]))):
		_set_result("Could not write " + scene_path)
		return

	var fs := EditorInterface.get_resource_filesystem() if Engine.is_editor_hint() else null
	if fs != null:
		fs.scan()
	_log_line("Generated " + scene_path)
	_set_result("Generated %s — open it from the FileSystem dock." % scene_path)


func _build_scene(script_path: String, root_type: String, node_name: String) -> String:
	var anchors := ""
	if root_type == "Control":
		anchors = "anchors_preset = 15\nanchor_right = 1.0\nanchor_bottom = 1.0\n"
	return "[gd_scene load_steps=2 format=3]\n\n" \
		+ "[ext_resource type=\"Script\" path=\"%s\" id=\"1\"]\n\n" % script_path \
		+ "[node name=\"%s\" type=\"%s\"]\n%s" % [node_name, root_type, anchors] \
		+ "script = ExtResource(\"1\")\n"


func _write_file(path: String, contents: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(contents)
	file.close()
	return true


# --- Links ---

func _on_open_examples() -> void:
	var examples := ProjectSettings.globalize_path("res://addons/multiplayer_service/examples")
	if DirAccess.dir_exists_absolute(examples):
		OS.shell_open(examples)
	else:
		OS.shell_open(_http_base() + "/dashboard/getting-started")


## Derives the dashboard base URL (http) from the configured WebSocket server URL.
func _http_base() -> String:
	var url := _url_edit.text.strip_edges()
	url = url.replace("wss://", "https://").replace("ws://", "http://")
	var connect_index := url.find("/connect")
	if connect_index != -1:
		url = url.substr(0, connect_index)
	if url == "":
		url = "https://spawnweaver.dev"
	return url


func _set_result(text: String) -> void:
	if _result != null:
		_result.text = text


func _log_line(text: String) -> void:
	if _log != null:
		_log.text += text + "\n"
