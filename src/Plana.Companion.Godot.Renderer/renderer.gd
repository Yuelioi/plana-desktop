extends SpineSprite

@onready var bubble = $"../Ui/Bubble"
@onready var bubble_text = $"../Ui/Bubble/Text"
@onready var bubble_tail = $"../Ui/BubbleTail"

var controller := StreamPeerTCP.new()
var controller_buffer := ""
var dragging := false
var drag_offset := Vector2i.ZERO
var press_position := Vector2.ZERO
var single_click_deadline := 0
var suppress_release_click := false
var full_window_pass_through := false
var bubble_deadline := 0

func _ready():
	get_window().content_scale_aspect = Window.CONTENT_SCALE_ASPECT_IGNORE
	get_window().size_changed.connect(apply_mouse_passthrough_polygon)
	apply_mouse_passthrough_polygon()
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("controller_port="):
			controller.connect_to_host("127.0.0.1", int(argument.trim_prefix("controller_port=")))
	get_animation_state().set_animation("Idle_01", true, 0)
	print("RENDERER_READY")

func apply_mouse_passthrough_polygon():
	if full_window_pass_through:
		get_window().mouse_passthrough_polygon = PackedVector2Array()
		return
	var size = Vector2(get_window().size)
	var character_shape = [
		Vector2(0.20, 0.34), Vector2(0.62, 0.34), Vector2(0.70, 0.60),
		Vector2(0.75, 0.78), Vector2(1.00, 0.82), Vector2(1.00, 0.90),
		Vector2(0.70, 0.90), Vector2(0.65, 1.00), Vector2(0.00, 1.00),
		Vector2(0.00, 0.80), Vector2(0.20, 0.76), Vector2(0.15, 0.55)
	]
	var normalized = character_shape
	if bubble.visible:
		normalized = [
			Vector2(0.03, 0.02), Vector2(0.97, 0.02), Vector2(0.97, 0.17),
			Vector2(0.56, 0.17), Vector2(0.56, 0.34)
		] + character_shape + [
			Vector2(0.44, 0.34), Vector2(0.44, 0.17), Vector2(0.03, 0.17)
		]
	var polygon = PackedVector2Array()
	for point in normalized:
		polygon.append(point * size)
	get_window().mouse_passthrough_polygon = polygon

func _process(_delta):
	poll_controller()
	if bubble_deadline > 0 and Time.get_ticks_msec() >= bubble_deadline:
		bubble_deadline = 0
		bubble.visible = false
		bubble_tail.visible = false
		apply_mouse_passthrough_polygon()
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
		if command is Dictionary and command.get("type") == "show_bubble":
			show_bubble(command.get("text", ""), command.get("isError", false))

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

func show_bubble(text: String, is_error: bool):
	bubble_text.text = text
	bubble.get_theme_stylebox("panel").border_color = Color(0.82, 0.25, 0.31, 0.75) if is_error else Color(0.31, 0.43, 0.78, 0.55)
	bubble.visible = not text.is_empty()
	bubble_tail.visible = bubble.visible
	apply_mouse_passthrough_polygon()
	bubble_deadline = Time.get_ticks_msec() + (14000 if is_error else 22000)
	send_event("bubble_shown", {"visible": bubble.visible})
