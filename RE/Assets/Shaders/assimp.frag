#version 460 core
out vec4 FragColor;
in vec2 TexCoord;
flat in double b_outline;
in vec4 v_outlineColor;
uniform sampler2D tex;
void main()
{
    if(b_outline != 0) 
    {
        FragColor = v_outlineColor;
        return;
    }
    FragColor = texture(tex, TexCoord);
    if(FragColor.w==0)discard;
}