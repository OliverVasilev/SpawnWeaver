extends Control
## SpawnWeaver example — Realtime Chat Room (Milestone 21.1).
##
## Demonstrates: connect, automatic guest auth, create/join a room by code, send and
## receive room events (chat messages), and a live list of connected players.
##
## How to run two players:
##   1. Start the backend and create a project (POST /api/projects) to get a public key.
##   2. Run this scene in two instances (Debug > Run Multiple Instances > 2).
##   3. In both: paste the project key, fix the URL/port, click Connect.
##   4. Window A: Create Room -> share the code. Window B: type the code -> Join.
##   5. Type messages and press Send (or Enter); both windows see them.

const DEFAULT_URL := "wss://spawnweaver.dev/connect"

var _url_edit: LineEdit
var _key_edit: LineEdit
var _name_edit: LineEdit
var _code_edit: LineEdit
var _msg_edit: LineEdit
var _code_label: Label
var _status: Label
var _players_label: Label
var _log: RichTextLabel

var _display_name := "Player"
var _players: Array = []


func _ready() -> void:
	_build_ui()
	_apply_saved_config()

	MultiplayerService.connected.connect(_on_connected)
	MultiplayerService.disconnected.connect(func(): _set_status("Disconnected."))
	MultiplayerService.connection_error.connect(func(r): _set_status("Connection error: " + r))
	MultiplayerService.welcomed.connect(func(_id): _set_status("Connected. Create or join a room."))
	MultiplayerService.room_created.connect(_on_room_ready)
	MultiplayerService.room_joined.connect(_on_room_joined)
	MultiplayerService.player_joined.connect(_on_player_joined)
	MultiplayerService.player_left.connect(_on_player_left)
	MultiplayerService.room_players.connect(func(_room, players): _set_players(players))
	MultiplayerService.event_received.connect(_on_event_received)
	MultiplayerService.sdk_error.connect(func(e): _system("Error: " + str(e.get("message", e.get("code", "")))))


# --- UI ---

## Pre-fill the URL + key from the credentials saved in the SpawnWeaver editor dock,
## so you don't retype them (or the port) in every example.
func _apply_saved_config() -> void:
	var cfg := MultiplayerService.load_config()
	var url := str(cfg.get("server_url", ""))
	var key := str(cfg.get("public_key", ""))
	if url != "":
		_url_edit.text = url
	if key != "":
		_key_edit.text = key
		_set_status("Loaded saved config from the SpawnWeaver dock — just click Connect.")


func _build_ui() -> void:
	var root := VBoxContainer.new()
	root.add_theme_constant_override("separation", 6)
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.offset_left = 12
	root.offset_top = 12
	root.offset_right = -12
	root.offset_bottom = -12
	add_child(root)

	root.add_child(_make_title("SpawnWeaver — Realtime Chat Room"))

	_url_edit = LineEdit.new()
	_url_edit.text = DEFAULT_URL
	root.add_child(_url_edit)

	_key_edit = LineEdit.new()
	_key_edit.placeholder_text = "Project key (pk_...)"
	root.add_child(_key_edit)

	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = "Display name"
	_name_edit.text = "Player"
	root.add_child(_name_edit)

	var connect_btn := Button.new()
	connect_btn.text = "Connect"
	connect_btn.pressed.connect(_on_connect_pressed)
	root.add_child(connect_btn)

	# Room row: create / join-by-code.
	var room_row := HBoxContainer.new()
	var create_btn := Button.new()
	create_btn.text = "Create Room"
	create_btn.pressed.connect(_on_create_pressed)
	room_row.add_child(create_btn)
	_code_edit = LineEdit.new()
	_code_edit.placeholder_text = "Room code"
	_code_edit.custom_minimum_size = Vector2(120, 0)
	room_row.add_child(_code_edit)
	var join_btn := Button.new()
	join_btn.text = "Join"
	join_btn.pressed.connect(_on_join_pressed)
	room_row.add_child(join_btn)
	root.add_child(room_row)

	_code_label = _make_label("Not in a room.")
	root.add_child(_code_label)

	_players_label = _make_label("Players: —")
	root.add_child(_players_label)

	_log = RichTextLabel.new()
	_log.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_log.scroll_following = true
	_log.bbcode_enabled = false
	root.add_child(_log)

	# Message row.
	var msg_row := HBoxContainer.new()
	_msg_edit = LineEdit.new()
	_msg_edit.placeholder_text = "Type a message and press Enter"
	_msg_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_msg_edit.text_submitted.connect(func(_t): _on_send_pressed())
	msg_row.add_child(_msg_edit)
	var send_btn := Button.new()
	send_btn.text = "Send"
	send_btn.pressed.connect(_on_send_pressed)
	msg_row.add_child(send_btn)
	root.add_child(msg_row)

	_status = _make_label("Enter your project key and connect.")
	root.add_child(_status)


