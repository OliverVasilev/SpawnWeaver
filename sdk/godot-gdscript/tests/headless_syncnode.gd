extends Node
## Headless smoke test for the SpawnSync drop-in node (no backend needed).
## Verifies it instantiates on Node2D + Node3D, doesn't crash without a connection, and that a
## remote node interpolates toward an incoming entity-state update.

func _ready() -> void:
	# Remote 2D node (interpolates incoming state).
	var n2 := Node2D.new()
	var s2 := SpawnSync.new()
	s2.entity_id = "t2"
	s2.local = false
	s2.interpolation_speed = 20.0
	n2.add_child(s2)
	add_child(n2)

	# Local 3D node (defers registration since we're not connected — must not crash).
	var n3 := Node3D.new()
	var s3 := SpawnSync.new()
	s3.entity_id = "t3"
	s3.local = true
	n3.add_child(s3)
	add_child(n3)

	print("SPAWNSYNC_TEST: instantiated on Node2D + Node3D")

	await get_tree().process_frame
	# Simulate a remote update; the 2D node should ease toward (100, 50).
	MultiplayerService.entity_state_changed.emit("t2", {}, {"x": 100.0, "y": 50.0, "rot": 1.0})
	for _i in range(20):
		await get_tree().process_frame

	var moved := n2.position.x > 1.0 and n2.position.x <= 100.5
	print("SPAWNSYNC_TEST: 2D remote interpolated to ", n2.position, " (moved=", moved, ")")
	print("SPAWNSYNC_TEST: ", ("OK" if moved else "FAIL"))
	get_tree().quit(0 if moved else 1)
