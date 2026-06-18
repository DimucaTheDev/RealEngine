#version 330 core
#include "/Assets/Shaders/Engine/material.glsl"
#include "/Assets/Shaders/Engine/oit_shared.glsl"

uniform vec3 viewPos;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
in vec3 WorldPos;

void main()
{
    vec3 n = normalize(Normal);
    vec2 uv;

    if (abs(n.x) > abs(n.y) && abs(n.x) > abs(n.z))
    {
        if (n.x > 0.0)
        uv = vec2(-WorldPos.z, WorldPos.y);
        else
        uv = vec2(WorldPos.z, WorldPos.y);
    }
    else if (abs(n.y) > abs(n.x) && abs(n.y) > abs(n.z))
    {
        if (n.y > 0.0)
        uv = vec2(WorldPos.x, -WorldPos.z);
        else
        uv = vec2(WorldPos.x, WorldPos.z);
    }
    else
    {
        if (n.z > 0.0)
        uv = vec2(WorldPos.x, WorldPos.y);
        else
        uv = vec2(-WorldPos.x, WorldPos.y);
    }

    float tiling = 0.85f;
    uv *= tiling * vec2(1, -1);
    
    vec4 tex = texture(material.diffuse, uv);
    float alpha = tex.a;

    if (alpha < 0.01)
    discard;

    vec3 color = tex.rgb;
    float weight = max(alpha, 0.001);

    oit_AccumColor = vec4(color * alpha * weight, alpha * weight);
    oit_AccumWeight = alpha * weight;
}