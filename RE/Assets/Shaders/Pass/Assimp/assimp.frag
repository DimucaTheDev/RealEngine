#version 460 core

#include "/Assets/Shaders/Engine/lighting.glsl"
#include "/Assets/Shaders/Engine/oit_shared.glsl"

uniform bool ignoreLight;
uniform int outline;
uniform vec4 outlineColor;

uniform bool hasDirLight;
uniform bool hasSpotLight;

//in vec3 FragPos;        // These are from lighting.glsl
//in vec3 Normal;
//in vec2 TexCoords;

 
void main()
{
    vec4 tex = texture(material.diffuse, TexCoords);

    float alpha = tex.a;

    if(alpha < 0.01)
        discard;

    if(outline == 1)
    {
        vec3 color = outlineColor.rgb;
        float weight = max(alpha, 0.001);

        oit_AccumColor = vec4(color * alpha * weight, alpha * weight);
        oit_AccumWeight = alpha * weight;

        return;
    }

    vec3 color;

    if(ignoreLight)
    {
        color = tex.rgb;
    }
    else
    {
        vec3 norm = normalize(Normal);
        vec3 viewDir = normalize(u_CameraPosition - FragPos);

        vec3 result = vec3(0.0);

        if(hasDirLight)
            result += CalcDirLight(dirLight, norm, viewDir);    

        if(hasSpotLight)
            result += CalcSpotLight(spotLight, norm, FragPos, viewDir);

        color = result * tex.rgb;
    }

    float weight = max(alpha, 0.001);

    oit_AccumColor = vec4(color * alpha * weight, alpha * weight);
    oit_AccumWeight = alpha * weight;
}