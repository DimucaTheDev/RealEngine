#version 460 core

out vec4 FragColor;
in vec2 v_TexCoord;

uniform usampler2D u_StencilTexture;
uniform vec3 u_OutlineColor = vec3(1.0, 0.5, 0.0);
uniform int u_OutlineWidth = 3;

void main()
{
    uint mask = texture(u_StencilTexture, v_TexCoord).r;

    if (mask > 0u) {
        discard;
    }

    vec2 texel = 1.0 / u_ClientSize;
    bool isBorder = false;

    for (int x = -u_OutlineWidth; x <= u_OutlineWidth; x++)
    {
        for (int y = -u_OutlineWidth; y <= u_OutlineWidth; y++)
        {
            if (x == 0 && y == 0) continue;

            if (float(x*x + y*y) > float(u_OutlineWidth * u_OutlineWidth)) continue;

            vec2 offset = vec2(float(x), float(y)) * texel;
            uint neighborMask = texture(u_StencilTexture, v_TexCoord + offset).r;

            if (neighborMask > 0u)
            {
                isBorder = true;
                break;
            }
        }
        if (isBorder) break;
    }

    if (isBorder) {
        FragColor = vec4(u_OutlineColor, 1);
    } else {
        discard;
    }
}