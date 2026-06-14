extends Node2D
## SpawnWeaver demo: a tiny shared-movement game.
##
## Each player is a colored dot. Use the arrow keys to move; your position is sent to the
## room as a "player_moved" game event, and other players' dots move in response.
##
## How to run:
##   1. Start the backend and create a project (POST /api/projects) to get a public key.
##   2. Paste the key + URL below (or via the on-screen fields), press Connect, then Join.
##   3. Run two instances (Debug > Run Multiple Instances) and join the same room code.

const SPEED := 220.0
const SELF_COLOR := Color(0.2, 0.7, 1.0)
const OTHER_COLOR := Color(1.0, 0.6, 0.2)

@export var server_url := "ws://127.0.0.1:5000/connect"
@export var project_key := ""

var _self_pos := Vector2(320, 240)
var _others: Dictionary = {}          # player_id -> Vector2
var _send_accumulator := 0.0

var _url_edit: LineEdit
var _key_edit: LineEdit
var _code_edit: LineEdit
var _status: Label


func _ready() -> void:
	_build_ui()

	MultiplayerService.connected.connect(func(): _set_status("Connected. Create or join a room."))
	MultiplayerService.disconnected.connect(func(): _set_status("Disconnected."))
	MultiplayerService.reconnecting.connect(func(attempt): _set_status("Reconnecting (attempt %d)..." % attempt))
	MultiplayerService.connection_error.connect(func(reason): _set_status("Error: " + reason))
	MultiplayerService.room_created.connect(_on_room_ready.bind("created"))
	MultiplayerService.room_joined.connect(_on_room_joined)
	MultiplayerService.player_left.connect(func(_room, pid): _others.erase(pid))
	MultiplayerService.event_received.connect(_on_event)
	MultiplayerService.error_received.connect(func(code, _msg): _set_status("Error: " + MultiplayerService.describe_error(code)))


func _process(delta: float) -> void:
	if not MultiplayerService.is_connected_to_server() or MultiplayerService.current_room_id == "":
		queue_redraw()
		return

	var move := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if move != Vector2.ZERO:
		_self_pos += move * SPEED * delta

	# Send our position ~15 times per second.
	_send_accumulator += delta
	if _send_accumulator >= 1.0 / 15.0:
		_send_accumulator = 0.0
		MultiplayerService.send_event("player_moved", {"x": _self_pos.x, "y": _self_pos.y},
			MultiplayerService.current_room_id)

	queue_redraw()


func _draw() -> void:
	draw_circle(_self_pos, 16, SELF_COLOR)
	for pos in _others.values():
		draw_circle(pos, 16, OTHER_COLOR)


func _on_room_ready(_a, _b, _c, _which: String) -> void:
	_set_status("In a room (%s). Move with the arrow keys." % _which)


func _on_room_joined(_room_id: String, room_code: String, _player: Dictionary, _players: Array) -> void:
	_set_status("Joined room %s. Move with the arrow keys." % room_code)


func _on_event(event: String, data: Dictionary, from_player_id: String) -> void:
	if event == "player_moved" and from_player_id != MultiplayerService.player_id:
		_others[from_player_id] = Vector2(float(data.get("x", 0)), float(data.get("y", 0)))


func _build_ui() -> void:
	var panel := VBoxContainer.new()
	panel.position = Vector2(8, 8)
	add_child(panel)

	_url_edit = LineEdit.new(); _url_edit.text = server_url; _url_edit.custom_minimum_size.x = 320
	_key_edit = LineEdit.new(); _key_edit.placeholder_text = "Project key (pk_...)"; _key_edit.text = project_key
	_code_edit = LineEdit.new(); _code_edit.placeholder_text = "Room code (to join)"
	panel.add_child(_url_edit)
	panel.add_child(_key_edit)

	var connect_btn := Button.new(); connect_btn.text = "Connect"
	connect_btn.pressed.connect(func():
		MultiplayerService.configure(_key_edit.text.strip_edges())
		MultiplayerService.connect_to_server(_url_edit.text.strip_edges()))
	panel.add_child(connect_btn)

	var create_btn := Button.new(); create_btn.text = "Create Room"
	create_btn.pressed.connect(func(): MultiplayerService.create_room("Player"))
	panel.add_child(create_btn)

	var join_row := HBoxContainer.new()
	join_row.add_child(_code_edit)
	var join_btn := Button.new(); join_btn.text = "Join"
	join_btn.pressed.connect(func(): MultiplayerService.join_room(_code_edit.text.strip_edges(), "Player"))
	join_row.add_child(join_btn)
	panel.add_child(join_row)

	_status = Label.new(); _status.text = "Enter a project key and Connect."
	panel.add_child(_status)


func _set_status(text: String) -> void:
	if _status != null:
		_status.text = text
