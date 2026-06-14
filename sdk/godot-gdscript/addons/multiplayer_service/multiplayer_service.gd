extends Node
## SpawnWeaver multiplayer client (autoload singleton "MultiplayerService").
##
## Minimal usage:
##     MultiplayerService.configure("pk_your_public_key")
##     MultiplayerService.connect_to_server("ws://127.0.0.1:5000/connect")
##     # ... on the `connected` signal:
##     MultiplayerService.create_room("Alice")
##     MultiplayerService.join_room("ABCD12", "Bob")
##     MultiplayerService.send_event("player_moved", {"x": 10, "y": 5})
##
## Guest authentication is automatic: connecting mints a stable player identity and a
## reconnect token (see the `authenticated` signal). The SDK auto-reconnects after an
## unexpected drop, detects heartbeat timeouts, and can produce a copyable debug report
## (`create_debug_report()`), all of which are surfaced as Godot signals.

## SDK version, reported in the debug report.
const SDK_VERSION := "0.3.0"

## Where the editor plugin saves project credentials (see the SpawnWeaver editor dock).
const CONFIG_PATH := "res://addons/multiplayer_service/spawnweaver.cfg"

## Fallback server URL when none is configured.
const DEFAULT_SERVER_URL := "ws://127.0.0.1:5000/connect"

# --- Connection lifecycle ---
signal connected()
signal disconnected()
signal connection_error(reason: String)
signal welcomed(connection_id: String)
## Emitted on connect with the stable player identity and the token to reuse on reconnect.
signal authenticated(player_id: String, player_token: String)
## Emitted before each automatic reconnect attempt (1-based).
signal reconnecting(attempt: int)
## Emitted when a reconnect attempt succeeds (after `reconnecting`). `connected` also fires.
signal reconnected()
## Emitted when automatic reconnection gives up after max_reconnect_attempts.
signal reconnect_failed()

# --- Rooms ---
signal room_created(room_id: String, room_code: String, players: Array)
signal room_joined(room_id: String, room_code: String, player: Dictionary, players: Array)
signal player_joined(room_id: String, player: Dictionary)
signal player_left(room_id: String, player_id: String)
signal room_players(room_id: String, players: Array)
signal room_expired(room_id: String)

# --- Lobbies ---
signal lobby_created(lobby: Dictionary)
signal lobby_list(lobbies: Array)
signal lobby_joined(lobby_id: String, player: Dictionary, players: Array)
signal lobby_closed(lobby_id: String)

# --- Game events ---
signal event_received(event: String, data: Dictionary, from_player_id: String)

# --- Matchmaking ---
signal matchmaking_queued(game_mode: String, region: String, match_size: int)
signal match_found(room_id: String, room_code: String, players: Array)
signal matchmaking_left()
signal matchmaking_timeout(game_mode: String, region: String)

# --- State sync (Milestone 23) ---
## Full current room + entity state, sent when you join a room that already has state.
signal state_snapshot_received(snapshot: Dictionary)
## Room-level state changed: the applied patch and the resulting full state.
signal room_state_changed(patch: Dictionary, state: Dictionary)
## An entity changed: its id, the applied patch, and the resulting full state.
signal entity_state_changed(entity_id: String, patch: Dictionary, full_state: Dictionary)
## An entity was deleted.
signal entity_state_deleted(entity_id: String)
## A state update you sent was rejected: { code, message, target, retryable }.
signal state_update_rejected(error: Dictionary)

# --- Errors ---
## Back-compat: a server error, by code + message.
signal error_received(code: String, message: String)
## Structured error: { code, message, details, retryable }. See the SDK README error table.
signal sdk_error(error: Dictionary)

enum State { DISCONNECTED, CONNECTING, CONNECTED }

## Human-friendly descriptions for the error codes carried by `error_received` / `sdk_error`.
const ERROR_DESCRIPTIONS := {
	"malformed_message": "The message sent to the server was not valid.",
	"unknown_message_type": "The server did not recognize that message type.",
	"invalid_payload": "A required field was missing or invalid.",
	"room_not_found": "That room or lobby does not exist — check the code.",
	"room_full": "That room or lobby is full.",
	"payload_too_large": "The message was too large.",
	"rate_limited": "You are sending messages too quickly; slow down.",
}

## Error codes the caller can safely retry (after a short delay). Everything else is fatal
## to the in-flight request and usually indicates a bug or bad input.
const RETRYABLE_ERRORS := {
	"rate_limited": true,
}

