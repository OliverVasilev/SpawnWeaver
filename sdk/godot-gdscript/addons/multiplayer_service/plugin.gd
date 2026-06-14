@tool
extends EditorPlugin
## Registers the MultiplayerService autoload and the SpawnWeaver editor dock when enabled.

const AUTOLOAD_NAME := "MultiplayerService"
const AUTOLOAD_PATH := "res://addons/multiplayer_service/multiplayer_service.gd"
const DockScript := preload("res://addons/multiplayer_service/editor/spawnweaver_dock.gd")

var _dock: Control


func _enter_tree() -> void:
	add_autoload_singleton(AUTOLOAD_NAME, AUTOLOAD_PATH)
	_dock = DockScript.new()
	add_control_to_dock(EditorPlugin.DOCK_SLOT_RIGHT_UL, _dock)


func _exit_tree() -> void:
	if _dock != null:
		remove_control_from_docks(_dock)
		_dock.free()
		_dock = null
	remove_autoload_singleton(AUTOLOAD_NAME)
