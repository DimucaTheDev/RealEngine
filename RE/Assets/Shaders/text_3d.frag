#version 460
in vec2 TexCoord;
uniform sampler2D texture0;
uniform vec4 uColor;
out vec4 FragColor;

void main()
{
    vec4 texColor = texture(texture0, TexCoord);
    float alpha = texture(texture0, TexCoord).r;
        if (alpha < 0.01)
        discard;
    FragColor = vec4(uColor.rgb, uColor.a * alpha);
}