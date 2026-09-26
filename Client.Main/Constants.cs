using Godot;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Client.Main
{
    public static class Constants
    {
        // Network
        public static string IPAddress = "127.0.0.1";
        public static int Port = 44405;

        // Terrain
        public const int TERRAIN_SIZE = 256;
        public const int TERRAIN_SIZE_MASK = 255;
        public const float TERRAIN_SCALE = 100f;

        //public static string DataPath => ProjectSettings.GlobalizePath("F://Program Files//MuCloneData//Data//");
        public static string DataPath = "F:/Program Files/MuCloneData/Data/"; 
    }
}