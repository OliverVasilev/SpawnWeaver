extends Node2D
## SpawnWeaver example — Simple State Sync Demo (Milestone 21.4 / 23).
##
## Demonstrates: spawn & own an entity, patch its state, receive other players' entity
## changes, get a full snapshot on late join, delete an entity, and shared room state.
##
## Each player owns one entity (keyed by their player id) — a colored dot they move with the
## arrow keys. Positions are synced via entity state (not raw events), so a player who joins
## late immediately sees everyone via the snapshot. The host can bump a shared "round".
##
## How to run two players:
##   1. Start the backend and create a project (in the dashboard) for a public key.
##   2. Run this scene in two instances (Debug > Run Multiple Instances > 2).
##   3. Both: paste the key, fix URL/port, Connect. Window A: Create Room (host).
##      Window B: paste the code -> Join (it snapshots A's entity immediately).
##   4. Move with the arrow keys. Host can press "Next round". "Delete mine" removes your dot.

const DEFAULT_URL := "wss://spawnweaver.dev/connect"
const SPEED := 220.0
const SEND_INTERVAL := 0.06
const ARENA := Rect2(40, 170, 720, 360)
const MINE_COLOR := Color(0.20, 0.75, 1.0)
const OTHER_COLOR := Color(1.0, 0.55, 0.25)

var _url_edit: LineEdit
var _key_edit: LineEdit
var _code_edit: LineEdit
var _status: Label
var _round_label: Label

var _in_room := false
var _is_host := false
var _my_id := ""
var _my_pos := Vector2(400, 350)
var _entities: Dictionary = {}      # entity_id -> { pos: Vector2, owner: String }
var _round := 0
var _send_accum := 0.0


func _ready() -> void:
	_build_ui()
	_apply_saved_config()
	MultiplayerService.welcomed.connect(func(_id): _set_status("Connected. Create or join a room."))
	MultiplayerService.connection_error.connect(func(r): _set_status("Connection error: " + r))
	MultiplayerService.room_created.connect(_on_room_created)
	MultiplayerService.room_joined.connect(_on_room_joined)
	MultiplayerService.state_snapshot_received.connect(_on_snapshot)
	MultiplayerService.entity_state_changed.connect(_on_entity_changed)
	MultiplayerService.entity_state_deleted.connect(_on_entity_deleted)
	MultiplayerService.room_state_changed.connect(_on_room_state_changed)
	MultiplayerService.state_update_rejected.connect(func(e): _set_status("Rejected: " + str(e.get("message", e.get("code", "")))))


func _process(delta: float) -> void:
	if _in_room:
		var move := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
		if move != Vector2.ZERO:
			_my_pos += move * SPEED * delta
			_my_pos.x = clampf(_my_pos.x, ARENA.position.x, ARENA.end.x)
			_my_pos.y = clampf(_my_pos.y, ARENA.position.y, ARENA.end.y)
			_send_accum += delta
			if _send_accum >= SEND_INTERVAL:
				_send_accum = 0.0
				MultiplayerService.patch_entity_state(_my_id, {"x": _my_pos.x, "y": _my_pos.y})
	queue_redraw()


