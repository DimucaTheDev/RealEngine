#version 330 core

in vec3 vPos;
out vec4 FragColor;

void main()
{
    vec3 dir = normalize(vPos);
    float t = (dir.y + 1.0) / 2.0;
    vec3 bottomColor = vec3(0.2, 0.3, 0.7);
    vec3 topColor = vec3(0.8, 0.9, 1.0);
    vec3 finalColor = mix(bottomColor, topColor, t);

    FragColor = vec4(finalColor, 1.0);
}