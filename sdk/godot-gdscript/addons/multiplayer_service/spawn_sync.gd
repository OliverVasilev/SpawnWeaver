@icon("res://addons/multiplayer_service/editor/spawn_sync_icon.svg")
class_name SpawnSync
extends Node
## Drop-in network sync for a Node2D or Node3D.
##
## Add this as a CHILD of the node you want to replicate (a player, a pickup, a projectile),
## then:
##   - On the copy this client controls, tick [member local] = true. It registers a SpawnWeaver
##     entity and sends this node's transform (+ any [member synced_properties]) several times a
##     second.
##   - On copies that represent OTHER players, leave [member local] = false. This node listens for
##     that entity's updates and smoothly interpolates the parent toward them.
##
## Requires the SpawnWeaver plugin (the [code]MultiplayerService[/code] autoload). The entity is
## created when you connect/register and deleted automatically when this node leaves the tree.
##
## Only primitive values (numbers / strings / bools) can be listed in [member synced_properties]
## — for a [Color] or [Vector2], sync the individual components.

## Stable id for this entity. Leave empty on a [member local] node to use your own player id
## (good for a one-avatar-per-player game). Spawned/owned objects need a unique id you set.
@export var entity_id: String = ""

## True on the single copy this client owns and controls (it sends; it never interpolates).
@export var local: bool = false

@export_group("Transform")
@export var sync_position: bool = true
@export var sync_rotation: bool = true
@export var sync_scale: bool = false

@export_group("Custom properties")
## Names of parent properties to replicate (e.g. "hp", "team"). Primitives only.
@export var synced_properties: PackedStringArray = PackedStringArray()

@export_group("Sending")
## How many transform updates per second a [member local] node sends (kept under the rate limit).
@export_range(1, 30, 1) var send_rate: float = 20.0

@export_group("Interpolation (remote)")
## Smooth remote copies toward incoming state instead of snapping.
@export var interpolate: bool = true
## Higher = snappier follow, lower = smoother/laggier. Frame-rate independent.
@export_range(1.0, 40.0, 0.5) var interpolation_speed: float = 16.0
## Free the parent automatically when this entity is deleted on the server.
@export var free_parent_on_delete: bool = true

var _parent: Node = null
var _is_2d := false
var _is_3d := false
var _registered := false
var _send_accum := 0.0
var _has_target := false

var _t_pos2 := Vector2.ZERO
var _t_pos3 := Vector3.ZERO
var _t_rot2 := 0.0
var _t_rot3 := Vector3.ZERO
var _t_scale2 := Vector2.ONE
var _t_scale3 := Vector3.ONE


func _ready() -> void:
	_parent = get_parent()
	_is_2d = _parent is Node2D
	_is_3d = _parent is Node3D
	if not _is_2d and not _is_3d:
		push_warning("SpawnSync: parent is neither Node2D nor Node3D; only custom properties will sync.")
	_capture_targets_from_parent()

	MultiplayerService.entity_state_changed.connect(_on_entity_changed)
	MultiplayerService.entity_state_deleted.connect(_on_entity_deleted)
	MultiplayerService.state_snapshot_received.connect(_on_snapshot)

	if local:
		if MultiplayerService.is_connected_to_server():
			_register()
		else:
			# Register once the connection (and our player id) is ready.
			MultiplayerService.welcomed.connect(func(_id): _register(), CONNECT_ONE_SHOT)


func _exit_tree() -> void:
	# Owned entities clean themselves up so other players see them disappear.
	if local and _registered and MultiplayerService.is_connected_to_server():
		MultiplayerService.delete_entity_state(entity_id)


func _process(delta: float) -> void:
	if local:
		if not _registered:
			return
		_send_accum += delta
		if _send_accum >= 1.0 / maxf(1.0, send_rate):
			_send_accum = 0.0
			MultiplayerService.patch_entity_state(entity_id, _build_state())
	elif interpolate and _has_target:
		_apply_interpolated(delta)


## Sets the controlling client and (re)registers. Useful when spawning a copy in code.
func set_local(is_local: bool) -> void:
	local = is_local
	if local and not _registered and MultiplayerService.is_connected_to_server():
		_register()


func _register() -> void:
	if _registered:
		return
	if entity_id == "":
		entity_id = MultiplayerService.player_id
	if entity_id == "":
		return
	_registered = true
	MultiplayerService.set_entity_state(entity_id, _build_state())


# --- Sending (local) ---

