extends Node
## Headless lobby test: host creates a public lobby; a second client lists it and joins
## by id. Prints "SMOKE: PASS" / exit 0 on success.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url: String = ""
var _key: String = ""
var _host: Node
var _joiner: Node
var _lobby_id: String = ""
var _done := false


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])
	_key = str(cfg["key"])

	_host = ClientScript.new()
	_joiner = ClientScript.new()
	add_child(_host)
	add_child(_joiner)

	_host.connection_error.connect(func(r): _fail("host conn error: " + r))
	_joiner.connection_error.connect(func(r): _fail("joiner conn error: " + r))
	_host.error_received.connect(func(c, m): _fail("host error: " + c))
	_joiner.error_received.connect(func(c, m): _fail("joiner error: " + c))

	_host.lobby_created.connect(_on_lobby_created)
	_joiner.lobby_list.connect(_on_lobby_list)
	_joiner.lobby_joined.connect(_on_lobby_joined)

	_host.connected.connect(func(): _host.create_lobby("Arena", "public", 4, {"mode": "ffa"}, "Host"))
	_host.configure(_key)
	_host.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_lobby_created(lobby: Dictionary) -> void:
	_lobby_id = str(lobby.get("lobbyId", ""))
	print("SMOKE: lobby created id=", _lobby_id, " players=", lobby.get("players", []).size())
	_joiner.connected.connect(func(): _joiner.list_lobbies())
	_joiner.configure(_key)
	_joiner.connect_to_server(_url)


func _on_lobby_list(lobbies: Array) -> void:
	print("SMOKE: list returned ", lobbies.size(), " lobby(ies)")
	for lobby in lobbies:
		if str(lobby.get("lobbyId", "")) == _lobby_id:
			_joiner.join_lobby_by_id(_lobby_id, "Joiner")
			return
	_fail("public lobby not found in list")


func _on_lobby_joined(lobby_id: String, _player: Dictionary, players: Array) -> void:
	print("SMOKE: joined lobby ", lobby_id, " players=", players.size())
	if players.size() == 2:
		print("SMOKE: PASS")
		_quit(0)
	else:
		_fail("expected 2 players, got %d" % players.size())


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
