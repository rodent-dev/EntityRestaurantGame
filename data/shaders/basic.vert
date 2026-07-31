#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aColor;

uniform mat4 uMvp;
uniform mat4 uModel;
uniform float uLit;     // 0 = unlit (grid, axes), 1 = gouraud shaded
uniform float uOutline; // world-units pushed along the normal; >0 = inverted-hull outline pass

out vec4 vColor;

// Per-vertex (gouraud) lighting — the PS2 way. One fixed directional light.
const vec3 LIGHT_DIR = normalize(vec3(0.5, 1.0, 0.35));

void main()
{
    vec3 n = normalize(mat3(uModel) * aNormal);
    float diffuse = max(dot(n, LIGHT_DIR), 0.0);
    float shade = mix(1.0, 0.35 + 0.65 * diffuse, uLit);
    vColor = vec4(aColor.rgb * shade, aColor.a);
    gl_Position = uMvp * vec4(aPos + aNormal * uOutline, 1.0);
}
