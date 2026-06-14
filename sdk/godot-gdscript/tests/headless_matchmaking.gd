extends Node
## Headless matchmaking test: two clients queue for the same game mode and get matched
## into the same room. Prints "SMOKE: PASS" / exit 0 on success.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url: String = ""
var _key: String = ""
var _a: Node
var _b: Node
var _a_room := ""
var _b_room := ""
var _done := false


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])
	_key = str(cfg["key"])

	_a = ClientScript.new()
	_b = ClientScript.new()
	add_child(_a)
	add_child(_b)

	_a.connection_error.connect(func(r): _fail("A conn error: " + r))
	_b.connection_error.connect(func(r): _fail("B conn error: " + r))
	_a.error_received.connect(func(c, m): _fail("A error: " + c))
	_b.error_received.connect(func(c, m): _fail("B error: " + c))

	_a.match_found.connect(func(room_id, code, players): _on_match("A", room_id, players.size()))
	_b.match_found.connect(func(room_id, code, players): _on_match("B", room_id, players.size()))

	# A queues first; once queued, B queues for the same mode.
	_a.matchmaking_queued.connect(func(_m, _r, _s): _start_b())
	_a.connected.connect(func(): _a.join_matchmaking("duel"))
	_a.configure(_key)
	_a.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _start_b() -> void:
	if _b.is_connected_to_server():
		return
	_b.connected.connect(func(): _b.join_matchmaking("duel"))
	_b.configure(_key)
	_b.connect_to_server(_url)


func _on_match(who: String, room_id: String, player_count: int) -> void:
	print("SMOKE: ", who, " matched into ", room_id, " players=", player_count)
	if who == "A":
		_a_room = room_id
	else:
		_b_room = room_id

	if _a_room != "" and _b_room != "":
		if _a_room == _b_room:
			print("SMOKE: PASS")
			_quit(0)
		else:
			_fail("players matched into different rooms")


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
