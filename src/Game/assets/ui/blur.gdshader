// The main menu's backdrop: the live town behind it, blurred and darkened a little.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 2.5;
uniform vec4 tint : source_color = vec4(0.05, 0.07, 0.06, 0.275);

void fragment() {
	vec3 behind = textureLod(screen_tex, SCREEN_UV, blur_lod).rgb;
	COLOR = vec4(mix(behind, tint.rgb, tint.a), 1.0);
}
