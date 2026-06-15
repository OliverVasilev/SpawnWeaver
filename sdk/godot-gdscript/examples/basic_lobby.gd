extends Control
## Minimal example: connect, create/join a room, and exchange a test event.
##
## 1. Run the SpawnWeaver backend and create a project (POST /api/projects).
## 2. Paste the project's PUBLIC key (pk_...) into the "Project key" field.
## 3. Set the URL to match your backend, click Connect, then Create or Join.
## Run two copies of this scene (Debug > Run Multiple Instances) to see two players.

const DEFAULT_URL := "wss://spawnweaver.dev/connect"

var _url_edit: LineEdit
var _key_edit: LineEdit
var _code_edit: LineEdit
var _log: RichTextLabel
var _players_label: Label

var _current_room_id: String = ""


func _ready() -> void:
	_build_ui()

	MultiplayerService.connected.connect(_on_connected)
	MultiplayerService.disconnected.connect(_on_disconnected)
	MultiplayerService.connection_error.connect(_on_connection_error)
	MultiplayerService.welcomed.connect(_on_welcomed)
	MultiplayerService.room_created.connect(_on_room_created)
	MultiplayerService.room_joined.connect(_on_room_joined)
	MultiplayerService.player_joined.connect(_on_player_joined)
	MultiplayerService.player_left.connect(_on_player_left)
	MultiplayerService.room_players.connect(_on_room_players)
	MultiplayerService.event_received.connect(_on_event_received)
	MultiplayerService.error_received.connect(_on_error_received)


func _build_ui() -> void:
	var root := VBoxContainer.new()
	root.add_theme_constant_override("separation", 6)
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.offset_left = 12
	root.offset_top = 12
	root.offset_right = -12
	root.offset_bottom = -12
	add_child(root)

	root.add_child(_make_label("SpawnWeaver — Basic Lobby Example"))

	_url_edit = LineEdit.new()
	_url_edit.text = DEFAULT_URL
	root.add_child(_url_edit)

	_key_edit = LineEdit.new()
	_key_edit.placeholder_text = "Project key (pk_...)"
	root.add_child(_key_edit)

	var connect_button := Button.new()
	connect_button.text = "Connect"
	connect_button.pressed.connect(_on_connect_pressed)
	root.add_child(connect_button)

	var create_button := Button.new()
	create_button.text = "Create Room"
	create_button.pressed.connect(func(): MultiplayerService.create_room("Player"))
	root.add_child(create_button)

	var join_row := HBoxContainer.new()
	_code_edit = LineEdit.new()
	_code_edit.placeholder_text = "Room code"
	_code_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	join_row.add_child(_code_edit)
	var join_button := Button.new()
	join_button.text = "Join Room"
	join_button.pressed.connect(func(): MultiplayerService.join_room(_code_edit.text.strip_edges(), "Player"))
	join_row.add_child(join_button)
	root.add_child(join_row)

	var event_button := Button.new()
	event_button.text = "Send Test Event"
	event_button.pressed.connect(_on_send_event_pressed)
	root.add_child(event_button)

	_players_label = _make_label("Players: -")
	root.add_child(_players_label)

	_log = RichTextLabel.new()
	_log.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_log.scroll_following = true
	root.add_child(_log)


func _make_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label


func _on_connect_pressed() -> void:
	MultiplayerService.configure(_key_edit.text.strip_edges())
	MultiplayerService.connect_to_server(_url_edit.text.strip_edges())
	_append("Connecting to %s ..." % _url_edit.text)


func _on_send_event_pressed() -> void:
	if _current_room_id == "":
		_append("Join or create a room first.")
		return
	MultiplayerService.send_event("player_moved", {"x": randi() % 100, "y": randi() % 100}, _current_room_id)
	_append("Sent player_moved event.")


func _on_connected() -> void:
	_append("[color=green]Connected.[/color]")


func _on_disconnected() -> void:
	_append("[color=orange]Disconnected.[/color]")


func _on_connection_error(reason: String) -> void:
	_append("[color=red]Connection error: %s[/color]" % reason)


func _on_welcomed(connection_id: String) -> void:
	_append("Welcome. Connection id: %s" % connection_id)


func _on_room_created(room_id: String, room_code: String, players: Array) -> void:
	_current_room_id = room_id
	_append("[color=green]Room created. Code: %s[/color]" % room_code)
	_update_players(players)


func _on_room_joined(room_id: String, room_code: String, _player: Dictionary, players: Array) -> void:
	_current_room_id = room_id
	_append("Joined room %s." % room_code)
	_update_players(players)


func _on_player_joined(_room_id: String, player: Dictionary) -> void:
	_append("Player joined: %s" % str(player.get("playerId", "?")))


func _on_player_left(_room_id: String, player_id: String) -> void:
	_append("Player left: %s" % player_id)


func _on_room_players(_room_id: String, players: Array) -> void:
	_update_players(players)


func _on_event_received(event: String, data: Dictionary, from_player_id: String) -> void:
	_append("Event '%s' from %s: %s" % [event, from_player_id, str(data)])


func _on_error_received(code: String, message: String) -> void:
	_append("[color=red]Error (%s): %s[/color]" % [code, message])


func _update_players(players: Array) -> void:
	var names: Array[String] = []
	for player in players:
		names.append(str(player.get("playerName", player.get("playerId", "?"))))
	_players_label.text = "Players: %s" % ", ".join(names)


func _append(line: String) -> void:
	_log.append_text(line + "\n")