const _MAX_RECENT_MESSAGES := 50
const _MAX_RECENT_ERRORS := 10
const _MAX_PING_SAMPLES := 20

var connection_id: String = ""
## Stable player identity (persists across reconnects when reusing player_token).
var player_id: String = ""
## Reconnect credential. Stored automatically; reused on the next connect_to_server().
var player_token: String = ""
## The room/lobby/match this client is currently in, or "" if none. Maintained automatically.
var current_room_id: String = ""
## Kind of the current room: "", "room", "lobby", or "match". Maintained automatically.
var current_room_kind: String = ""
## When true, the SDK automatically reconnects (reusing the token) after an unexpected drop.
var auto_reconnect := true
## Maximum automatic reconnect attempts before emitting reconnect_failed.
var max_reconnect_attempts := 5
## Seconds between application-level heartbeat pings while connected (also feeds latency stats).
var heartbeat_interval := 15.0
## If no message arrives from the server for this many seconds, treat the link as dropped
## and reconnect. 0 disables timeout detection (the WebSocket keep-alive still applies).
var heartbeat_timeout := 45.0
## When true, re-join the last room/lobby (by code) after a successful reconnect.
var rejoin_last_room_on_reconnect := false
## The reason for the most recent disconnect ("" if none / clean). Useful for debugging.
var last_disconnect_reason: String = ""

var _socket := WebSocketPeer.new()
var _state: State = State.DISCONNECTED
var _project_key: String = ""
var _last_url: String = ""
var _user_closed := false
var _reconnecting := false
var _reconnect_attempts := 0
var _debug := false

# Diagnostics state (debug report + heartbeat/latency).
var _recent_messages: Array = []
var _recent_errors: Array = []
var _last_inbound_msec: int = 0
var _last_ping_msec: int = 0
var _ping_seq: int = 0
var _ping_inflight: Dictionary = {}
var _ping_samples: Array = []

# Reconnect bookkeeping.
var _pending_rejoin := false
var _last_room_code: String = ""


func _ready() -> void:
	set_process(true)


## Sets the public project key appended to the connection URL as `projectKey`.
func configure(project_key: String) -> void:
	_project_key = project_key


## Loads the credentials saved by the editor plugin. Returns { public_key, server_url, debug }.
func load_config() -> Dictionary:
	var cfg := ConfigFile.new()
	if cfg.load(CONFIG_PATH) != OK:
		return {}
	return {
		"public_key": str(cfg.get_value("project", "public_key", "")),
		"server_url": str(cfg.get_value("project", "server_url", DEFAULT_SERVER_URL)),
		"debug": bool(cfg.get_value("project", "debug_enabled", false)),
	}


## Connects using the credentials saved by the editor plugin. Returns false if none are set.
func connect_using_config() -> bool:
	var config := load_config()
	var key := str(config.get("public_key", ""))
	if key == "":
		push_warning("MultiplayerService: no saved config — set credentials in the SpawnWeaver editor dock.")
		return false
	if bool(config.get("debug", false)):
		set_debug_enabled(true)
	configure(key)
	connect_to_server(str(config.get("server_url", DEFAULT_SERVER_URL)))
	return true


## Sets the reconnect token to present on the next connect (normally stored automatically).
func set_player_token(token: String) -> void:
	player_token = token


## Enables/disables verbose SDK logging (connection, auth, room/lobby/matchmaking, messages,
## rejections, disconnects). Off by default.
func set_debug_enabled(enabled: bool) -> void:
	_debug = enabled
	_log("debug", "debug logging %s" % ("enabled" if enabled else "disabled"))


func is_debug_enabled() -> bool:
	return _debug


func is_connected_to_server() -> bool:
	return _state == State.CONNECTED


## Opens a WebSocket connection. `url` is e.g. "ws://127.0.0.1:5000/connect".
func connect_to_server(url: String) -> void:
	_last_url = url
	_user_closed = false
	_reconnecting = false
	_reconnect_attempts = 0
	_open_socket(url)


func disconnect_from_server() -> void:
	_user_closed = true
	_log("connection", "disconnect requested by app")
	_socket.close()


## Returns a human-friendly description for an error code from `error_received` / `sdk_error`.
func describe_error(code: String) -> String:
	return ERROR_DESCRIPTIONS.get(code, code)


