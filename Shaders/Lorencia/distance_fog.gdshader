shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled;

uniform vec4 fog_color : source_color = vec4(0.61, 0.67, 0.53, 1.0); // Χρώμα ομίχλης Lorencia
uniform float fog_start : hint_range(0.0, 1000.0) = 44.0;
uniform float fog_end : hint_range(10.0, 5000.0) = 2000.0;
uniform float fog_density : hint_range(0.0, 2.0) = 0.88;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_nearest;

void vertex() {
    POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
    // 1. Ανάκτηση βάθους από το Depth Buffer
    float depth = texture(DEPTH_TEXTURE, SCREEN_UV).r;
    vec4 ndc = vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    vec4 view = INV_PROJECTION_MATRIX * ndc;
    view.xyz /= view.w;
    
    // 2. Απόσταση από την κάμερα (Z depth)
    float distance_from_camera = -view.z;
    
    // 3. Υπολογισμός παράγοντα ομίχλης (Exponential / Linear mix)
    float fog_factor = clamp((distance_from_camera - fog_start) / (fog_end - fog_start), 0.0, 1.0);
    fog_factor = 1.0 - exp(-fog_factor * fog_density * 3.0);
    fog_factor = clamp(fog_factor, 0.0, 1.0);
    
    // 4. Μίξη του χρώματος της οθόνης με το χρώμα της ομίχλης
    vec4 screen_color = texture(SCREEN_TEXTURE, SCREEN_UV);
    ALBEDO = mix(screen_color.rgb, fog_color.rgb, fog_factor);
}