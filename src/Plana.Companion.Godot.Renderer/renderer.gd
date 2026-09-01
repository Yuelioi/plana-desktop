extends SpineSprite

var controller := StreamPeerTCP.new()
var controller_buffer := ""
var dragging := false
var drag_offset := Vector2i.ZERO
var press_position := Vector2.ZERO

func _ready():
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("controller_port="):
			controller.connect_to_host("127.0.0.1", int(argument.trim_prefix("controller_port=")))
	get_animation_state().set_animation("Idle_01", true, 0)
	print("RENDERER_READY")

func _process(_delta):
	poll_controller()

func _input(event):
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				dragging = true
				press_position = event.position
				drag_offset = DisplayServer.mouse_get_position() - DisplayServer.window_get_position()
			elif dragging:
				dragging = false
				if event.position.distance_to(press_position) < 6:
					send_event("interaction", {"interaction": "double-click" if event.double_click else "click"})
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