func _open_socket(url: String) -> void:
	var params: Array[String] = []
	if _project_key != "":
		params.append("projectKey=" + _project_key.uri_encode())
	if player_token != "":
		# Present the stored token to resume the same player identity.
		params.append("playerToken=" + player_token.uri_encode())
	# Report SDK + engine versions for the dashboard's connection inspector (Milestone 22).
	params.append("sdkVersion=" + SDK_VERSION.uri_encode())
	params.append("engine=" + ("Godot " + str(Engine.get_version_info().get("string", ""))).uri_encode())

	var full_url := url
	if not params.is_empty():
		full_url += ("&" if url.contains("?") else "?") + "&".join(params)

	_log("connection", "connecting to %s" % url)
	var err := _socket.connect_to_url(full_url)
	if err != OK:
		_log("connection", "connect_to_url failed (error %d)" % err)
		connection_error.emit("Could not start connection (error %d). Is the URL correct?" % err)
		return
	_state = State.CONNECTING


func _schedule_reconnect() -> void:
	if _reconnect_attempts >= max_reconnect_attempts:
		_reconnecting = false
		_log("reconnect", "giving up after %d attempts" % _reconnect_attempts)
		reconnect_failed.emit()
		return
	_reconnect_attempts += 1
	var delay := minf(pow(2.0, _reconnect_attempts - 1), 30.0)
	_log("reconnect", "attempt %d in %.1fs" % [_reconnect_attempts, delay])
	reconnecting.emit(_reconnect_attempts)
	get_tree().create_timer(delay).timeout.connect(_attempt_reconnect, CONNECT_ONE_SHOT)


func _attempt_reconnect() -> void:
	if _user_closed:
		_reconnecting = false
		return
	_open_socket(_last_url)


func create_room(player_name: String = "") -> void:
	var payload := {}
	if player_name != "":
		payload["playerName"] = player_name
	_send("room.create", payload)


func join_room(room_code: String, player_name: String = "") -> void:
	var payload := {"roomCode": room_code}
	if player_name != "":
		payload["playerName"] = player_name
	_send("room.join", payload)


func leave_room(room_id: String) -> void:
	_send("room.leave", {"roomId": room_id})


func list_players(room_id: String) -> void:
	_send("room.players", {"roomId": room_id})


## Creates a lobby. visibility is "public" (listed) or "private" (join by code only).
## max_players <= 0 means unlimited. Replies via the `lobby_created` signal.
func create_lobby(name: String = "", visibility: String = "public", max_players: int = 0, metadata: Dictionary = {}, player_name: String = "") -> void:
	var payload := {"visibility": visibility}
	if name != "":
		payload["name"] = name
	if max_players > 0:
		payload["maxPlayers"] = max_players
	if not metadata.is_empty():
		payload["metadata"] = metadata
	if player_name != "":
		payload["playerName"] = player_name
	_send("lobby.create", payload)


## Requests the list of public lobbies. Replies via the `lobby_list` signal.
func list_lobbies() -> void:
	_send("lobby.list", {})


## Joins a lobby by its code (works for public and private). Replies via `lobby_joined`.
func join_lobby(code: String, player_name: String = "") -> void:
	var payload := {"code": code}
	if player_name != "":
		payload["playerName"] = player_name
	_send("lobby.join", payload)


## Joins a public lobby by its id (from the list). Replies via `lobby_joined`.
func join_lobby_by_id(lobby_id: String, player_name: String = "") -> void:
	var payload := {"lobbyId": lobby_id}
	if player_name != "":
		payload["playerName"] = player_name
	_send("lobby.join", payload)


## Sends a game event to the room; the server relays it to the other members,
## who receive it via the `event_received` signal. `room_id` defaults to the room/lobby
## you're currently in (`current_room_id`), so you usually don't need to pass it.
func send_event(event: String, data: Dictionary = {}, room_id: String = "") -> void:
	var target := room_id if room_id != "" else current_room_id
	var payload := {"event": event, "data": data}
	if target != "":
		payload["roomId"] = target
	_send("game.event", payload)


## Enters the matchmaking queue. region defaults to "global"; match_size <= 0 means 2.
## Replies via `matchmaking_queued`, then `match_found` or `matchmaking_timeout`.
func join_matchmaking(game_mode: String, region: String = "", match_size: int = 0, player_name: String = "") -> void:
	var payload := {"gameMode": game_mode}
	if region != "":
		payload["region"] = region
	if match_size > 0:
		payload["matchSize"] = match_size
	if player_name != "":
		payload["playerName"] = player_name
	_send("matchmaking.join", payload)


