#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aUV;
layout(location = 2) in vec3 aNormal;

uniform int outline;
uniform vec4 outlineColor;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec2 TexCoords;
out vec3 FragPos;
out vec3 Normal;
out double b_outline;
out vec4 v_outlineColor;

void main()
{
    TexCoords = aUV;
    b_outline = outline;
    v_outlineColor = outlineColor;

    FragPos = vec3(model * vec4(aPos, 1.0));
    Normal  = mat3(transpose(inverse(model))) * aNormal;

    gl_Position = projection * view * model * vec4(aPos, 1.0);
}
