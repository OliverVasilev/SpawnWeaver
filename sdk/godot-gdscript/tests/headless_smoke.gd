extends Node
## Headless end-to-end smoke test: drives two SDK clients against a running backend.
## Reads tests/test_config.json ({ "url": "...", "key": "pk_..." }), then:
##   client A connects -> creates a room
##   client B connects -> joins by code
## Prints "SMOKE: PASS" and exits 0 on success, or "SMOKE: FAIL ..." and exits 1.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url: String = ""
var _key: String = ""
var _code: String = ""
var _room_id: String = ""
var _client_a: Node
var _client_b: Node
var _done := false


func _ready() -> void:
	var raw := FileAccess.get_file_as_string("res://tests/test_config.json")
	var cfg = JSON.parse_string(raw)
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])
	_key = str(cfg["key"])

	_client_a = ClientScript.new()
	_client_b = ClientScript.new()
	add_child(_client_a)
	add_child(_client_b)

	_client_a.connection_error.connect(func(r): _fail("A connection_error: " + r))
	_client_b.connection_error.connect(func(r): _fail("B connection_error: " + r))
	_client_a.error_received.connect(func(c, m): _fail("A error: " + c))
	_client_b.error_received.connect(func(c, m): _fail("B error: " + c))

	_client_a.room_created.connect(_on_a_room_created)
	_client_b.room_joined.connect(_on_b_room_joined)
	_client_b.event_received.connect(_on_b_event_received)
	_client_a.connected.connect(func(): _client_a.create_room("Alice"))

	_client_a.configure(_key)
	_client_a.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_a_room_created(room_id: String, room_code: String, players: Array) -> void:
	_code = room_code
	_room_id = room_id
	print("SMOKE: A created room code=", room_code, " players=", players.size())
	_client_b.connected.connect(func(): _client_b.join_room(_code, "Bob"))
	_client_b.configure(_key)
	_client_b.connect_to_server(_url)


func _on_b_room_joined(room_id: String, room_code: String, player: Dictionary, players: Array) -> void:
	print("SMOKE: B joined room code=", room_code, " players=", players.size())
	if players.size() != 2:
		_fail("expected 2 players, got %d" % players.size())
		return
	# Now exercise the game-event relay: A sends, B should receive it.
	_client_a.send_event("player_moved", {"x": 10, "y": 5}, _room_id)


func _on_b_event_received(event: String, data: Dictionary, from_player_id: String) -> void:
	print("SMOKE: B received event=", event, " data=", data, " from=", from_player_id)
	if event == "player_moved" and int(data.get("x", 0)) == 10:
		print("SMOKE: PASS")
		_quit(0)
	else:
		_fail("unexpected event payload")


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
