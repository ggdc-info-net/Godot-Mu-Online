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

                        // Εφαρμογή Bind Pose
                        if (vert.Node >= 0 && vert.Node < boneMatrices.Length)
                        {
                            pos = SysVector3.Transform(pos, boneMatrices[vert.Node]);
                        }

                        // ΠΡΟΣΟΧΗ: ΚΑΜΙΑ ΑΛΛΑΓΗ ΑΞΟΝΩΝ. Κρατάμε τα μαθηματικά του MU άθικτα!
                        Vector3 gdPos = new Vector3(pos.X * SCALE_FACTOR, pos.Y * SCALE_FACTOR, pos.Z * SCALE_FACTOR);

                        // SKINNING: Λέμε στο Godot σε ποιο οστό ανήκει αυτό το Vertex (100% βάρος)
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

                    // Φόρτωση Texture
                    if (!string.IsNullOrEmpty(mesh.TexturePath))
                    {
                        StandardMaterial3D material = new StandardMaterial3D();
                        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

                        // ΔΙΟΡΘΩΣΗ: Ενώνουμε τον φάκελο με το αρχείο (π.χ. "Monster" + "/" + "helmat.jpg")
                        string texPath = string.IsNullOrEmpty(textureFolder) ? mesh.TexturePath : Path.Combine(textureFolder, mesh.TexturePath).Replace("\\", "/");
                        
                        Texture2D tex = await TextureLoader.Instance.PrepareAndGetTexture(texPath);
                        if (tex != null)
                        {
                            material.AlbedoTexture = tex;
                            var script = TextureLoader.Instance.GetScript(mesh.TexturePath);
                            if (script != null)
                            {
                                if (script.Alpha) material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                                if (script.Bright) material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
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