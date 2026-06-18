#version 460 core

layout(location = 0) in vec2 v_TexCoord;
layout(location = 0) out vec4 f_Color;

uniform sampler2D u_Texture;

uniform float u_Distortion = 0.1;   
uniform float u_ScanlineCount = 720.0; 
uniform float u_ScanlineIntensity = 0.15; 

 vec2 curve(vec2 uv)
{
    uv = uv * 2.0 - 1.0;  

     uv.x *= 1.0 + pow(abs(uv.y) * u_Distortion, 2.0);
    uv.y *= 1.0 + pow(abs(uv.x) * u_Distortion, 2.0);

    uv = uv * 0.5 + 0.5;  
    return uv;
}

void main()
{ 
    vec2 crtCoord = curve(v_TexCoord);
 
    if (crtCoord.x < 0.0 || crtCoord.x > 1.0 || crtCoord.y < 0.0 || crtCoord.y > 1.0)
    {
        f_Color = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }
 
    vec4 baseColor = texture(u_Texture, crtCoord);
 
    float scanline = sin(crtCoord.y * u_ScanlineCount * 3.14159265);

     scanline = mix(1.0, 1.0 - u_ScanlineIntensity, step(0.0, scanline));

     baseColor.rgb *= scanline;

     float vignette = crtCoord.x * crtCoord.y * (1.0 - crtCoord.x) * (1.0 - crtCoord.y);
    vignette = clamp(pow(16.0 * vignette, 0.25), 0.0, 1.0);
    baseColor.rgb *= vignette;

    f_Color = baseColor;
}