using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using Client.Data.BMD;
using Client.Main.Content;
using SysVector3 = System.Numerics.Vector3;
using SysMatrix = System.Numerics.Matrix4x4;

namespace Client.Main.Utils
{
    public static class BMDMeshConverter
    {
        private const float SCALE_FACTOR = 0.01f;

        public static async Task<ArrayMesh> CreateMeshAsync(BMD bmd, string textureFolder)
        {
            ArrayMesh arrayMesh = new ArrayMesh();
            SysMatrix[] boneMatrices = ComputeBindPoseMatrices(bmd);

            for (int i = 0; i < bmd.Meshes.Length; i++)
            {
                var mesh = bmd.Meshes[i];
                if (mesh.Triangles == null || mesh.Triangles.Length == 0) continue;

                SurfaceTool st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);

                foreach (var tri in mesh.Triangles)
                {
                    int vertexCount = Math.Min((int)tri.Polygon, 4);
                    if (vertexCount < 3) continue;

                    int[] indices = vertexCount == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };

                    foreach (int idx in indices)
                    {
                        if (idx >= vertexCount) break;

                        var vertIdx = tri.VertexIndex[idx];
                        var tcIdx = tri.TexCoordIndex[idx];

                        if (vertIdx < 0 || vertIdx >= mesh.Vertices.Length) continue;

                        var vert = mesh.Vertices[vertIdx];
                        var pos = vert.Position;

                        if (vert.Node >= 0 && vert.Node < boneMatrices.Length)
                        {
                            pos = SysVector3.Transform(pos, boneMatrices[vert.Node]);
                        }

                        Vector3 gdPos = new Vector3(pos.X * SCALE_FACTOR, pos.Y * SCALE_FACTOR, pos.Z * SCALE_FACTOR);

                        st.SetBones(new int[] { vert.Node, 0, 0, 0 });
                        st.SetWeights(new float[] { 1.0f, 0f, 0f, 0f });

                        if (tcIdx >= 0 && tcIdx < mesh.TexCoords.Length)
                        {
                            st.SetUV(new Vector2(mesh.TexCoords[tcIdx].U, mesh.TexCoords[tcIdx].V));
                        }

                        st.AddVertex(gdPos);
                    }
                }

                st.Index();
                st.GenerateNormals();
                var surfaceMesh = st.Commit();

                if (surfaceMesh != null)
                {
                    arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceMesh.SurfaceGetArrays(0));

                    if (!string.IsNullOrEmpty(mesh.TexturePath))
                    {
                        StandardMaterial3D material = new StandardMaterial3D();
                        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;

                        material.AlbedoColor = new Color(0.65f, 0.65f, 0.65f, 1.0f);

                        string texPath = string.IsNullOrEmpty(textureFolder) ? mesh.TexturePath : Path.Combine(textureFolder, mesh.TexturePath).Replace("\\", "/");
                        
                        Texture2D tex = await TextureLoader.Instance.PrepareAndGetTexture(texPath);
                        if (tex != null)
                        {
                            material.AlbedoTexture = tex;

                            var script = TextureLoader.Instance.GetScript(texPath);
                            var textureData = TextureLoader.Instance.Get(texPath);

                            // 1. Κρυφά meshes (_h)
                            if (script != null && script.HiddenMesh)
                            {
                                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                                material.AlbedoColor = new Color(1f, 1f, 1f, 0f);
                            }
                            // 2. Σκιές (_dc, _dt)
                            else if (script != null && script.ShadowMesh > 0)
                            {
                                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                                material.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
                                material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
                                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                            }
                            // 3. Φώτα / Λάμψεις (_r)
                            else if (script != null && script.Bright)
                            {
                                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                                material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
                                material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
                                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                            }
                            // 4. Διαφάνειες (_a ή 4 κανάλια RGBA)
                            else if ((script != null && script.Alpha) || (textureData != null && textureData.Components == 4))
                            {
                                if (script != null && script.Alpha)
                                {
                                    material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                                }
                                else
                                {
                                    material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                                    material.AlphaScissorThreshold = 0.5f;
                                }
                            }
                        }
                        arrayMesh.SurfaceSetMaterial(i, material);
                    }
                }
            }
            return arrayMesh;
        }

        private static SysMatrix[] ComputeBindPoseMatrices(BMD bmd)
        {
            if (bmd.Bones == null || bmd.Bones.Length == 0) return Array.Empty<SysMatrix>();
            var matrices = new SysMatrix[bmd.Bones.Length];
            for (int i = 0; i < bmd.Bones.Length; i++)
            {
                var bone = bmd.Bones[i];
                SysMatrix localMatrix = SysMatrix.Identity;
                if (bone.Matrixes != null && bone.Matrixes.Length > 0 && bone.Matrixes[0].Position.Length > 0)
                {
                    localMatrix = SysMatrix.CreateFromQuaternion(bone.Matrixes[0].Quaternion[0]) * SysMatrix.CreateTranslation(bone.Matrixes[0].Position[0]);
                }
                matrices[i] = (bone.Parent >= 0 && bone.Parent < i) ? localMatrix * matrices[bone.Parent] : localMatrix;
            }
            return matrices;
        }
    }
}