func _build_state() -> Dictionary:
	var s := {}
	if _is_2d:
		if sync_position:
			s["x"] = _parent.position.x
			s["y"] = _parent.position.y
		if sync_rotation:
			s["rot"] = _parent.rotation
		if sync_scale:
			s["sx"] = _parent.scale.x
			s["sy"] = _parent.scale.y
	elif _is_3d:
		if sync_position:
			s["x"] = _parent.position.x
			s["y"] = _parent.position.y
			s["z"] = _parent.position.z
		if sync_rotation:
			s["rx"] = _parent.rotation.x
			s["ry"] = _parent.rotation.y
			s["rz"] = _parent.rotation.z
		if sync_scale:
			s["sx"] = _parent.scale.x
			s["sy"] = _parent.scale.y
			s["sz"] = _parent.scale.z
	for prop in synced_properties:
		s[prop] = _parent.get(prop)
	return s


# --- Receiving (remote) ---

func _on_entity_changed(id: String, _patch: Dictionary, full: Dictionary) -> void:
	if local or id != entity_id:
		return
	_set_targets_from_state(full)
	_apply_custom_properties(full)
	_has_target = true
	if not interpolate:
		_snap_to_targets()


func _on_snapshot(snapshot: Dictionary) -> void:
	if local:
		return
	for e in snapshot.get("entities", []):
		if str(e.get("entityId", "")) == entity_id:
			var state: Dictionary = e.get("state", {})
			_set_targets_from_state(state)
			_apply_custom_properties(state)
			_snap_to_targets()      # start exactly where the entity is
			_has_target = true
			return


func _on_entity_deleted(id: String) -> void:
	if local or id != entity_id:
		return
	if free_parent_on_delete and is_instance_valid(_parent):
		_parent.queue_free()


func _set_targets_from_state(s: Dictionary) -> void:
	if _is_2d:
		if sync_position and s.has("x") and s.has("y"):
			_t_pos2 = Vector2(float(s["x"]), float(s["y"]))
		if sync_rotation and s.has("rot"):
			_t_rot2 = float(s["rot"])
		if sync_scale and s.has("sx") and s.has("sy"):
			_t_scale2 = Vector2(float(s["sx"]), float(s["sy"]))
	elif _is_3d:
		if sync_position and s.has("x") and s.has("y") and s.has("z"):
			_t_pos3 = Vector3(float(s["x"]), float(s["y"]), float(s["z"]))
		if sync_rotation and s.has("rx") and s.has("ry") and s.has("rz"):
			_t_rot3 = Vector3(float(s["rx"]), float(s["ry"]), float(s["rz"]))
		if sync_scale and s.has("sx") and s.has("sy") and s.has("sz"):
			_t_scale3 = Vector3(float(s["sx"]), float(s["sy"]), float(s["sz"]))


func _apply_custom_properties(s: Dictionary) -> void:
	for prop in synced_properties:
		if s.has(prop):
			_parent.set(prop, s[prop])


func _apply_interpolated(delta: float) -> void:
	var t := 1.0 - exp(-interpolation_speed * delta)
	if _is_2d:
		if sync_position:
			_parent.position = _parent.position.lerp(_t_pos2, t)
		if sync_rotation:
			_parent.rotation = lerp_angle(_parent.rotation, _t_rot2, t)
		if sync_scale:
			_parent.scale = _parent.scale.lerp(_t_scale2, t)
	elif _is_3d:
		if sync_position:
			_parent.position = _parent.position.lerp(_t_pos3, t)
		if sync_rotation:
			_parent.rotation = Vector3(
				lerp_angle(_parent.rotation.x, _t_rot3.x, t),
				lerp_angle(_parent.rotation.y, _t_rot3.y, t),
				lerp_angle(_parent.rotation.z, _t_rot3.z, t))
		if sync_scale:
			_parent.scale = _parent.scale.lerp(_t_scale3, t)


func _snap_to_targets() -> void:
	if _is_2d:
		if sync_position:
			_parent.position = _t_pos2
		if sync_rotation:
			_parent.rotation = _t_rot2
		if sync_scale:
			_parent.scale = _t_scale2
	elif _is_3d:
		if sync_position:
			_parent.position = _t_pos3
		if sync_rotation:
			_parent.rotation = _t_rot3
		if sync_scale:
			_parent.scale = _t_scale3


func _capture_targets_from_parent() -> void:
	if _is_2d:
		_t_pos2 = _parent.position
		_t_rot2 = _parent.rotation
		_t_scale2 = _parent.scale
	elif _is_3d:
		_t_pos3 = _parent.position
		_t_rot3 = _parent.rotation
		_t_scale3 = _parent.scale
