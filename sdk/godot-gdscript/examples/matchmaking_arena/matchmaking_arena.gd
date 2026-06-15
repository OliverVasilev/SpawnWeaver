extends Node2D
## SpawnWeaver example — 1v1 Matchmaking Arena (Milestone 21.3).
##
## Demonstrates: automatic guest login, entering the matchmaking queue, being matched with
## another player into a generated room, exchanging movement events, and ending the match.
##
## Two players who both click "Find Match" are paired into the same room. Move your dot
## with the arrow keys; your position is relayed to your opponent as a `game.event`.
##
## How to run two players:
##   1. Start the backend and create a project (POST /api/projects) for a public key.
##   2. Run this scene in two instances (Debug > Run Multiple Instances > 2).
##   3. Both: paste the key, fix URL/port, Connect, then Find Match. You'll be paired.
##   4. Move with the arrow keys; End Match returns you to the queue screen.

const DEFAULT_URL := "wss://spawnweaver.dev/connect"
const GAME_MODE := "duel_1v1"
const SPEED := 240.0
const SEND_INTERVAL := 0.05            # 20 position updates/sec (under the rate limit)
const SMOOTHING := 14.0                # how fast the opponent dot catches up to its latest position
const SELF_COLOR := Color(0.20, 0.75, 1.0)
const OPP_COLOR := Color(1.0, 0.55, 0.25)
const ARENA := Rect2(40, 150, 720, 380)

var _url_edit: LineEdit
var _key_edit: LineEdit
var _name_edit: LineEdit
var _status: Label
var _find_btn: Button
var _end_btn: Button

var _in_match := false
var _self_pos := Vector2(400, 340)
var _opp_pos := Vector2.ZERO
var _opp_target := Vector2.ZERO
var _opp_id := ""
var _has_opponent := false
var _send_accum := 0.0


func _ready() -> void:
	_build_ui()
	_apply_saved_config()

	MultiplayerService.welcomed.connect(func(_id): _set_status("Connected. Click Find Match."))
	MultiplayerService.connection_error.connect(func(r): _set_status("Connection error: " + r))
	MultiplayerService.disconnected.connect(func(): _set_status("Disconnected."))
	MultiplayerService.matchmaking_queued.connect(func(_m, _r, _s): _set_status("Searching for an opponent…"))
	MultiplayerService.match_found.connect(_on_match_found)
	MultiplayerService.matchmaking_timeout.connect(func(_m, _r): _on_match_end("No opponent found — try again."))
	MultiplayerService.player_left.connect(_on_player_left)
	MultiplayerService.room_expired.connect(func(_id): _on_match_end("Opponent left — match over."))
	MultiplayerService.event_received.connect(_on_event_received)
	MultiplayerService.sdk_error.connect(func(e): _set_status("Error: " + str(e.get("message", e.get("code", "")))))


func _process(delta: float) -> void:
	if _in_match:
		var move := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
		if move != Vector2.ZERO:
			_self_pos += move * SPEED * delta
			_self_pos.x = clampf(_self_pos.x, ARENA.position.x, ARENA.end.x)
			_self_pos.y = clampf(_self_pos.y, ARENA.position.y, ARENA.end.y)
		_send_accum += delta
		if _send_accum >= SEND_INTERVAL and move != Vector2.ZERO:
			_send_accum = 0.0
			MultiplayerService.send_event("move", {"x": _self_pos.x, "y": _self_pos.y})
		if _has_opponent:
			# Smoothly follow the opponent's last reported position instead of snapping to it.
			_opp_pos = _opp_pos.lerp(_opp_target, 1.0 - exp(-delta * SMOOTHING))
	queue_redraw()


