extends Control
## SpawnWeaver example — Lobby + Ready Check (Milestone 21.2).
##
## Demonstrates: create a lobby, join a lobby, list players, toggle ready status,
## lobby ready-state updates (relayed as room events), and the host starting the game
## (which transitions everyone out of the lobby).
##
## Ready state and "start" are sent as room events (`game.event`) between lobby members,
## so this works on top of the existing lobby + event-relay backend.
##
## How to run two players:
##   1. Start the backend and create a project (POST /api/projects) for a public key.
##   2. Run this scene in two instances (Debug > Run Multiple Instances > 2).
##   3. Both: paste the key, fix URL/port, Connect. Window A: Create Lobby (becomes host).
##      Window B: List Lobbies -> Join, or paste the code -> Join.
##   4. Each player clicks Ready. When everyone is ready, the host's Start Game enables.

const DEFAULT_URL := "wss://spawnweaver.dev/connect"

var _url_edit: LineEdit
var _key_edit: LineEdit
var _name_edit: LineEdit
var _code_edit: LineEdit
var _status: Label
var _code_label: Label
var _roster: RichTextLabel
var _lobby_list: ItemList
var _ready_btn: Button
var _start_btn: Button

var _display_name := "Player"
var _players: Array = []
var _ready_by_id: Dictionary = {}     # player_id -> bool
var _host_id := ""
var _is_ready := false
var _lobbies: Array = []


func _ready() -> void:
	_build_ui()
	_apply_saved_config()

	MultiplayerService.welcomed.connect(func(_id): _set_status("Connected. Create or join a lobby."))
	MultiplayerService.connection_error.connect(func(r): _set_status("Connection error: " + r))
	MultiplayerService.disconnected.connect(func(): _set_status("Disconnected."))
	MultiplayerService.lobby_created.connect(_on_lobby_created)
	MultiplayerService.lobby_list.connect(_on_lobby_list)
	MultiplayerService.lobby_joined.connect(_on_lobby_joined)
	MultiplayerService.lobby_closed.connect(func(_id): _system("Lobby closed."))
	MultiplayerService.player_joined.connect(_on_player_joined)
	MultiplayerService.player_left.connect(_on_player_left)
	MultiplayerService.room_players.connect(func(_r, players): _set_players(players))
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

	root.add_child(_make_title("SpawnWeaver — Lobby + Ready Check"))

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

	var row := HBoxContainer.new()
	var create_btn := Button.new()
	create_btn.text = "Create Lobby"
	create_btn.pressed.connect(func(): MultiplayerService.create_lobby("My Lobby", "public", 8, {}, _display_name))
	row.add_child(create_btn)
	var list_btn := Button.new()
	list_btn.text = "List Lobbies"
	list_btn.pressed.connect(func(): MultiplayerService.list_lobbies())
	row.add_child(list_btn)
	_code_edit = LineEdit.new()
	_code_edit.placeholder_text = "Lobby code"
	_code_edit.custom_minimum_size = Vector2(110, 0)
	row.add_child(_code_edit)
	var join_btn := Button.new()
	join_btn.text = "Join"
	join_btn.pressed.connect(_on_join_pressed)
	row.add_child(join_btn)
	root.add_child(row)

	_lobby_list = ItemList.new()
	_lobby_list.custom_minimum_size = Vector2(0, 70)
	_lobby_list.item_activated.connect(_on_lobby_activated)
	root.add_child(_lobby_list)

	_code_label = _make_label("Not in a lobby.")
	root.add_child(_code_label)

	_roster = RichTextLabel.new()
	_roster.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_roster.bbcode_enabled = false
	root.add_child(_roster)

	var action_row := HBoxContainer.new()
	_ready_btn = Button.new()
	_ready_btn.text = "Ready"
	_ready_btn.disabled = true
	_ready_btn.pressed.connect(_on_ready_pressed)
	action_row.add_child(_ready_btn)
	_start_btn = Button.new()
	_start_btn.text = "Start Game"
	_start_btn.disabled = true
	_start_btn.visible = false
	_start_btn.pressed.connect(_on_start_pressed)
	action_row.add_child(_start_btn)
	root.add_child(action_row)

	_status = _make_label("Enter your project key and connect.")
	root.add_child(_status)


# --- Handlers ---

func _on_connect_pressed() -> void:
	_display_name = _name_edit.text.strip_edges()
	if _display_name == "":
		_display_name = "Player"
	MultiplayerService.configure(_key_edit.text.strip_edges())
	_set_status("Connecting...")
	MultiplayerService.connect_to_server(_url_edit.text.strip_edges())


func _on_join_pressed() -> void:
	var code := _code_edit.text.strip_edges().to_upper()
	if code == "":
		_set_status("Enter a lobby code.")
		return
	MultiplayerService.join_lobby(code, _display_name)


