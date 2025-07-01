#version 330 core
in vec2 vUV;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    vec4 texColor = texture(uTexture, vUV);
    if (texColor.a < 0.01)
        discard;
    FragColor = texColor;
}