func _draw() -> void:
	# Arena.
	draw_rect(ARENA, Color(0.08, 0.10, 0.16), true)
	draw_rect(ARENA, Color(0.2, 0.5, 0.8), false, 2.0)
	if not _in_match:
		return
	draw_circle(_self_pos, 16.0, SELF_COLOR)
	if _has_opponent:
		draw_circle(_opp_pos, 16.0, OPP_COLOR)


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

	panel.add_child(_make_title("SpawnWeaver — 1v1 Matchmaking Arena"))

	var row := HBoxContainer.new()
	_url_edit = LineEdit.new()
	_url_edit.text = DEFAULT_URL
	_url_edit.custom_minimum_size = Vector2(240, 0)
	row.add_child(_url_edit)
	_key_edit = LineEdit.new()
	_key_edit.placeholder_text = "Project key (pk_...)"
	_key_edit.custom_minimum_size = Vector2(220, 0)
	row.add_child(_key_edit)
	panel.add_child(row)

	var row2 := HBoxContainer.new()
	_name_edit = LineEdit.new()
	_name_edit.placeholder_text = "Display name"
	_name_edit.text = "Player"
	row2.add_child(_name_edit)
	var connect_btn := Button.new()
	connect_btn.text = "Connect"
	connect_btn.pressed.connect(_on_connect_pressed)
	row2.add_child(connect_btn)
	_find_btn = Button.new()
	_find_btn.text = "Find Match"
	_find_btn.disabled = true
	_find_btn.pressed.connect(_on_find_pressed)
	row2.add_child(_find_btn)
	_end_btn = Button.new()
	_end_btn.text = "End Match"
	_end_btn.disabled = true
	_end_btn.pressed.connect(func(): _on_match_end("You ended the match."))
	row2.add_child(_end_btn)
	panel.add_child(row2)

	_status = _make_label("Enter your project key and connect.")
	panel.add_child(_status)


func _on_connect_pressed() -> void:
	MultiplayerService.configure(_key_edit.text.strip_edges())
	_set_status("Connecting…")
	MultiplayerService.connect_to_server(_url_edit.text.strip_edges())
	_find_btn.disabled = false


func _on_find_pressed() -> void:
	if not MultiplayerService.is_connected_to_server():
		_set_status("Connect first.")
		return
	var name := _name_edit.text.strip_edges()
	MultiplayerService.join_matchmaking(GAME_MODE, "", 2, name if name != "" else "Player")
	_find_btn.disabled = true


func _on_match_found(room_id: String, room_code: String, players: Array) -> void:
	_in_match = true
	_has_opponent = false
	_opp_id = ""
	_self_pos = Vector2(ARENA.position.x + 80, ARENA.get_center().y)
	for p in players:
		var pid := str(p.get("playerId", ""))
		if pid != MultiplayerService.player_id:
			_opp_id = pid
	_set_status("Match found! Room %s — use the arrow keys." % room_code)
	_find_btn.disabled = true
	_end_btn.disabled = false


func _on_event_received(event: String, data: Dictionary, from_player_id: String) -> void:
	if event == "move" and (from_player_id == _opp_id or _opp_id == ""):
		_opp_id = from_player_id
		_opp_target = Vector2(float(data.get("x", 0.0)), float(data.get("y", 0.0)))
		if not _has_opponent:
			_opp_pos = _opp_target      # first update: start here, don't slide in from the corner
		_has_opponent = true


func _on_player_left(_room_id: String, player_id: String) -> void:
	if player_id == _opp_id:
		_on_match_end("Opponent left — match over.")


func _on_match_end(reason: String) -> void:
	if _in_match and MultiplayerService.current_room_id != "":
		MultiplayerService.leave_room(MultiplayerService.current_room_id)
	_in_match = false
	_has_opponent = false
	_opp_id = ""
	_end_btn.disabled = true
	_find_btn.disabled = not MultiplayerService.is_connected_to_server()
	_set_status(reason + " Click Find Match to play again.")


func _set_status(text: String) -> void:
	_status.text = text


func _make_title(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 18)
	return label


func _make_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	return label
