#version 460 core

layout(location = 0) in vec2 v_TexCoord;
layout(location = 0) out vec4 f_Color;

uniform sampler2D u_Texture; 

float hash(vec2 p)
{
    p = fract(p * vec2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return fract(p.x * p.y);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    vec2 u = f * f * (3.0 - 2.0 * f);

    return mix(a, b, u.x) +
    (c - a) * u.y * (1.0 - u.x) +
    (d - b) * u.x * u.y;
}

void main()
{
    vec2 uv = v_TexCoord;

    vec3 col = texture(u_Texture, uv).rgb;

    float lum = dot(col, vec3(0.299, 0.587, 0.114));

    float posterize = 6.0;
    col = floor(col * posterize) / posterize;

    col = mix(col, vec3(lum), 0.15);

    float edge =
    abs(dFdx(lum)) +
    abs(dFdy(lum));

    col -= edge * 1.8;

    col = pow(col, vec3(0.85));

    vec3 lightDir = normalize(vec3(0.3, 0.6, 1.0));
    float lighting = dot(vec3(0.0, 0.0, 1.0), lightDir) * 0.5 + 0.5;

    col *= lighting * 1.2;

    float n = noise(gl_FragCoord.xy * 0.5 + u_Time * 10.0);
    col += (n - 0.5) * 0.04;

    float vignette = smoothstep(1.3, 0.2, length(v_TexCoord * 2.0 - 1.0));
    col *= vignette;

    col = clamp(col, 0.0, 1.0);

    f_Color = vec4(col, 1.0);
}