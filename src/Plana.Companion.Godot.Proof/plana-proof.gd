extends SpineSprite

var click_through := false
var capture_path := ""
var capture_frames := 0
var startup_animation := "Idle_01"
var auto_exit_frames := 0
var elapsed_frames := 0

func _ready():
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("capture="):
			capture_path = argument.trim_prefix("capture=")
		if argument.begins_with("animation="):
			startup_animation = argument.trim_prefix("animation=")
		if argument == "click_through=true":
			click_through = true
		if argument.begins_with("auto_exit_frames="):
			auto_exit_frames = int(argument.trim_prefix("auto_exit_frames="))
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_MOUSE_PASSTHROUGH, click_through)
	get_animation_state().set_animation(startup_animation, startup_animation == "Idle_01", 0)
	print("PROOF_READY animation=", startup_animation, " click_through=", click_through)

func _process(_delta):
	elapsed_frames += 1
	if auto_exit_frames > 0 and elapsed_frames >= auto_exit_frames:
		print("PROOF_AUTO_EXIT frames=", elapsed_frames)
		get_tree().quit()
		return
	if not capture_path.is_empty():
		capture_frames += 1
		if capture_frames == 30:
			var error = get_viewport().get_texture().get_image().save_png(capture_path)
			print("PROOF_CAPTURE path=", capture_path, " error=", error)
			get_tree().quit()
			return
	if Input.is_key_pressed(KEY_ESCAPE):
		get_tree().quit()
	if Input.is_action_just_pressed("ui_text_submit"):
		play_head_pat()

func _unhandled_key_input(event):
	if not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_1:
			get_animation_state().set_animation("Idle_01", true, 0)
			print("PROOF_ANIMATION Idle_01")
		KEY_2:
			play_head_pat()
		KEY_3:
			var state = get_animation_state()
			state.set_animation("17", false, 0)
			state.add_animation("Idle_01", 0, true, 0)
			print("PROOF_ANIMATION 17 -> Idle_01")
		KEY_T:
			click_through = not click_through
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_MOUSE_PASSTHROUGH, click_through)
			print("PROOF_CLICK_THROUGH ", click_through)

func play_head_pat():
	var state = get_animation_state()
	state.set_animation("S_Pat_01_M_all", false, 0)
	state.add_animation("Idle_01", 0, true, 0)
	print("PROOF_ANIMATION S_Pat_01_M_all -> Idle_01")
