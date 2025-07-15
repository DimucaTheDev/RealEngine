#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aUV;
uniform int outline;
uniform vec4 outlineColor;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
out vec2 TexCoord;
out double b_outline;
out vec4 v_outlineColor;

void main()
{
    TexCoord = aUV;
    b_outline = outline;
    v_outlineColor = outlineColor;
    gl_Position = projection * view * model * vec4(aPos, 1.0);
}