# --- Button handlers ---

func _on_connect_pressed() -> void:
	_display_name = _name_edit.text.strip_edges()
	if _display_name == "":
		_display_name = "Player"
	MultiplayerService.configure(_key_edit.text.strip_edges())
	_set_status("Connecting...")
	MultiplayerService.connect_to_server(_url_edit.text.strip_edges())


func _on_create_pressed() -> void:
	if not MultiplayerService.is_connected_to_server():
		_set_status("Connect first.")
		return
	MultiplayerService.create_room(_display_name)


func _on_join_pressed() -> void:
	var code := _code_edit.text.strip_edges().to_upper()
	if code == "":
		_set_status("Enter a room code to join.")
		return
	MultiplayerService.join_room(code, _display_name)


func _on_send_pressed() -> void:
	var text := _msg_edit.text.strip_edges()
	if text == "" or MultiplayerService.current_room_id == "":
		return
	# Relay the chat line to the room; we echo our own line locally since the relay
	# excludes the sender.
	MultiplayerService.send_event("chat", {"name": _display_name, "text": text})
	_chat_line(_display_name, text)
	_msg_edit.clear()


# --- Signal handlers ---

func _on_connected() -> void:
	_set_status("Connected.")


func _on_room_ready(room_id: String, room_code: String, players: Array) -> void:
	_code_label.text = "Room code: %s  (share this)" % room_code
	_set_players(players)
	_system("You created room %s." % room_code)
	_msg_edit.grab_focus()


func _on_room_joined(room_id: String, room_code: String, player: Dictionary, players: Array) -> void:
	if str(player.get("playerId", "")) == MultiplayerService.player_id:
		_code_label.text = "Room code: %s" % room_code
		_system("You joined room %s." % room_code)
		_msg_edit.grab_focus()
	_set_players(players)


func _on_player_joined(_room_id: String, player: Dictionary) -> void:
	if str(player.get("playerId", "")) != MultiplayerService.player_id:
		_system("%s joined." % _short(player.get("playerId", "")))
	if not _players.has(player):
		_players.append(player)
		_render_players()


func _on_player_left(_room_id: String, player_id: String) -> void:
	_system("%s left." % _short(player_id))
	_players = _players.filter(func(p): return str(p.get("playerId", "")) != player_id)
	_render_players()


func _on_event_received(event: String, data: Dictionary, from_player_id: String) -> void:
	if event == "chat":
		var name := str(data.get("name", _short(from_player_id)))
		_chat_line(name, str(data.get("text", "")))


# --- Helpers ---

func _set_players(players: Array) -> void:
	_players = players.duplicate()
	_render_players()


func _render_players() -> void:
	_players_label.text = "Players (%d): %s" % [
		_players.size(),
		", ".join(_players.map(func(p): return _short(p.get("playerId", ""))))
	]


func _chat_line(name: String, text: String) -> void:
	_log.add_text("%s: %s\n" % [name, text])


func _system(text: String) -> void:
	_log.add_text("— %s\n" % text)


func _set_status(text: String) -> void:
	_status.text = text


func _short(player_id) -> String:
	var s := str(player_id)
	return s.substr(0, 12) if s.length() > 12 else s


func _make_title(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 18)
	return label


func _make_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label
