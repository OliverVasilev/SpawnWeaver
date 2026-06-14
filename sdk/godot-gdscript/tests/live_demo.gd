extends Node
## Live dashboard demo seeder. Connects several SDK clients and KEEPS them connected so the
## dashboard shows live data: active connections, a lobby (with host + ready events), a room
## exchanging events, a waiting matchmaking queue, and a recorded error.
##
## Reads tests/test_config.json ({ "url", "key" }) and runs until the process is killed.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url := ""
var _key := ""

var _host: Node       # creates a lobby
var _guest: Node      # joins the lobby
var _carol: Node      # creates a room
var _dave: Node       # joins the room
var _queued: Node     # sits in matchmaking

var _lobby_code := ""
var _room_code := ""
var _room_ready := false
var _tick := 0.0


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		push_error("could not read tests/test_config.json")
		get_tree().quit(1)
		return
	_url = str(cfg["url"])
	_key = str(cfg["key"])

	_host = _make_client()
	_guest = _make_client()
	_carol = _make_client()
	_dave = _make_client()
	_queued = _make_client()

	# Lobby: host creates, guest joins, both ready up (ready relayed as room events).
	_host.lobby_created.connect(_on_lobby_created)
	_host.connected.connect(func(): _host.create_lobby("Demo Lobby", "public", 8, {"map": "forest", "mode": "coop"}, "HostAlice"))

	# Room: carol creates, dave joins, they exchange move events. Carol also triggers an error.
	_carol.room_created.connect(_on_room_created)
	_carol.connected.connect(func():
		_carol.create_room("Carol")
		_carol.join_room("BADCOD", "Carol"))   # intentional room_not_found for the Error Explorer

	# Matchmaking: quinn queues for a 1v1 and waits (backend started with a long MM timeout).
	_queued.connected.connect(func(): _queued.join_matchmaking("duel_1v1", "global", 2, "QueuedQuinn"))

	print("DEMO: connecting clients to ", _url)
	_host.configure(_key); _host.connect_to_server(_url)
	_carol.configure(_key); _carol.connect_to_server(_url)
	_queued.configure(_key); _queued.connect_to_server(_url)


func _make_client() -> Node:
	var c := ClientScript.new()
	add_child(c)
	return c


func _on_lobby_created(lobby: Dictionary) -> void:
	_lobby_code = str(lobby.get("code", lobby.get("roomCode", "")))
	print("DEMO: lobby created code=", _lobby_code)
	_guest.lobby_joined.connect(func(_id, _p, _players):
		_host.send_event("ready", {"ready": true})
		_guest.send_event("ready", {"ready": true}))
	_guest.connected.connect(func(): _guest.join_lobby(_lobby_code, "BobGuest"))
	_guest.configure(_key); _guest.connect_to_server(_url)


func _on_room_created(_room_id: String, room_code: String, _players: Array) -> void:
	_room_code = room_code
	print("DEMO: room created code=", _room_code)
	_dave.room_joined.connect(func(_rid, _rc, _pl, _players): _set_room_ready())
	_dave.connected.connect(func(): _dave.join_room(_room_code, "Dave"))
	_dave.configure(_key); _dave.connect_to_server(_url)


func _set_room_ready() -> void:
	_room_ready = true
	print("DEMO: room is active; exchanging move events")


func _process(delta: float) -> void:
	# Keep some live traffic flowing so message counters and timelines stay fresh.
	if not _room_ready:
		return
	_tick += delta
	if _tick >= 2.0:
		_tick = 0.0
		_carol.send_event("player_moved", {"x": randi() % 400, "y": randi() % 300})
		_dave.send_event("player_moved", {"x": randi() % 400, "y": randi() % 300})
