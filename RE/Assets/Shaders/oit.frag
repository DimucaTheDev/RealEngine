#version 460

in vec2 TexCoord;

uniform sampler2D accumColorTex;
uniform sampler2D accumWeightTex;

out vec4 FragColor;

void main()
{
    vec4 accum = texture(accumColorTex, TexCoord);
    float weight = texture(accumWeightTex, TexCoord).r;

    vec3 color = accum.rgb / max(weight, 0.00001);

    FragColor = vec4(color, accum.a);
}