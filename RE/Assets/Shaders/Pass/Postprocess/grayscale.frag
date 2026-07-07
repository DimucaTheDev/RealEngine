#version 460 core

layout(location = 0) in vec2 v_TexCoord;
layout(location = 0) out vec4 f_Color;

uniform sampler2D u_Texture;

void main()
{
    vec4 color = texture(u_Texture, v_TexCoord).rgba;
    float w = (color.r + color.g + color.b) / 3;
    f_Color = vec4(w, w, w, color.a);
    return;
}