func _draw() -> void:
	draw_rect(ARENA, Color(0.08, 0.10, 0.16), true)
	draw_rect(ARENA, Color(0.2, 0.5, 0.8), false, 2.0)
	if not _in_room:
		return
	for id in _entities:
		var e: Dictionary = _entities[id]
		var color := MINE_COLOR if id == _my_id else OTHER_COLOR
		draw_circle(e["pos"], 15.0, color)


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
	var panel := VBoxContainer.new()
	panel.add_theme_constant_override("separation", 6)
	panel.position = Vector2(12, 8)
	add_child(panel)
	panel.add_child(_label("SpawnWeaver — Simple State Sync Demo", 18))

	var row := HBoxContainer.new()
	_url_edit = LineEdit.new(); _url_edit.text = DEFAULT_URL; _url_edit.custom_minimum_size = Vector2(240, 0)
	row.add_child(_url_edit)
	_key_edit = LineEdit.new(); _key_edit.placeholder_text = "Project key (pk_...)"; _key_edit.custom_minimum_size = Vector2(220, 0)
	row.add_child(_key_edit)
	var connect_btn := Button.new(); connect_btn.text = "Connect"; connect_btn.pressed.connect(_on_connect)
	row.add_child(connect_btn)
	panel.add_child(row)

	var row2 := HBoxContainer.new()
	var create_btn := Button.new(); create_btn.text = "Create Room"; create_btn.pressed.connect(func(): MultiplayerService.create_room("Player"))
	row2.add_child(create_btn)
	_code_edit = LineEdit.new(); _code_edit.placeholder_text = "Room code"; _code_edit.custom_minimum_size = Vector2(110, 0)
	row2.add_child(_code_edit)
	var join_btn := Button.new(); join_btn.text = "Join"; join_btn.pressed.connect(_on_join)
	row2.add_child(join_btn)
	var round_btn := Button.new(); round_btn.text = "Next round (host)"; round_btn.pressed.connect(_on_next_round)
	row2.add_child(round_btn)
	var del_btn := Button.new(); del_btn.text = "Delete mine"; del_btn.pressed.connect(_on_delete_mine)
	row2.add_child(del_btn)
	panel.add_child(row2)

	_round_label = _label("Round: —", 14)
	panel.add_child(_round_label)
	_status = _label("Enter your project key and connect.", 14)
	panel.add_child(_status)


func _on_connect() -> void:
	MultiplayerService.configure(_key_edit.text.strip_edges())
	_set_status("Connecting…")
	MultiplayerService.connect_to_server(_url_edit.text.strip_edges())


func _on_join() -> void:
	var code := _code_edit.text.strip_edges().to_upper()
	if code != "":
		MultiplayerService.join_room(code, "Player")


func _on_next_round() -> void:
	if _is_host:
		MultiplayerService.patch_room_state({"round": _round + 1})
	else:
		_set_status("Only the host can change room state.")


func _on_delete_mine() -> void:
	if _my_id != "":
		MultiplayerService.delete_entity_state(_my_id)


# --- Signal handlers ---

func _on_room_created(_room_id: String, room_code: String, _players: Array) -> void:
	_is_host = true
	_enter_room("Room %s created (you are host)." % room_code)


func _on_room_joined(_room_id: String, _room_code: String, player: Dictionary, _players: Array) -> void:
	if str(player.get("playerId", "")) == MultiplayerService.player_id:
		_enter_room("Joined room. Move with the arrow keys.")


func _enter_room(message: String) -> void:
	_in_room = true
	_my_id = MultiplayerService.player_id
	_my_pos = Vector2(ARENA.position.x + 60 + randi() % 200, ARENA.get_center().y)
	# Spawn (and own) my entity.
	MultiplayerService.set_entity_state(_my_id, {"x": _my_pos.x, "y": _my_pos.y})
	_set_status(message)


func _on_snapshot(snapshot: Dictionary) -> void:
	# Late join: populate everyone already present.
	for e in snapshot.get("entities", []):
		var id := str(e.get("entityId", ""))
		var s: Dictionary = e.get("state", {})
		_entities[id] = {"pos": Vector2(float(s.get("x", 0)), float(s.get("y", 0))), "owner": str(e.get("ownerId", ""))}
	var room_state: Dictionary = snapshot.get("roomState", {})
	if room_state.has("round"):
		_set_round(int(room_state["round"]))


func _on_entity_changed(entity_id: String, _patch: Dictionary, full: Dictionary) -> void:
	_entities[entity_id] = {
		"pos": Vector2(float(full.get("x", 0)), float(full.get("y", 0))),
		"owner": _entities.get(entity_id, {}).get("owner", entity_id)
	}


func _on_entity_deleted(entity_id: String) -> void:
	_entities.erase(entity_id)


func _on_room_state_changed(_patch: Dictionary, state: Dictionary) -> void:
	if state.has("round"):
		_set_round(int(state["round"]))


func _set_round(value: int) -> void:
	_round = value
	_round_label.text = "Round: %d" % value


func _set_status(text: String) -> void:
	_status.text = text


func _label(text: String, size: int) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	return l
