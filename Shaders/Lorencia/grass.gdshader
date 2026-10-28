shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_opaque;

uniform sampler2D grass_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform float wind_time = 0.0;
uniform float wind_speed = 1.0;
uniform float wind_strength = 1.0;
uniform float alpha_scissor = 0.30;

varying vec2 grass_uv;
varying float blade_top;

void vertex() {
    float top = 1.0 - UV.y;
    
    // 👈 Κάνουμε την καμπύλη πιο "εκρηκτική" στην κορυφή (δίνει πιο φυσικό λύγισμα)
    float bend_curve = top * top; 
    blade_top = top;

    // Φυσικός άνεμος στην κορυφή (αυξημένη ένταση)
    float phase = INSTANCE_CUSTOM.b * 6.2831853;
    float amp = mix(0.8, 2.5, INSTANCE_CUSTOM.a) * wind_strength * 1.6; // 👈 Αυξήσαμε το εύρος
    float t = wind_time * wind_speed * 1.2 + phase;                   // 👈 Αυξήσαμε λίγο την ταχύτητα κυματισμού
    
    // Προσθέτουμε και μια δεύτερη αρμονική (sine wave) για πιο "ατίθαλη" κίνηση
    float secondary_wave = sin(t * 2.1) * 0.3;

    VERTEX.x += (sin(t) + secondary_wave) * amp * bend_curve;
    VERTEX.z += (cos(t * 0.77) + secondary_wave) * amp * 0.5 * bend_curve;

    float u0 = INSTANCE_CUSTOM.r;
    float uw = INSTANCE_CUSTOM.g;
    grass_uv = vec2(mix(u0, u0 + uw, UV.x), clamp(UV.y, 0.02, 0.98));
}

void fragment() {
    vec4 tex = texture(grass_texture, grass_uv);
    float a = tex.a * COLOR.a;
    if (a < alpha_scissor)
        discard;

    // Σκίαση στη ρίζα για να δένει τέλεια με το χιόνι/χώμα
    float root_ao = mix(0.50, 1.05, blade_top);
    ALBEDO = tex.rgb * COLOR.rgb * root_ao;
}