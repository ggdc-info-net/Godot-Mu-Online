shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_back;

uniform sampler2D _MainTex;

void fragment()
{
    vec4 tex = texture(_MainTex, UV);

    // Vertex light από OZB
    vec3 light = COLOR.rgb;

    ALBEDO = tex.rgb * light;
    ALPHA = 1.0;
}
