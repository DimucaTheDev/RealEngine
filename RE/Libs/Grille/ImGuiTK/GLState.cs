using OpenTK.Graphics.OpenGL4;

namespace RE.Libs.Grille.ImGuiTK;

public unsafe class GLState
{
    private readonly bool CompatibilityProfile;

    private readonly int GLVersion;
    private readonly int[] prevPolygonMode;
    private readonly int[] prevScissorBox;
    private TextureUnit prevActiveTextureUnit;
    private int prevArrayBuffer;
    private bool prevBlendEnabled;
    private BlendEquationMode prevBlendEquationAlpha;
    private BlendEquationMode prevBlendEquationRgb;
    private BlendingFactorDest prevBlendFuncDstAlpha;
    private BlendingFactorDest prevBlendFuncDstRgb;
    private BlendingFactorSrc prevBlendFuncSrcAlpha;
    private BlendingFactorSrc prevBlendFuncSrcRgb;
    private bool prevCullFaceEnabled;
    private bool prevDepthTestEnabled;
    private int prevProgram;
    private bool prevScissorTestEnabled;
    private int prevTexture02D;
    private int prevVAO;


    public GLState()
    {
        prevScissorBox = new int[4];
        prevPolygonMode = new int[2];

        StateBackupEnabled = true;

        var major = GL.GetInteger(GetPName.MajorVersion);
        var minor = GL.GetInteger(GetPName.MinorVersion);

        GLVersion = major * 100 + minor * 10;
        CompatibilityProfile =
            (GL.GetInteger((GetPName)All.ContextProfileMask) & (int)All.ContextCompatibilityProfileBit) != 0;
    }

    public bool StateBackupEnabled { get; set; }

    public void Backup()
    {
        if (!StateBackupEnabled) return;

        prevVAO = GL.GetInteger(GetPName.VertexArrayBinding);
        prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);
        prevProgram = GL.GetInteger(GetPName.CurrentProgram);
        prevBlendEnabled = GL.GetBoolean(GetPName.Blend);
        prevScissorTestEnabled = GL.GetBoolean(GetPName.ScissorTest);
        prevBlendEquationRgb = (BlendEquationMode)GL.GetInteger(GetPName.BlendEquationRgb);
        prevBlendEquationAlpha = (BlendEquationMode)GL.GetInteger(GetPName.BlendEquationAlpha);
        prevBlendFuncSrcRgb = (BlendingFactorSrc)GL.GetInteger(GetPName.BlendSrcRgb);
        prevBlendFuncSrcAlpha = (BlendingFactorSrc)GL.GetInteger(GetPName.BlendSrcAlpha);
        prevBlendFuncDstRgb = (BlendingFactorDest)GL.GetInteger(GetPName.BlendDstRgb);
        prevBlendFuncDstAlpha = (BlendingFactorDest)GL.GetInteger(GetPName.BlendDstAlpha);
        prevCullFaceEnabled = GL.GetBoolean(GetPName.CullFace);
        prevDepthTestEnabled = GL.GetBoolean(GetPName.DepthTest);
        prevActiveTextureUnit = (TextureUnit)GL.GetInteger(GetPName.ActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        prevTexture02D = GL.GetInteger(GetPName.TextureBinding2D);
        fixed (int* iptr = prevScissorBox)
        {
            GL.GetInteger(GetPName.ScissorBox, iptr);
        }

        fixed (int* iptr = prevPolygonMode)
        {
            GL.GetInteger(GetPName.PolygonMode, iptr);
        }
    }

    public void Setup()
    {
        Backup();

        if (GLVersion <= 310 || CompatibilityProfile)
        {
            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.PolygonMode(MaterialFace.Back, PolygonMode.Fill);
        }
        else
        {
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
    }

    public void Restore()
    {
        if (!StateBackupEnabled) return;

        // Reset state
        GL.BindTexture(TextureTarget.Texture2D, prevTexture02D);
        GL.ActiveTexture(prevActiveTextureUnit);
        GL.UseProgram(prevProgram);
        GL.BindVertexArray(prevVAO);
        GL.Scissor(prevScissorBox[0], prevScissorBox[1], prevScissorBox[2], prevScissorBox[3]);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);
        GL.BlendEquationSeparate(prevBlendEquationRgb, prevBlendEquationAlpha);
        GL.BlendFuncSeparate(prevBlendFuncSrcRgb, prevBlendFuncDstRgb, prevBlendFuncSrcAlpha, prevBlendFuncDstAlpha);

        void Set(EnableCap cap, bool value)
        {
            if (value) GL.Enable(cap);
            else GL.Disable(cap);
        }

        Set(EnableCap.Blend, prevBlendEnabled);
        Set(EnableCap.DepthTest, prevDepthTestEnabled);
        Set(EnableCap.CullFace, prevCullFaceEnabled);
        Set(EnableCap.ScissorTest, prevScissorTestEnabled);

        if (GLVersion <= 310 || CompatibilityProfile)
        {
            GL.PolygonMode(MaterialFace.Front, (PolygonMode)prevPolygonMode[0]);
            GL.PolygonMode(MaterialFace.Back, (PolygonMode)prevPolygonMode[1]);
        }
        else
        {
            GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)prevPolygonMode[0]);
        }
    }
}