func _on_lobby_activated(index: int) -> void:
	if index >= 0 and index < _lobbies.size():
		MultiplayerService.join_lobby_by_id(str(_lobbies[index].get("lobbyId", "")), _display_name)


func _on_lobby_created(lobby: Dictionary) -> void:
	_host_id = MultiplayerService.player_id      # the creator is the host
	_enter_lobby(str(lobby.get("code", lobby.get("roomCode", ""))))
	_start_btn.visible = true
	_system("You created the lobby (you are host).")


func _on_lobby_list(lobbies: Array) -> void:
	_lobbies = lobbies
	_lobby_list.clear()
	for lobby in lobbies:
		_lobby_list.add_item("%s — %d player(s)" % [
			str(lobby.get("name", "Lobby")), int(lobby.get("playerCount", 0))])
	if lobbies.is_empty():
		_system("No public lobbies yet.")


func _on_lobby_joined(lobby_id: String, player: Dictionary, players: Array) -> void:
	if str(player.get("playerId", "")) == MultiplayerService.player_id:
		_enter_lobby(_code_edit.text.strip_edges().to_upper())
		_system("You joined the lobby.")
	_set_players(players)
	# The host is the first member (the creator). Refresh start-button visibility.
	if not players.is_empty():
		_host_id = str(players[0].get("playerId", _host_id))
	_start_btn.visible = (_host_id == MultiplayerService.player_id)


func _on_player_joined(_room_id: String, player: Dictionary) -> void:
	var pid := str(player.get("playerId", ""))
	if pid != MultiplayerService.player_id and not _has_player(pid):
		_players.append(player)
	_system("%s joined." % _short(pid))
	# Re-announce our ready state so late joiners see it.
	if _is_ready:
		MultiplayerService.send_event("ready", {"name": _display_name, "ready": true})
	_render_roster()


func _on_player_left(_room_id: String, player_id: String) -> void:
	_players = _players.filter(func(p): return str(p.get("playerId", "")) != player_id)
	_ready_by_id.erase(player_id)
	_system("%s left." % _short(player_id))
	_render_roster()


func _on_event_received(event: String, data: Dictionary, from_player_id: String) -> void:
	match event:
		"ready":
			_ready_by_id[from_player_id] = bool(data.get("ready", false))
			_render_roster()
		"start":
			_system("Host started the game! Transitioning to room %s…" % MultiplayerService.current_room_id)
			_begin_game()


func _on_ready_pressed() -> void:
	_is_ready = not _is_ready
	_ready_by_id[MultiplayerService.player_id] = _is_ready
	_ready_btn.text = "Unready" if _is_ready else "Ready"
	MultiplayerService.send_event("ready", {"name": _display_name, "ready": _is_ready})
	_render_roster()


func _on_start_pressed() -> void:
	MultiplayerService.send_event("start", {})
	_system("Starting game…")
	_begin_game()


# --- Lobby helpers ---

func _enter_lobby(code: String) -> void:
	_code_label.text = "Lobby code: %s  (share this)" % code
	_ready_btn.disabled = false
	_is_ready = false
	_ready_btn.text = "Ready"


func _begin_game() -> void:
	# In a real game you'd change_scene to your gameplay scene here. The lobby IS the room,
	# so you keep the same connection and `current_room_id`.
	_ready_btn.disabled = true
	_start_btn.disabled = true
	_code_label.text = "Game started in room %s." % MultiplayerService.current_room_id


func _set_players(players: Array) -> void:
	_players = players.duplicate()
	_render_roster()


func _render_roster() -> void:
	_update_start_button()
	_roster.clear()
	if _players.is_empty():
		_roster.add_text("Waiting for players…\n")
		return
	for p in _players:
		var pid := str(p.get("playerId", ""))
		var mark := "✓" if _ready_by_id.get(pid, false) else "○"
		var who := _short(pid)
		if pid == _host_id:
			who += " (host)"
		if pid == MultiplayerService.player_id:
			who += " (you)"
		_roster.add_text("%s  %s\n" % [mark, who])


func _update_start_button() -> void:
	var everyone_ready := _players.size() >= 2
	for p in _players:
		if not _ready_by_id.get(str(p.get("playerId", "")), false):
			everyone_ready = false
			break
	_start_btn.disabled = not (everyone_ready and _host_id == MultiplayerService.player_id)


func _has_player(player_id: String) -> bool:
	for p in _players:
		if str(p.get("playerId", "")) == player_id:
			return true
	return false


func _set_status(text: String) -> void:
	_status.text = text


func _system(text: String) -> void:
	_roster.add_text("— %s\n" % text)


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
