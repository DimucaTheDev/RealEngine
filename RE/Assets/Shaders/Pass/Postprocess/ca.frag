#version 460 core

layout(location = 0) in vec2 v_TexCoord;
layout(location = 0) out vec4 f_Color;

uniform sampler2D u_Texture;

uniform float u_AberrationAmount = 0.005;

void main()
{
    vec2 distortionDirection = v_TexCoord - vec2(0.5);

    float dist = length(distortionDirection);

    vec2 offset = distortionDirection * dist * u_AberrationAmount;

    float r = texture(u_Texture, v_TexCoord - offset).r;
    float g = texture(u_Texture, v_TexCoord).g;
    float b = texture(u_Texture, v_TexCoord + offset).b;
    float a = texture(u_Texture, v_TexCoord).a;

    f_Color = vec4(r, g, b, a);
}