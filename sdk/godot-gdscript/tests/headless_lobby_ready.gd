extends Node
## Headless test for the Lobby + Ready Check example's core assumption: room events
## (`game.event`) relay between members of a LOBBY, so ready-state and "start" signals work.
##
## Reads tests/test_config.json ({ "url", "key" }), then:
##   client A connects -> creates a lobby
##   client B connects -> joins the lobby by code
##   A broadcasts a "ready" event; B must receive it
##   B broadcasts "ready"; A must receive it; then A broadcasts "start"; B must receive it
## Prints "SMOKE: PASS" / exit 0 on success.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url := ""
var _key := ""
var _code := ""
var _a: Node
var _b: Node
var _a_saw_b_ready := false
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

	_a.connection_error.connect(func(r): _fail("A connection_error: " + r))
	_b.connection_error.connect(func(r): _fail("B connection_error: " + r))
	_a.sdk_error.connect(func(e): _fail("A error: " + str(e.get("code", ""))))
	_b.sdk_error.connect(func(e): _fail("B error: " + str(e.get("code", ""))))

	_a.lobby_created.connect(_on_a_lobby_created)
	_a.event_received.connect(_on_a_event)
	_b.lobby_joined.connect(_on_b_lobby_joined)
	_b.event_received.connect(_on_b_event)

	_a.connected.connect(func(): _a.create_lobby("Ready Test", "public", 4, {}, "Host"))
	_a.configure(_key)
	_a.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_a_lobby_created(lobby: Dictionary) -> void:
	_code = str(lobby.get("code", lobby.get("roomCode", "")))
	print("SMOKE: A created lobby code=", _code)
	if _code == "":
		_fail("lobby had no code")
		return
	_b.connected.connect(func(): _b.join_lobby(_code, "Guest"))
	_b.configure(_key)
	_b.connect_to_server(_url)


func _on_b_lobby_joined(_lobby_id: String, player: Dictionary, players: Array) -> void:
	if str(player.get("playerId", "")) != _b.player_id:
		return
	print("SMOKE: B joined lobby players=", players.size())
	# A announces ready; B should receive it (event relay inside a lobby).
	_a.send_event("ready", {"ready": true})


func _on_b_event(event: String, data: Dictionary, _from: String) -> void:
	if event == "ready":
		print("SMOKE: B received A's ready")
		# Reply so we prove the other direction too.
		_b.send_event("ready", {"ready": true})
	elif event == "start":
		print("SMOKE: B received start")
		print("SMOKE: PASS")
		_quit(0)


func _on_a_event(event: String, _data: Dictionary, _from: String) -> void:
	if event == "ready" and not _a_saw_b_ready:
		_a_saw_b_ready = true
		print("SMOKE: A received B's ready -> host starts")
		_a.send_event("start", {})


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
