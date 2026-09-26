using Godot;
using System;

namespace Client.Main
{
    public static class MathUtils
    {
        /// <summary>
        /// Euler angles are assumed to be in DEGREES (MU / Unity style)
        /// </summary>
        public static Quaternion AngleQuaternion(Vector3 eulerDegrees)
        {
            // Godot expects radians
            return new Quaternion(
                Vector3.Right,  Mathf.DegToRad(eulerDegrees.X)) *
                   new Quaternion(
                Vector3.Up,     Mathf.DegToRad(eulerDegrees.Y)) *
                   new Quaternion(
                Vector3.Back,   Mathf.DegToRad(eulerDegrees.Z));
        }

        /// <summary>
        /// Builds a rotation matrix from Euler angles (degrees)
        /// Equivalent to MonoGame / Unity logic
        /// </summary>
        public static Basis AngleMatrix(Vector3 anglesDegrees)
        {
            Quaternion q = AngleQuaternion(anglesDegrees);
            return new Basis(q);
        }

        public static float DotProduct(Vector3 x, Vector3 y)
        {
            return x.Dot(y);
        }

        /// <summary>
        /// Computes normalized face normal from 3 vertices
        /// </summary>
        public static Vector3 FaceNormalize(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;

            Vector3 normal = edge1.Cross(edge2);
            if (normal.LengthSquared() == 0)
                return Vector3.Zero;

            return normal.Normalized();
        }

        /// <summary>
        /// Rotate vector by matrix (ignores translation)
        /// </summary>
        public static Vector3 VectorRotate(Vector3 v, Basis m)
        {
            return m * v;
        }

        /// <summary>
        /// Inverse rotate (transpose of rotation matrix)
        /// </summary>
        public static Vector3 VectorIRotate(Vector3 v, Basis m)
        {
            return m.Transposed() * v;
        }
    }
}
