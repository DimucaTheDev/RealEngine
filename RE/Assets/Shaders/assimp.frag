#version 330 core
  
uniform vec3 viewPos;

out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec2 TexCoords;
   
#include "lighting.glsl"

void main()
{ 
    //properties
    vec3 norm = normalize(Normal);
    vec3 viewDir = normalize(viewPos - FragPos);

    //phase 1: Directional lighting
    vec3 result = CalcDirLight(dirLight, norm, viewDir);
    //phase 2: Point lights
    //for(int i = 0; i < NR_POINT_LIGHTS; i++)
     //   result += CalcPointLight(pointLights[i], norm, FragPos, viewDir);
    //phase 3: Spot light
    result += CalcSpotLight(spotLight, norm, FragPos, viewDir);    

    FragColor = vec4(result, 1.0);
}  