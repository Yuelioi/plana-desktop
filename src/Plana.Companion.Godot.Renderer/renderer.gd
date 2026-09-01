extends SpineSprite

var controller := StreamPeerTCP.new()
var controller_buffer := ""
var dragging := false
var drag_offset := Vector2i.ZERO
var press_position := Vector2.ZERO
var single_click_deadline := 0
var suppress_release_click := false
var full_window_pass_through := false
var idle_animation := "Idle_01"
var hidden_slots: Array[String] = []
var hit_polygon_normalized := [
	Vector2(0.20, 0.34), Vector2(0.62, 0.34), Vector2(0.70, 0.60),
	Vector2(0.75, 0.78), Vector2(1.00, 0.82), Vector2(1.00, 0.90),
	Vector2(0.70, 0.90), Vector2(0.65, 1.00), Vector2(0.00, 1.00),
	Vector2(0.00, 0.80), Vector2(0.20, 0.76), Vector2(0.15, 0.55)
]

func _ready():
	var app_icon := Image.load_from_file("res://AppIcon.png")
	if not app_icon.is_empty():
		DisplayServer.set_icon(app_icon)
	load_character_from_arguments()
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_IGNORE
	get_window().size_changed.connect(apply_mouse_passthrough_polygon)
	before_world_transforms_change.connect(apply_hidden_slots)
	apply_mouse_passthrough_polygon()
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("controller_port="):
			controller.connect_to_host("127.0.0.1", int(argument.trim_prefix("controller_port=")))
	get_animation_state().set_animation(idle_animation, true, 0)
	print("RENDERER_READY")

func apply_mouse_passthrough_polygon():
	if full_window_pass_through:
		get_window().mouse_passthrough_polygon = PackedVector2Array()
		return
	var size = Vector2(get_window().size)
	var polygon = PackedVector2Array()
	for point in hit_polygon_normalized:
		polygon.append(point * size)
	get_window().mouse_passthrough_polygon = polygon

func load_character_from_arguments():
	var values := {}
	for argument in OS.get_cmdline_user_args():
		var separator = argument.find("=")
		if separator > 0:
			values[argument.substr(0, separator)] = argument.substr(separator + 1)
	if not values.has("character_skeleton") or not values.has("character_atlas"):
		return
	var atlas = SpineAtlasResource.new()
	if atlas.load_from_atlas_file(values["character_atlas"]) != OK:
		push_error("CHARACTER_LOAD_ERROR atlas")
		return
	var skeleton_file = SpineSkeletonFileResource.new()
	if skeleton_file.load_from_file(values["character_skeleton"]) != OK:
		push_error("CHARACTER_LOAD_ERROR skeleton")
		return
	var skeleton_data = SpineSkeletonDataResource.new()
	skeleton_data.atlas_res = atlas
	skeleton_data.skeleton_file_res = skeleton_file
	skeleton_data.default_mix = 0.15
	skeleton_data.update_skeleton_data()
	if not skeleton_data.is_skeleton_data_loaded():
		push_error("CHARACTER_LOAD_ERROR data")
		return
	skeleton_data_res = skeleton_data
	position = Vector2(float(values.get("character_x", "320")), float(values.get("character_y", "835")))
	var character_scale = float(values.get("character_scale", "0.36"))
	scale = Vector2(character_scale, character_scale)
	idle_animation = values.get("character_idle", "Idle_01")
	if values.has("character_hidden_slots"):
		var decoded_slots = Marshalls.base64_to_utf8(values["character_hidden_slots"])
		var parsed_slots = JSON.parse_string(decoded_slots)
		if parsed_slots is Array:
			for slot in parsed_slots:
				hidden_slots.append(str(slot))
	if values.has("character_hit_polygon"):
		var decoded = Marshalls.base64_to_utf8(values["character_hit_polygon"])
		var points = JSON.parse_string(decoded)
		if points is Array and points.size() >= 3:
			hit_polygon_normalized.clear()
			for point in points:
				if point is Dictionary:
					hit_polygon_normalized.append(Vector2(float(point.get("x", 0)), float(point.get("y", 0))))

func apply_hidden_slots():
	if hidden_slots.is_empty() or get_skeleton() == null:
		return
	for slot_name in hidden_slots:
		get_skeleton().set_attachment(slot_name, "")

func _process(_delta):
	poll_controller()
	if single_click_deadline > 0 and Time.get_ticks_msec() >= single_click_deadline:
		single_click_deadline = 0
		send_event("interaction", {"interaction": "click"})

func _input(event):
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				if event.double_click:
					single_click_deadline = 0
					dragging = false
					suppress_release_click = true
					send_event("interaction", {"interaction": "double-click"})
					return
				dragging = true
				press_position = event.position
				drag_offset = DisplayServer.mouse_get_position() - DisplayServer.window_get_position()
			elif dragging:
				dragging = false
				if event.position.distance_to(press_position) < 6 and not suppress_release_click:
					single_click_deadline = Time.get_ticks_msec() + 250
				suppress_release_click = false
		elif event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
			send_event("context", {})
	if event is InputEventMouseMotion and dragging:
		DisplayServer.window_set_position(DisplayServer.mouse_get_position() - drag_offset)

func poll_controller():
	controller.poll()
	if controller.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	var available = controller.get_available_bytes()
	if available <= 0:
		return
	controller_buffer += controller.get_utf8_string(available)
	while controller_buffer.contains("\n"):
		var end = controller_buffer.find("\n")
		var command = JSON.parse_string(controller_buffer.substr(0, end))
		controller_buffer = controller_buffer.substr(end + 1)
		if command is Dictionary and command.get("type") == "perform":
			perform_cues(command.get("cues", []))
		if command is Dictionary and command.get("type") == "set_input_mode":
			full_window_pass_through = command.get("passThrough", false)
			apply_mouse_passthrough_polygon()
			send_event("input_mode", {"passThrough": full_window_pass_through})

func perform_cues(cues: Array):
	if cues.is_empty():
		return
	var state = get_animation_state()
	for index in cues.size():
		var cue = cues[index]
		if index == 0:
			state.set_animation(cue.get("Animation", "Idle_01"), cue.get("Loop", false), 0)
		else:
			state.add_animation(cue.get("Animation", "Idle_01"), 0, cue.get("Loop", false), 0)
	send_event("performed", {"cueCount": cues.size()})

func send_event(type: String, data: Dictionary):
	if controller.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	data["type"] = type
	controller.put_data((JSON.stringify(data) + "\n").to_utf8_buffer())
