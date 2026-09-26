shader_type spatial;
render_mode unshaded, blend_mix, cull_back;

uniform sampler2D _MainTex;

void fragment()
{
    vec4 tex = texture(_MainTex, UV);

    vec3 light = COLOR.rgb;
    float alpha = COLOR.a;

    ALBEDO = tex.rgb * light;
    ALPHA = tex.a * alpha;

    if (ALPHA < 0.01)
        discard;
}
