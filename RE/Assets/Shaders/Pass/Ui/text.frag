#version 460

in vec2 vUV;
layout (binding=0) uniform sampler2D u_texture;
layout (location = 2) uniform vec3 textColor;
layout (location = 3) uniform vec4 textColor4;
out vec4 fragColor;

void main()
{
	vec2 uv = vUV.xy;
	float text = texture(u_texture, uv).r;
	if(dot(textColor4,textColor4) == 0)
		fragColor = vec4(textColor.rgb*text + textColor4.rgb, text);
	else
	fragColor = vec4(textColor4.rgba) * text;
}