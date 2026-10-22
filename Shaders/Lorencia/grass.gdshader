shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_opaque;

uniform sampler2D grass_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform float wind_time = 0.0;
uniform float wind_speed = 1.0;
uniform float wind_strength = 1.0;
uniform float alpha_scissor = 0.35;

varying vec2 grass_uv;
varying float blade_top;

void vertex()
{
    float top = 1.0 - UV.y;
    blade_top = top;

    float phase = INSTANCE_CUSTOM.b * 6.2831853;
    float amp = mix(1.2, 4.0, INSTANCE_CUSTOM.a) * wind_strength;

    float bend = top * top;
    float t = wind_time * wind_speed + phase;
    float sx = sin(t);
    float sz = cos(t * 0.77 + phase * 1.3);

    VERTEX.x += sx * amp * bend;
    VERTEX.z += sz * amp * 0.45 * bend;

    float u0 = INSTANCE_CUSTOM.r;
    float uw = INSTANCE_CUSTOM.g;
    grass_uv = vec2(mix(u0, u0 + uw, UV.x), clamp(UV.y, 0.02, 0.98));
}

void fragment()
{
    vec4 tex = texture(grass_texture, grass_uv);
    float a = tex.a * COLOR.a;
    if (a < alpha_scissor)
        discard;

    float root_ao = mix(0.60, 1.05, blade_top);
    ALBEDO = tex.rgb * COLOR.rgb * root_ao;
}