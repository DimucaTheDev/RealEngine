#version 330 core

layout (location = 0) in vec3 aPos;
out vec3 vPos;

uniform mat4 view;
uniform mat4 projection;

void main()
{
    vPos = aPos; 
    vec4 pos = projection * view * vec4(aPos * 100.0, 1.0); 
    gl_Position = pos.xyww;
}