## Leaves the matchmaking queue. Replies via `matchmaking_left`.
func leave_matchmaking() -> void:
	_send("matchmaking.leave", {})


# --- State sync (Milestone 23) ---
# `room_id` defaults to the room/lobby you're in (current_room_id), like send_event.

## Patches shared room-level state (room host only). Broadcasts `room_state_changed`.
func patch_room_state(patch: Dictionary, room_id: String = "") -> void:
	_send("state.room.patch", {"roomId": _state_room(room_id), "patch": patch})


## Sets (creates or replaces) an entity's full state; you become its owner.
## Broadcasts `entity_state_changed`.
func set_entity_state(entity_id: String, state: Dictionary, room_id: String = "") -> void:
	_send("state.entity.set", {"roomId": _state_room(room_id), "entityId": entity_id, "state": state})


## Merges a partial patch into an entity you own. Broadcasts `entity_state_changed`.
func patch_entity_state(entity_id: String, patch: Dictionary, room_id: String = "") -> void:
	_send("state.entity.patch", {"roomId": _state_room(room_id), "entityId": entity_id, "patch": patch})


## Deletes an entity you own. Broadcasts `entity_state_deleted`.
func delete_entity_state(entity_id: String, room_id: String = "") -> void:
	_send("state.entity.delete", {"roomId": _state_room(room_id), "entityId": entity_id})


func _state_room(room_id: String) -> String:
	return room_id if room_id != "" else current_room_id


func ping(request_id: String = "") -> void:
	_send("ping", {}, request_id)


## Most recent round-trip latency in milliseconds (-1 if never measured).
func get_ping_ms() -> float:
	if _ping_samples.is_empty():
		return -1.0
	return float(_ping_samples[_ping_samples.size() - 1])


## A copyable diagnostic snapshot (Milestone 20.7): SDK/Godot versions, connection + player
## state, the last errors, the last 50 protocol messages, and latency stats. Paste the
## string form into the dashboard debug-bundle viewer.
func create_debug_report() -> Dictionary:
	return {
		"sdk_version": SDK_VERSION,
		"godot_version": Engine.get_version_info().get("string", ""),
		"project_key": _project_key,
		"connection_state": _state_name(),
		"connection_id": connection_id,
		"player_id": player_id,
		"room_id": current_room_id,
		"room_kind": current_room_kind,
		"auto_reconnect": auto_reconnect,
		"reconnect_attempts": _reconnect_attempts,
		"last_disconnect_reason": last_disconnect_reason,
		"ping": _ping_stats(),
		"last_errors": _recent_errors.duplicate(true),
		"recent_messages": _recent_messages.duplicate(true),
	}


## The debug report as pretty-printed JSON, ready to copy into the dashboard.
func create_debug_report_string() -> String:
	return JSON.stringify(create_debug_report(), "  ")


func _process(_delta: float) -> void:
	_socket.poll()

	match _socket.get_ready_state():
		WebSocketPeer.STATE_OPEN:
			if _state == State.CONNECTING:
				_on_socket_open()
			while _socket.get_available_packet_count() > 0:
				_handle_packet(_socket.get_packet().get_string_from_utf8())
			if _state == State.CONNECTED:
				_run_heartbeat()
		WebSocketPeer.STATE_CLOSED:
			if _state == State.CONNECTING:
				_state = State.DISCONNECTED
				if _reconnecting and not _user_closed:
					_schedule_reconnect()
				else:
					last_disconnect_reason = "connection rejected"
					connection_error.emit("Connection was rejected. Check the project key, URL, or player token.")
			elif _state == State.CONNECTED:
				_state = State.DISCONNECTED
				connection_id = ""
				current_room_id = ""
				current_room_kind = ""
				last_disconnect_reason = "intentional" if _user_closed else "connection lost"
				_log("connection", "disconnected (%s)" % last_disconnect_reason)
				disconnected.emit()
				if auto_reconnect and not _user_closed:
					_reconnecting = true
					_schedule_reconnect()


func _on_socket_open() -> void:
	var was_reconnecting := _reconnecting or _reconnect_attempts > 0
	_state = State.CONNECTED
	_reconnecting = false
	_reconnect_attempts = 0
	var now := Time.get_ticks_msec()
	_last_inbound_msec = now
	_last_ping_msec = now
	_ping_inflight.clear()
	_log("connection", "connected")
	connected.emit()
	if was_reconnecting:
		_pending_rejoin = rejoin_last_room_on_reconnect and _last_room_code != ""
		_log("reconnect", "reconnected")
		reconnected.emit()


