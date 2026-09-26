shader_type spatial;
render_mode unshaded, depth_draw_never, cull_back;

uniform sampler2D _MainTex;
uniform float _DistortionFrequency = 1.0;
uniform float _DistortionAmplitude = 0.02;
uniform float _WaterSpeed = 0.1;

varying vec2 v_uv;
varying vec4 v_color;

void vertex() {
    v_uv = UV;
    v_color = COLOR;

    float time_offset = TIME * _WaterSpeed;
    float wave = sin((UV.x + time_offset) * _DistortionFrequency) * _DistortionAmplitude;

    v_uv.x += wave;
    v_uv.y += wave;
}

void fragment() {
    vec4 tex_color = texture(_MainTex, v_uv);

    // Multiply texture by vertex color (Unity behavior)
    tex_color *= v_color;

    // Keep same alpha logic as Unity shader
    tex_color.a *= tex_color.a;

    ALBEDO = tex_color.rgb;
    ALPHA = tex_color.a;
}
