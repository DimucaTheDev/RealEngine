#version 460 core

layout(location = 0) in vec2 v_TexCoord;
layout(location = 0) out vec4 f_Color;

uniform sampler2D u_Texture; 
 
float noise(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

float scaryOnOff(float a, float b, float c, float t) {
    float t_complex = t + a * cos(t * b + noise(vec2(t, 0.1)));
    return step(c + 0.1 * noise(vec2(t_complex, 0.2)), sin(t_complex));
}

float scaryTear(vec2 uv, float time) {
    float tearSpeed = time * 0.8;
    float tearY = fract(tearSpeed);

    float tearEffect = 1.0 - abs(uv.y - tearY);
    tearEffect = smoothstep(0.97, 0.99, tearEffect);

    return tearEffect * scaryOnOff(4.1, 10.0, 0.6, time);
}

vec2 scaryDistortion(vec2 uv, float time) {
    uv.x += (noise(vec2(time, uv.y)) - 0.5) * 0.008;

    float window = 1.0 / (1.0 + 30.0 * (uv.y - mod(time / 3.0, 1.0)) * (uv.y - mod(time / 3.0, 1.0)));
    uv.x += sin(uv.y * 15.0 + time * 2.0) * 0.015 * scaryOnOff(2.5, 5.0, 0.35, time) * (1.0 + window * 6.0);

    uv.x += scaryTear(uv, time) * 0.07;

    float glitchPulse = scaryOnOff(1.1, 15.0, 0.7, time);
    uv += (noise(vec2(time * 10.0, 0.5)) - 0.5) * 0.01 * glitchPulse;

    return uv;
}

void main() {
    vec2 uv = v_TexCoord;

    vec2 crtUV = uv * 2.0 - 1.0;
    crtUV.x *= 1.0 + pow(abs(crtUV.y) / 3.5, 2.0);
    crtUV.y *= 1.0 + pow(abs(crtUV.x) / 2.5, 2.0);
    //uv = crtUV * 0.5 + 0.5;

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
        f_Color = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    vec2 distortedUV = scaryDistortion(uv, u_Time);

    float shift = 0.009 * scaryOnOff(3.5, 8.0, 0.45, u_Time);
    vec2 shiftVec = vec2(shift, (noise(vec2(u_Time, 0.3)) - 0.5) * 0.002);

    vec4 color;
    color.r = texture(u_Texture, distortedUV + shiftVec).r;
    color.g = texture(u_Texture, distortedUV).g;
    color.b = texture(u_Texture, distortedUV - shiftVec).b;
    color.a = 1.0;

    vec2 pixelatedUV = floor(distortedUV * vec2(u_ClientSize)) / vec2(u_ClientSize);

    float grain = noise(pixelatedUV * (u_Time * 1.5)) * 0.12;
    color.rgb += vec3(grain);

    float dirtyNoise = scaryOnOff(5.0, 20.0, 0.7, u_Time);
    if (dirtyNoise > 0.0) {
        color.rgb += vec3(noise(vec2(u_Time, pixelatedUV.y)) * 0.05);
    }

    vec3 lumaCoeff = vec3(0.299, 0.587, 0.114);
    float gray = dot(color.rgb, lumaCoeff);
    vec3 eerieColor = vec3(0.8, 0.85, 0.7);
    color.rgb = mix(color.rgb, vec3(gray) * eerieColor, 0.45);

    float stripe = step(0.985, sin(uv.y * 30.0 + u_Time * 10.0)) * scaryOnOff(1.5, 4.0, 0.85, u_Time);
    color.rgb += vec3(stripe * 0.2);

    float strongGlitch = scaryOnOff(1.0, 18.0, 0.8, u_Time);
    if (strongGlitch > 0.0) {
        //color.r = texture(uTexture, distortedUV * uTime).r + noise(vec2(uTime));
        //color.gb *= 0.5;
    }

    float flicker = 1.0 + sin(u_Time * 5.0) * 0.03 + (noise(vec2(u_Time)) - 0.5) * 0.04;
    color.rgb *= flicker;

    f_Color = color;
}