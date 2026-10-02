using Godot;
using System;
using System.IO;
using Client.Data.BMD;
using Client.Main;
using Client.Main.Utils;

public partial class test : Node3D
{
    public override async void _Ready()
    {
        BMDReader reader = new BMDReader();
        string modelFolder = "Monster";
        
        string actualPath = GetActualFilePath(modelFolder, "Monster01.bmd");
        if (actualPath == null) 
        {
            GD.PrintErr("Το αρχείο δεν βρέθηκε!");
            return;
        }

        BMD bmdModel = await reader.Load(actualPath);

        // Τώρα το "Monster" περνάει σωστά και τα textures θα βρεθούν!
        ArrayMesh mesh = await Client.Main.Utils.BMDMeshConverter.CreateMeshAsync(bmdModel, modelFolder);
        
        Node3D characterRoot = Client.Main.Utils.BMDRigBuilder.CreateAnimatedCharacter(bmdModel, mesh);
        AddChild(characterRoot);

        AnimationPlayer animPlayer = characterRoot.GetNode<AnimationPlayer>("AnimationPlayer");
        if (animPlayer.HasAnimation("Action_1")) animPlayer.Play("Action_1");
        else if (animPlayer.HasAnimation("Action_0")) animPlayer.Play("Action_0");
    }

    private string GetActualFilePath(string folder, string fileName)
    {
        string fullDir = Path.Combine(Constants.DataPath, folder);
        if (!Directory.Exists(fullDir)) return null;

        foreach (string file in Directory.GetFiles(fullDir))
        {
            if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                return file; 
        }
        return null;
    }
}