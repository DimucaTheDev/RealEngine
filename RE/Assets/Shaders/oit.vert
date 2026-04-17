#version 460

out vec2 TexCoord;

vec2 verts[3] = vec2[]
(
    vec2(-1,-1),
    vec2( 3,-1),
    vec2(-1, 3)
);

void main()
{
    gl_Position = vec4(verts[gl_VertexID],0,1);
    TexCoord = (gl_Position.xy * 0.5 + 0.5);
}