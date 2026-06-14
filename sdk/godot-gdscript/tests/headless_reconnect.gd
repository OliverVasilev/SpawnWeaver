extends Node
## Headless reconnect test: connects, captures the player id + token, disconnects, then
## reconnects (token reused automatically) and asserts the player id is unchanged.
## Prints "SMOKE: PASS" / exit 0 on success.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _client: Node
var _url: String = ""
var _phase := 0
var _player_id_1 := ""
var _done := false


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])

	_client = ClientScript.new()
	add_child(_client)
	_client.connection_error.connect(func(r): _fail("connection_error: " + r))
	_client.authenticated.connect(_on_authenticated)
	_client.disconnected.connect(_on_disconnected)

	_client.configure(str(cfg["key"]))
	_client.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_authenticated(player_id: String, player_token: String) -> void:
	if _phase == 0:
		_player_id_1 = player_id
		print("SMOKE: first connect player_id=", player_id, " token_len=", player_token.length())
		_phase = 1
		_client.disconnect_from_server()
	elif _phase == 2:
		print("SMOKE: reconnect player_id=", player_id)
		if player_id == _player_id_1:
			print("SMOKE: PASS")
			_quit(0)
		else:
			_fail("player id changed on reconnect (%s != %s)" % [player_id, _player_id_1])


func _on_disconnected() -> void:
	if _phase == 1:
		_phase = 2
		_client.connect_to_server(_url) # token reused automatically


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