func _run_heartbeat() -> void:
	var now := Time.get_ticks_msec()

	if heartbeat_timeout > 0.0 and _last_inbound_msec > 0:
		if now - _last_inbound_msec > int(heartbeat_timeout * 1000.0):
			_log("heartbeat", "no traffic for %.0fs — forcing reconnect" % heartbeat_timeout)
			last_disconnect_reason = "heartbeat timeout"
			_socket.close()
			return

	if heartbeat_interval > 0.0 and now - _last_ping_msec >= int(heartbeat_interval * 1000.0):
		_last_ping_msec = now
		_ping_seq += 1
		var req := "hb_%d" % _ping_seq
		_ping_inflight[req] = now
		ping(req)


func _send(type: String, payload: Dictionary = {}, request_id: String = "") -> void:
	if _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		push_warning("MultiplayerService: cannot send '%s' while not connected." % type)
		_log("send", "dropped '%s' (not connected)" % type)
		return

	var envelope := {"type": type}
	if request_id != "":
		envelope["requestId"] = request_id
	if not payload.is_empty():
		envelope["payload"] = payload

	_record_message("out", type)
	if type != "ping":
		_log("send", type)
	_socket.send_text(JSON.stringify(envelope))


func _handle_packet(text: String) -> void:
	_last_inbound_msec = Time.get_ticks_msec()

	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		push_warning("MultiplayerService: received a non-object message.")
		return

	var type := str(parsed.get("type", ""))
	var request_id := str(parsed.get("requestId", ""))
	var payload: Dictionary = {}
	if typeof(parsed.get("payload")) == TYPE_DICTIONARY:
		payload = parsed["payload"]

	_record_message("in", type)
	if type != "pong":
		_log("recv", type)

	match type:
		"connection.welcome":
			connection_id = str(payload.get("connectionId", ""))
			player_id = str(payload.get("playerId", ""))
			player_token = str(payload.get("playerToken", ""))
			_log("auth", "authenticated as %s" % player_id)
			welcomed.emit(connection_id)
			authenticated.emit(player_id, player_token)
			_maybe_rejoin()
		"room.created":
			current_room_id = str(payload.get("roomId", ""))
			current_room_kind = "room"
			_last_room_code = str(payload.get("roomCode", ""))
			room_created.emit(payload.get("roomId", ""), payload.get("roomCode", ""), payload.get("players", []))
		"room.joined":
			if str(payload.get("player", {}).get("playerId", "")) == player_id:
				current_room_id = str(payload.get("roomId", ""))
				current_room_kind = "room"
				_last_room_code = str(payload.get("roomCode", ""))
			room_joined.emit(payload.get("roomId", ""), payload.get("roomCode", ""), payload.get("player", {}), payload.get("players", []))
			player_joined.emit(payload.get("roomId", ""), payload.get("player", {}))
		"room.left":
			if str(payload.get("playerId", "")) == player_id and str(payload.get("roomId", "")) == current_room_id:
				current_room_id = ""
				current_room_kind = ""
				_last_room_code = ""
			player_left.emit(payload.get("roomId", ""), payload.get("playerId", ""))
		"room.players":
			room_players.emit(payload.get("roomId", ""), payload.get("players", []))
		"room.expired":
			if str(payload.get("roomId", "")) == current_room_id:
				current_room_id = ""
				current_room_kind = ""
				_last_room_code = ""
			room_expired.emit(payload.get("roomId", ""))
		"lobby.created":
			current_room_id = str(payload.get("lobbyId", ""))
			current_room_kind = "lobby"
			_last_room_code = str(payload.get("code", payload.get("roomCode", "")))
			lobby_created.emit(payload)
		"lobby.list":
			lobby_list.emit(payload.get("lobbies", []))
		"lobby.joined":
			if str(payload.get("player", {}).get("playerId", "")) == player_id:
				current_room_id = str(payload.get("lobbyId", ""))
				current_room_kind = "lobby"
				if payload.has("code"):
					_last_room_code = str(payload.get("code", ""))
			lobby_joined.emit(payload.get("lobbyId", ""), payload.get("player", {}), payload.get("players", []))
		"lobby.closed":
			if str(payload.get("lobbyId", "")) == current_room_id:
				current_room_id = ""
				current_room_kind = ""
				_last_room_code = ""
			lobby_closed.emit(payload.get("lobbyId", ""))
		"matchmaking.queued":
			matchmaking_queued.emit(payload.get("gameMode", ""), payload.get("region", ""), int(payload.get("matchSize", 0)))
		"match.found":
			current_room_id = str(payload.get("roomId", ""))
			current_room_kind = "match"
			_last_room_code = str(payload.get("roomCode", ""))
			match_found.emit(payload.get("roomId", ""), payload.get("roomCode", ""), payload.get("players", []))
		"matchmaking.left":
			matchmaking_left.emit()
		"matchmaking.timeout":
			matchmaking_timeout.emit(payload.get("gameMode", ""), payload.get("region", ""))
		"game.event":
			event_received.emit(payload.get("event", ""), payload.get("data", {}), payload.get("fromPlayerId", ""))
		"state.snapshot":
			state_snapshot_received.emit(payload)
		"state.room.changed":
			room_state_changed.emit(payload.get("patch", {}), payload.get("state", {}))
		"state.entity.changed":
			entity_state_changed.emit(payload.get("entityId", ""), payload.get("patch", {}), payload.get("state", {}))
		"state.entity.deleted":
			entity_state_deleted.emit(payload.get("entityId", ""))
		"state.update.rejected":
			var rejected := _make_error(str(payload.get("code", "")), str(payload.get("message", "")))
			rejected["target"] = payload.get("target", null)
			state_update_rejected.emit(rejected)
		"pong":
			_on_pong(request_id)
		"error":
			_on_protocol_error(payload)
		_:
			push_warning("MultiplayerService: unhandled message type '%s'." % type)


