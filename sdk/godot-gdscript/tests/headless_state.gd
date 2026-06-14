extends Node
## Headless state-sync test (Milestone 23). Reads tests/test_config.json ({ "url", "key" }):
##   host A creates a room, sets room state + an entity
##   B joins -> receives a full snapshot (room state + entity)
##   A patches the entity -> B receives entity_state_changed (merged)
##   B tries to patch A's entity -> state_update_rejected (state_forbidden)
## Prints "SMOKE: PASS" / exit 0 on success.

const ClientScript := preload("res://addons/multiplayer_service/multiplayer_service.gd")

var _url := ""
var _key := ""
var _code := ""
var _a: Node
var _b: Node
var _b_started := false
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
	_a.state_update_rejected.connect(func(e): _fail("A unexpected rejection: " + str(e.get("code", ""))))

	_a.room_created.connect(_on_a_room_created)
	_a.entity_state_changed.connect(_on_a_entity_changed)
	_b.state_snapshot_received.connect(_on_b_snapshot)
	_b.entity_state_changed.connect(_on_b_entity_changed)
	_b.state_update_rejected.connect(_on_b_rejected)

	_a.connected.connect(func(): _a.create_room("Host"))
	_a.configure(_key)
	_a.connect_to_server(_url)

	get_tree().create_timer(10.0).timeout.connect(func(): _fail("timed out"))


func _on_a_room_created(_room_id: String, room_code: String, _players: Array) -> void:
	_code = room_code
	print("SMOKE: A created room ", _code, "; seeding state")
	_a.patch_room_state({"phase": "combat"})
	_a.set_entity_state("boss", {"hp": 100})


func _on_a_entity_changed(entity_id: String, _patch: Dictionary, full: Dictionary) -> void:
	# Wait until A's own entity is confirmed set, then bring B in.
	if entity_id == "boss" and int(full.get("hp", 0)) == 100 and not _b_started:
		_b_started = true
		_b.connected.connect(func(): _b.join_room(_code, "Guest"))
		_b.configure(_key)
		_b.connect_to_server(_url)


func _on_b_snapshot(snapshot: Dictionary) -> void:
	print("SMOKE: B snapshot ", snapshot)
	var room_state: Dictionary = snapshot.get("roomState", {})
	var entities: Array = snapshot.get("entities", [])
	if str(room_state.get("phase", "")) != "combat":
		_fail("snapshot missing room state")
		return
	if entities.is_empty() or str(entities[0].get("entityId", "")) != "boss":
		_fail("snapshot missing entity")
		return
	# Now A patches the entity; B should see the merged change.
	_a.patch_entity_state("boss", {"hp": 50})


func _on_b_entity_changed(entity_id: String, _patch: Dictionary, full: Dictionary) -> void:
	if entity_id == "boss" and int(full.get("hp", -1)) == 50:
		print("SMOKE: B saw entity patch hp=50; trying to patch a non-owned entity")
		_b.patch_entity_state("boss", {"hp": 1})   # B does not own it -> rejected


func _on_b_rejected(error: Dictionary) -> void:
	print("SMOKE: B rejected ", error)
	if str(error.get("code", "")) == "state_forbidden":
		print("SMOKE: PASS")
		_quit(0)
	else:
		_fail("unexpected rejection code: " + str(error.get("code", "")))


func _fail(message: String) -> void:
	if _done:
		return
	push_error("SMOKE FAIL: " + message)
	print("SMOKE: FAIL ", message)
	_quit(1)


func _quit(code: int) -> void:
	_done = true
	get_tree().quit(code)
