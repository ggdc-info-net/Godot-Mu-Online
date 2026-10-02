using System;
using System.IO;
using Godot;


    public static class Utils
    {
        public static string GetActualPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
                //kl

            // Convert res:// or user:// to OS path
            string realPath = ProjectSettings.GlobalizePath(path);

            if (File.Exists(realPath))
                return realPath;

            string directory = Path.GetDirectoryName(realPath);
            string fileName = Path.GetFileName(realPath);

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (string.Equals(
                        Path.GetFileName(file),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }

            return realPath;
        }
    }

