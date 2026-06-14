extends Node
## Headless auto-reconnect test: connect, then simulate an unexpected drop (close the
## underlying socket without calling disconnect_from_server). The SDK should automatically
## reconnect (reusing the token) and resume the same player id. Prints "SMOKE: PASS".

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _client: Node
var _url: String = ""
var _player_id_1 := ""
var _phase := 0
var _done := false


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])

	_client = ClientScript.new()
	add_child(_client)
	_client.auto_reconnect = true
	_client.connection_error.connect(func(r): _fail("connection_error: " + r))
	_client.reconnect_failed.connect(func(): _fail("reconnect gave up"))
	_client.reconnecting.connect(func(attempt): print("SMOKE: reconnecting attempt ", attempt))
	_client.authenticated.connect(_on_authenticated)

	_client.configure(str(cfg["key"]))
	_client.connect_to_server(_url)

	get_tree().create_timer(12.0).timeout.connect(func(): _fail("timed out"))


func _on_authenticated(player_id: String, _token: String) -> void:
	if _phase == 0:
		_player_id_1 = player_id
		print("SMOKE: connected as ", player_id, "; simulating unexpected drop")
		_phase = 1
		# Force-close the underlying socket WITHOUT setting _user_closed (an unexpected drop).
		_client._socket.close()
	elif _phase == 1:
		print("SMOKE: auto-reconnected as ", player_id)
		if player_id == _player_id_1:
			print("SMOKE: PASS")
			_quit(0)
		else:
			_fail("player id changed after auto-reconnect")


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
