using Assimp;
using BulletSharp.Math;
using OpenTK.Mathematics;
using RE.Fmod;
using Quaternion = BulletSharp.Math.Quaternion;
using Vector3 = BulletSharp.Math.Vector3;
using Vector4 = BulletSharp.Math.Vector4;

namespace RE.Utils
{
    internal static class MathExtensions
    {
        #region Bullet
        extension(Vector3 v)
        {
            public OpenTK.Mathematics.Vector3 ToOpenTkVector3() => new(v.X, v.Y, v.Z);
        }

        extension(Vector4 v)
        {
            public OpenTK.Mathematics.Vector4 ToOpenTkVector4() => new(v.X, v.Y, v.Z, v.W);
        }

        extension(Quaternion q)
        {
            public OpenTK.Mathematics.Quaternion ToOpenTkQuaternion() => new(q.X, q.Y, q.Z, q.W);
        }

        extension(Matrix m)
        {
            public Matrix4 ToOpenTk() =>
                new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42,
                    m.M43, m.M44);
        }
        #endregion

        #region OpenTK 
        extension(OpenTK.Mathematics.Vector3 v)
        {
            public Vector3 ToBulletVector3() => new(v.X, v.Y, v.Z);
            public System.Numerics.Vector3 ToSystemVector3() => new(v.X, v.Y, v.Z);
            public VECTOR ToFmodVector3() => new() { x = v.X, y = v.Y, z = v.Z };
        }

        extension(Vector2 v)
        {
            public System.Numerics.Vector2 ToSystemVector2() => new(v.X, v.Y);
        }

        extension(OpenTK.Mathematics.Vector4 v)
        {
            public Vector4 ToBulletVector4() => new(v.X, v.Y, v.Z, v.W);
        }

        extension(OpenTK.Mathematics.Quaternion q)
        {
            public Quaternion ToBulletQuaternion() => new(q.X, q.Y, q.Z, q.W);
            public System.Numerics.Quaternion ToSystemQuaternion() => new(q.X, q.Y, q.Z, q.W);
        }

        extension(Matrix4 v)
        {
            public Matrix ToBulletMatrix() =>
                new(v.M11, v.M12, v.M13, v.M14, v.M21, v.M22, v.M23, v.M24, v.M31, v.M32, v.M33, v.M34, v.M41, v.M42, v.M43, v.M44);
        }
        #endregion

        #region System.Numerics

        extension(System.Numerics.Vector3 v)
        {
            public OpenTK.Mathematics.Vector3 ToOpenTkVector3() => new(v.X, v.Y, v.Z);
        }

        extension(System.Numerics.Vector2 v)
        {
            public Vector2 ToOpenTkVector2() => new(v.X, v.Y);
        }

        extension(System.Numerics.Quaternion q)
        {
            public OpenTK.Mathematics.Quaternion ToOpenTkQuaternion() => new(q.X, q.Y, q.Z, q.W);
        }
        #endregion

        extension(VECTOR v)
        {
            public OpenTK.Mathematics.Vector3 ToOpenTkVector3() => new(v.x, v.y, v.z);
            public System.Numerics.Vector3 ToSystemVector3() => new(v.x, v.y, v.z);
        }

        extension(Vector3D v)
        {
            public System.Numerics.Vector3 ToSystemVector3() => new(v.X, v.Y, v.Z);
            public OpenTK.Mathematics.Vector3 ToOpenTkVector3() => new(v.X, v.Y, v.Z);
        }
    }
}
