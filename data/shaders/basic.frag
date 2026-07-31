#version 330 core
in vec4 vColor;
out vec4 FragColor;

uniform vec4 uTint;   // per-draw multiplier; set to white for untinted geometry
uniform float uSolid; // 1 = ignore vertex color and emit uTint flat (outline hulls)

void main()
{
    FragColor = mix(vColor * uTint, uTint, uSolid);
}