func _on_pong(request_id: String) -> void:
	if request_id == "" or not _ping_inflight.has(request_id):
		return
	var rtt := Time.get_ticks_msec() - int(_ping_inflight[request_id])
	_ping_inflight.erase(request_id)
	_ping_samples.append(float(rtt))
	if _ping_samples.size() > _MAX_PING_SAMPLES:
		_ping_samples.pop_front()


func _on_protocol_error(payload: Dictionary) -> void:
	var code := str(payload.get("code", ""))
	var message := str(payload.get("message", ""))
	var error := _make_error(code, message, payload.get("details", {}))

	_recent_errors.append({"code": code, "message": error["message"], "t": Time.get_unix_time_from_system()})
	if _recent_errors.size() > _MAX_RECENT_ERRORS:
		_recent_errors.pop_front()

	_log("error", "%s: %s%s" % [code, error["message"], " (retryable)" if error["retryable"] else ""])
	error_received.emit(code, message)
	sdk_error.emit(error)


## Builds the structured error shape { code, message, details, retryable }.
func _make_error(code: String, message: String, details = {}) -> Dictionary:
	var resolved_details: Dictionary = details if typeof(details) == TYPE_DICTIONARY else {}
	return {
		"code": code,
		"message": message if message != "" else describe_error(code),
		"details": resolved_details,
		"retryable": bool(RETRYABLE_ERRORS.get(code, false)),
	}


func _maybe_rejoin() -> void:
	if not _pending_rejoin or _last_room_code == "":
		return
	_pending_rejoin = false
	_log("reconnect", "re-joining %s by code %s" % [current_room_kind, _last_room_code])
	if current_room_kind == "lobby":
		join_lobby(_last_room_code)
	else:
		join_room(_last_room_code)


func _record_message(direction: String, type: String) -> void:
	_recent_messages.append({"dir": direction, "type": type, "t": Time.get_ticks_msec()})
	if _recent_messages.size() > _MAX_RECENT_MESSAGES:
		_recent_messages.pop_front()


func _ping_stats() -> Dictionary:
	if _ping_samples.is_empty():
		return {"last_ms": -1.0, "avg_ms": -1.0, "min_ms": -1.0, "max_ms": -1.0, "samples": 0}
	var total := 0.0
	var lo := float(_ping_samples[0])
	var hi := float(_ping_samples[0])
	for sample in _ping_samples:
		var v := float(sample)
		total += v
		lo = minf(lo, v)
		hi = maxf(hi, v)
	return {
		"last_ms": float(_ping_samples[_ping_samples.size() - 1]),
		"avg_ms": total / _ping_samples.size(),
		"min_ms": lo,
		"max_ms": hi,
		"samples": _ping_samples.size(),
	}


func _state_name() -> String:
	match _state:
		State.CONNECTING:
			return "connecting"
		State.CONNECTED:
			return "connected"
		_:
			return "disconnected"


func _log(category: String, message: String) -> void:
	if _debug:
		print("[SpawnWeaver][%s] %s" % [category, message])
