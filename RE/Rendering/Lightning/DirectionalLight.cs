using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using RE.Utils;

namespace RE.Rendering.Lightning
{
    [GlStructName("DirLight")]
    public struct DirectionalLight
    {
        [GlPropertyName("direction")]
        public Vector3 Direction { get; set; }

        [GlPropertyName("ambient")]
        public Vector3 AmbientColor { get; set; }

        [GlPropertyName("diffuse")]
        public Vector3 DiffuseColor { get; set; }

        [GlPropertyName("specular")]
        public Vector3 SpecularColor { get; set; }
    }
}
