shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, unshaded;

uniform sampler2D _MainTex : source_color, repeat_enable, filter_linear_mipmap;
uniform vec2 _ScrollSpeed = vec2(0.1, 0.1);
uniform float _WaterSpeed = 1.0; // Αυξάνετέ το λίγο αν το θες πιο γρήγορο (π.χ. 1.0 αντί για 0.1)
uniform float _DistortionFrequency = 5.0;
uniform float _DistortionAmplitude = 0.02;

uniform vec3 _WaterTint : source_color = vec3(0.84, 0.97, 1.08);

varying vec2 v_uv;
varying vec4 v_color;

void vertex() {
    v_uv = UV;
    v_color = COLOR;

    // Αυτόματη κίνηση μέσω του TIME της Godot
    float wave = sin((UV.x + TIME * _WaterSpeed) * _DistortionFrequency) * _DistortionAmplitude;

    v_uv.x += wave;
    v_uv.y += wave;
}

void fragment() {
    vec4 tex_color = texture(_MainTex, fract(v_uv));
    vec3 base = tex_color.rgb * v_color.rgb;
    vec3 shaded = base * _WaterTint;

    ALBEDO = shaded;
    ALPHA = tex_color.a * v_color.a;
}