using Godot;
using Client.Data.BMD;

namespace Client.Main.Utils
{
    public static class BMDRigBuilder
    {
        private const float SCALE_FACTOR = 0.01f;

        public static Node3D CreateAnimatedCharacter(BMD bmd, ArrayMesh skinnedMesh)
        {
            Node3D root = new Node3D();
            root.Name = string.IsNullOrEmpty(bmd.Name) ? "MuCharacter" : bmd.Name;
            
            // Το MU Online είναι Z-up. Γυρνάμε τον χαρακτήρα όρθιο
            root.RotationDegrees = new Vector3(-90, 0, 0);

            Skeleton3D skeleton = new Skeleton3D();
            skeleton.Name = "Skeleton3D";
            root.AddChild(skeleton);

            MeshInstance3D meshInstance = new MeshInstance3D();
            meshInstance.Name = "Mesh";
            meshInstance.Mesh = skinnedMesh;
            skeleton.AddChild(meshInstance);
            meshInstance.Skeleton = new NodePath("..");

            // Πίνακας για να θυμόμαστε τα "μοναδικά" ονόματα που θα δώσουμε στο Godot
            string[] godotBoneNames = new string[bmd.Bones.Length];

            // 1. Στήσιμο του Σκελετού
            for (int i = 0; i < bmd.Bones.Length; i++)
            {
                var bone = bmd.Bones[i];
                
                // Δημιουργία Μοναδικού Ονόματος (Γιατί το Godot αγνοεί τα διπλότυπα!)
                string baseName = string.IsNullOrWhiteSpace(bone.Name) ? $"Bone_{i}" : bone.Name;
                if (baseName == "Dummy") baseName = $"Dummy_{i}";

                string uniqueName = baseName;
                int suffix = 1;
                while (skeleton.FindBone(uniqueName) != -1) // Όσο το όνομα υπάρχει ήδη
                {
                    uniqueName = $"{baseName}_{suffix++}";
                }

                // Αποθηκεύουμε το μοναδικό όνομα και το προσθέτουμε στο Godot
                godotBoneNames[i] = uniqueName;
                skeleton.AddBone(uniqueName);

                // Συνδέουμε με τον γονέα
                if (bone.Parent != -1 && bone.Parent < i) 
                {
                    skeleton.SetBoneParent(i, bone.Parent);
                }

                // Στήνουμε τη βασική πόζα
                if (bone.Matrixes != null && bone.Matrixes.Length > 0 && bone.Matrixes[0].Position.Length > 0)
                {
                    var pos = bone.Matrixes[0].Position[0];
                    var rot = bone.Matrixes[0].Quaternion[0];
                    
                    Vector3 gdPos = new Vector3(pos.X * SCALE_FACTOR, pos.Y * SCALE_FACTOR, pos.Z * SCALE_FACTOR);
                    Godot.Quaternion gdRot = new Godot.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
                    
                    skeleton.SetBoneRest(i, new Transform3D(new Basis(gdRot), gdPos));
                }
            }

            // 2. Animations
            AnimationPlayer animPlayer = new AnimationPlayer();
            animPlayer.Name = "AnimationPlayer";
            root.AddChild(animPlayer);
            AnimationLibrary animLib = new AnimationLibrary();

            for (int a = 0; a < bmd.Actions.Length; a++)
            {
                var action = bmd.Actions[a];
                if (action.NumAnimationKeys <= 1) continue;

                Animation anim = new Animation();
                float fps = action.PlaySpeed > 0 ? 6f * action.PlaySpeed : 12f;
                anim.Step = 1f / fps;
                anim.Length = (action.NumAnimationKeys - 1) * anim.Step;
                anim.LoopMode = Animation.LoopModeEnum.Linear;

                for (int b = 0; b < bmd.Bones.Length; b++)
                {
                    var bone = bmd.Bones[b];
                    if (bone.Matrixes == null || a >= bone.Matrixes.Length || action.NumAnimationKeys > bone.Matrixes[a].Position.Length) continue;

                    // Χρησιμοποιούμε το μοναδικό όνομα που φτιάξαμε παραπάνω!
                    string trackPath = $"Skeleton3D:{godotBoneNames[b]}";
                    
                    int posTrack = anim.AddTrack(Animation.TrackType.Position3D);
                    anim.TrackSetPath(posTrack, trackPath);
                    int rotTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
                    anim.TrackSetPath(rotTrack, trackPath);

                    for (int k = 0; k < action.NumAnimationKeys; k++)
                    {
                        float time = k * anim.Step;
                        var pos = bone.Matrixes[a].Position[k];
                        var rot = bone.Matrixes[a].Quaternion[k];

                        Vector3 gdPos = new Vector3(pos.X * SCALE_FACTOR, pos.Y * SCALE_FACTOR, pos.Z * SCALE_FACTOR);
                        Godot.Quaternion gdRot = new Godot.Quaternion(rot.X, rot.Y, rot.Z, rot.W);

                        anim.PositionTrackInsertKey(posTrack, time, gdPos);
                        anim.RotationTrackInsertKey(rotTrack, time, gdRot);
                    }
                }
                animLib.AddAnimation($"Action_{a}", anim);
            }
            animPlayer.AddAnimationLibrary("", animLib);

            return root;
        }
    }
}