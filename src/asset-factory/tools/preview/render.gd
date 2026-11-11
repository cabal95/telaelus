extends SceneTree
# Usage: godot --path test-output/asset-lab --resolution 512x512 -s render.gd -- <model.obj> <out.png> [bgcolor]
# Renders a contact sheet: game camera angle (4 yaws), plus top view. Orthographic, game-like light.

const CELL := 400

func _parse_mtl(path: String) -> Dictionary:
	var mats := {}
	if not FileAccess.file_exists(path):
		return mats
	var cur := ""
	for line in FileAccess.get_file_as_string(path).split("\n"):
		var p := line.strip_edges().split(" ", false)
		if p.size() == 0:
			continue
		if p[0] == "newmtl":
			cur = p[1]
			mats[cur] = {"Kd": Color(0.8, 0.8, 0.8), "Ke": null, "d": 1.0}
		elif p[0] == "Kd" and p.size() >= 4:
			mats[cur]["Kd"] = Color(float(p[1]), float(p[2]), float(p[3]))
		elif p[0] == "Ke" and p.size() >= 4:
			var e := Color(float(p[1]), float(p[2]), float(p[3]))
			if e.r + e.g + e.b > 0.0:
				mats[cur]["Ke"] = e
		elif p[0] == "d" and p.size() >= 2:
			mats[cur]["d"] = float(p[1])
	return mats

func _load_obj(path: String) -> Node3D:
	var root := Node3D.new()
	var mats := _parse_mtl(path.get_basename() + ".mtl")
	var verts: Array[Vector3] = []
	var per_mat := {}  # name -> PackedVector3Array of triangle verts
	var cur := "default"
	for line in FileAccess.get_file_as_string(path).split("\n"):
		var p := line.strip_edges().split(" ", false)
		if p.size() == 0:
			continue
		match p[0]:
			"v":
				verts.append(Vector3(float(p[1]), float(p[2]), float(p[3])))
			"usemtl":
				cur = p[1]
			"f":
				var idx: Array[int] = []
				for i in range(1, p.size()):
					var n := int(p[i].split("/")[0])
					idx.append(n - 1 if n > 0 else verts.size() + n)
				if not per_mat.has(cur):
					per_mat[cur] = PackedVector3Array()
				for i in range(1, idx.size() - 1):
					# OBJ is CCW-front; Godot is CW-front, so swap the last two
					per_mat[cur].append_array([verts[idx[0]], verts[idx[i + 1]], verts[idx[i]]])
	var mesh := ArrayMesh.new()
	for name in per_mat:
		var tris: PackedVector3Array = per_mat[name]
		# flat shading: per-face normals, computed from winding (CCW = front)
		var normals := PackedVector3Array()
		for i in range(0, tris.size(), 3):
			var n := (tris[i + 2] - tris[i]).cross(tris[i + 1] - tris[i]).normalized()
			normals.append_array([n, n, n])
		var arr := []
		arr.resize(Mesh.ARRAY_MAX)
		arr[Mesh.ARRAY_VERTEX] = tris
		arr[Mesh.ARRAY_NORMAL] = normals
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
		var m := StandardMaterial3D.new()
		var def: Dictionary = mats.get(name, {"Kd": Color(0.8, 0.8, 0.8), "Ke": null, "d": 1.0})
		m.albedo_color = def["Kd"]
		m.roughness = 0.9
		if def["Ke"] != null:
			m.emission_enabled = true
			m.emission = def["Ke"]
		if def["d"] < 1.0:
			m.albedo_color.a = def["d"]
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		mesh.surface_set_material(mesh.get_surface_count() - 1, m)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	root.add_child(mi)
	return root

func _load_glb(path: String) -> Node3D:
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var err := doc.append_from_file(path, state)
	if err != OK:
		push_error("glb load failed: %d" % err)
	return doc.generate_scene(state) as Node3D

func _merged_aabb(node: Node) -> AABB:
	var out := AABB()
	var first := true
	var stack: Array = [node]
	while stack.size() > 0:
		var n: Node = stack.pop_back()
		if n is MeshInstance3D:
			var a: AABB = (n as MeshInstance3D).global_transform * (n as MeshInstance3D).mesh.get_aabb() if n.is_inside_tree() else (n as MeshInstance3D).mesh.get_aabb()
			out = a if first else out.merge(a)
			first = false
		stack.append_array(n.get_children())
	return out

func _load_model(path: String) -> Node3D:
	return _load_glb(path) if path.ends_with(".glb") else _load_obj(path)

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var model_path := args[0]
	var out_path := args[1]
	var bg := Color(0.33, 0.47, 0.30)  # roughly the game's grass
	var model := _load_model(model_path)
	var aabb: AABB = _merged_aabb(model)
	print("AABB pos=", aabb.position, " size=", aabb.size)
	var views := [
		{"name": "game", "yaw": 45.0, "pitch": -48.0},
		{"name": "yaw135", "yaw": 135.0, "pitch": -48.0},
		{"name": "yaw225", "yaw": 225.0, "pitch": -48.0},
		{"name": "top", "yaw": 45.0, "pitch": -89.0},
	]
	var sheet := Image.create(CELL * 2, CELL * 2, false, Image.FORMAT_RGBA8)
	var i := 0
	for v in views:
		var vp := SubViewport.new()
		vp.size = Vector2i(CELL, CELL)
		vp.own_world_3d = true
		vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(vp)
		var env := WorldEnvironment.new()
		env.environment = Environment.new()
		env.environment.background_mode = Environment.BG_COLOR
		env.environment.background_color = bg
		env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		env.environment.ambient_light_color = Color(0.6, 0.6, 0.65)
		env.environment.ambient_light_energy = 0.5
		vp.add_child(env)
		# ground plane so shadows read
		var ground := MeshInstance3D.new()
		var pm := PlaneMesh.new()
		pm.size = Vector2(40, 40)
		ground.mesh = pm
		ground.position.y = -0.002
		var gm := StandardMaterial3D.new()
		gm.albedo_color = bg
		gm.roughness = 1.0
		ground.material_override = gm
		vp.add_child(ground)
		var light := DirectionalLight3D.new()
		light.shadow_enabled = true
		light.rotation_degrees = Vector3(-50, -40, 0)
		vp.add_child(light)
		var inst := _load_model(model_path)
		vp.add_child(inst)
		var cam := Camera3D.new()
		cam.projection = Camera3D.PROJECTION_ORTHOGONAL
		var extent := maxf(maxf(aabb.size.x, aabb.size.z), aabb.size.y) * 1.8 + 0.5
		if args.size() > 2:
			extent = float(args[2])
		cam.size = extent
		var center := aabb.get_center()
		var basis := Basis.from_euler(Vector3(deg_to_rad(v["pitch"]), deg_to_rad(v["yaw"]), 0))
		vp.add_child(cam)
		cam.global_transform = Transform3D(basis, center + basis * Vector3(0, 0, 30))
		await process_frame
		await process_frame
		await process_frame
		var img := vp.get_texture().get_image()
		img.convert(Image.FORMAT_RGBA8)
		sheet.blit_rect(img, Rect2i(0, 0, CELL, CELL), Vector2i((i % 2) * CELL, (i / 2) * CELL))
		i += 1
	sheet.save_png(out_path)
	print("saved ", out_path)
	quit()
