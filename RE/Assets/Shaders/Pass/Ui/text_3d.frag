#version 460

in vec2 TexCoord;

uniform sampler2D texture0;
uniform vec4 uColor;

layout(location = 0) out vec4 accumColor;
layout(location = 1) out float accumWeight;

void main()
{
    float alpha = texture(texture0, TexCoord).r;

    if (alpha < 0.01)
        discard;

    float a = uColor.a * alpha;
    vec3 color = uColor.rgb;

    float weight = max(a, 0.001);

    accumColor = vec4(color * a * weight, a);
    accumWeight = a * weight;
}