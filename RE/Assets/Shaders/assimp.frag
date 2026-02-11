#version 330 core
  
uniform vec3 viewPos;
uniform bool ignoreLight;
uniform int outline;
uniform vec4 outlineColor;

uniform bool hasDirLight;
uniform bool hasSpotLight;

out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
   
#include "lighting.glsl"

void main()
{ 
    if(outline == 1){
        FragColor = outlineColor;
        return;
    }
    if(ignoreLight){
        FragColor = vec4(vec3(texture(material.diffuse, TexCoords)), 1);
        return;
    }
    //properties
    vec3 norm = normalize(Normal);
    vec3 viewDir = normalize(viewPos - FragPos);

    //phase 1: Directional lighting
    vec3 result = vec3(0,0,0);
    
    if(hasDirLight)
        result += CalcDirLight(dirLight, norm, viewDir);
    //phase 2: Point lights
    //for(int i = 0; i < NR_POINT_LIGHTS; i++)
     //   result += CalcPointLight(pointLights[i], norm, FragPos, viewDir);
    //phase 3: Spot light
    
    if(hasSpotLight)
        result += CalcSpotLight(spotLight, norm, FragPos, viewDir);    

    FragColor = vec4(result, 0.2); 
}  