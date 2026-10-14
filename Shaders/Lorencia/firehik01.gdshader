shader_type spatial;
render_mode unshaded, blend_add, cull_disabled;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;
uniform vec4 albedo_color : source_color = vec4(1.5, 1.5, 1.5, 1.0);

uniform float time_speed = 14.0;
uniform float wave_amplitude = 0.02;

// Μεταβλητή που περνάει από τη vertex() στη fragment()
varying float time_offset;

void vertex() {
    // Παίρνουμε τις συντεταγμένες (X, Z) της φωτιάς στον κόσμο (World Space)
    // ώστε κάθε φωτιά στον χάρτη να έχει διαφορετικό συγχρονισμό!
    vec3 world_pos = MODEL_MATRIX[3].xyz;
    time_offset = (world_pos.x + world_pos.z) * 2.5;

    // Billboard Keep Scale (σβήσε τις παρακάτω γραμμές αν δεν θέλεις να κοιτάει την κάμερα)
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
        vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
        vec4(0.0, 0.0, 0.0, 1.0)
    );
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
    vec2 uv = UV;
    
    float mask = 1.0 - uv.y;
    
    // Προσθέτουμε το time_offset στον TIME!
    float wave = sin(uv.y * 10.0 - (TIME * time_speed + time_offset)) * wave_amplitude * mask;
    
    uv.x += wave;
    
    vec4 tex_color = texture(albedo_tex, uv);
    
    ALBEDO = tex_color.rgb * albedo_color.rgb;
    ALPHA = tex_color.a * albedo_color.a;
}