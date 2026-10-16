shader_type spatial;
render_mode unshaded, blend_add, cull_disabled;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;
uniform vec4 albedo_color : source_color = vec4(1.5, 1.0, 0.4, 1.0); // Ζεστό κίτρινο/πορτοκαλί χρώμα

uniform float flicker_speed = 12.0;    // Πόσο γρήγορα τρεμοπαίζει
uniform float flicker_intensity = 0.3; // Η ένταση του τρεμοπαίγματος

void vertex() {
    // Billboard εφέ για να κοιτάζει πάντα την κάμερα
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
    vec2 uv = UV;
    vec4 tex_color = texture(albedo_tex, uv);

    // Υπολογισμός τρεμοπαίγματος με βάση τον χρόνο (TIME)
    float flicker = 1.0 + (sin(TIME * flicker_speed) * 0.6 + sin(TIME * flicker_speed * 2.7) * 0.4) * flicker_intensity;

    ALBEDO = tex_color.rgb * albedo_color.rgb * flicker;
    ALPHA = tex_color.a * albedo_color.a;
}