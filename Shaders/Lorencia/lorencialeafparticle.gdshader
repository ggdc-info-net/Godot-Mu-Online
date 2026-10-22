shader_type spatial;
render_mode cull_disabled, blend_mix, depth_prepass_alpha, unshaded;

uniform sampler2D leaf_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform float alpha_cutoff = 0.16;

void fragment()
{
    vec4 tex = texture(leaf_texture, UV);
    if (tex.a < alpha_cutoff)
        discard;

    ALBEDO = tex.rgb * COLOR.rgb;
    ALPHA = tex.a * COLOR.a;
}