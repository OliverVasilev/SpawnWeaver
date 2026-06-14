extends Node
## Headless test for the Milestone 20 SDK polish: structured errors, debug mode, latency
## tracking, and the copyable debug report.
##
## Reads tests/test_config.json ({ "url": "...", "key": "pk_..." }), then:
##   connects with debug logging on
##   triggers a server error (join a non-existent room) and asserts a structured sdk_error
##   builds a debug report and asserts it contains the player id + recent messages
## Prints "SMOKE: PASS" and exits 0 on success, or "SMOKE: FAIL ..." and exits 1.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _client: Node
var _url: String = ""
var _got_structured_error := false
var _done := false


func _ready() -> void:
	var cfg = JSON.parse_string(FileAccess.get_file_as_string("res://tests/test_config.json"))
	if typeof(cfg) != TYPE_DICTIONARY:
		_fail("could not read tests/test_config.json")
		return
	_url = str(cfg["url"])

	_client = ClientScript.new()
	add_child(_client)
	_client.set_debug_enabled(true)
	_client.connection_error.connect(func(r): _fail("connection_error: " + r))
	_client.sdk_error.connect(_on_sdk_error)
	_client.authenticated.connect(_on_authenticated)

	_client.configure(str(cfg["key"]))
	_client.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_authenticated(player_id: String, _token: String) -> void:
	print("SMOKE: authenticated as ", player_id)
	# Joining a room code that does not exist must produce a structured error.
	_client.join_room("ZZZZZZ", "Tester")


func _on_sdk_error(error: Dictionary) -> void:
	print("SMOKE: sdk_error ", error)
	if not error.has("code") or not error.has("message") or not error.has("retryable"):
		_fail("error missing structured fields")
		return
	if str(error["code"]) != "room_not_found":
		_fail("unexpected error code: " + str(error["code"]))
		return
	_got_structured_error = true
	_verify_report()


func _verify_report() -> void:
	var report: Dictionary = _client.create_debug_report()
	print("SMOKE: report keys=", report.keys())
	if str(report.get("sdk_version", "")) == "":
		_fail("report missing sdk_version")
		return
	if str(report.get("player_id", "")) == "":
		_fail("report missing player_id")
		return
	if not (report.get("recent_messages", []) as Array).size() > 0:
		_fail("report has no recent messages")
		return
	if (report.get("last_errors", []) as Array).is_empty():
		_fail("report has no recorded errors")
		return
	var report_string: String = _client.create_debug_report_string()
	if not report_string.contains("sdk_version"):
		_fail("report string is not valid")
		return
	print("SMOKE: PASS")
	_quit(0)


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
