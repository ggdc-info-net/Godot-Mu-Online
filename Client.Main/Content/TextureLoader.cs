using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Data;
using Client.Data.Texture;
using Godot;

namespace Client.Main.Content
{
    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        private readonly ConcurrentDictionary<string, Task<TextureData>> _textureTasks = new();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new();

        private readonly Dictionary<string, BaseReader<TextureData>> _readers = new()
        {
            { ".ozt", new OZTReader() },
            { ".tga", new OZTReader() },
            { ".ozj", new OZJReader() },
            { ".jpg", new OZJReader() },
            { ".ozp", new OZPReader() },
            { ".png", new OZPReader() },
            { ".ozd", new OZDReader() },
            { ".dds", new OZDReader() }
        };

        public Task<TextureData> Prepare(string path)
        {
            GD.Print($"Prepare: {path}");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            string key = path.ToLowerInvariant();

            if (_textureTasks.TryGetValue(key, out var task))
                return task;

            task = InternalPrepare(path);
            _textureTasks.TryAdd(key, task);
            return task;
        }

        public async Task<Texture2D> PrepareAndGetTexture(string path)
        {
            GD.Print($"PrepareAndGetTexture: {path}");
            await Prepare(path);
            return GetTexture(path);
        }

        private async Task<TextureData> InternalPrepare(string path)
        {
            try
            {
                var dataPath = Path.Combine(Constants.DataPath, path);
                string ext = Path.GetExtension(path)?.ToLowerInvariant();

                GD.Print($"InternalPrepare: {dataPath} ({ext})");

                if (!_readers.TryGetValue(ext, out var reader))
                {
                    GD.PrintErr($"Unsupported texture format: {ext}");
                    return null;
                }

                string fullPath = FindTexturePath(dataPath, ext);
                if (fullPath == null)
                    return null;

                GD.Print("fullpath: " + fullPath);

                var data = await reader.Load(fullPath);
                if (data == null)
                    return null;

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(path)
                };

                _textures.TryAdd(path.ToLowerInvariant(), clientTexture);
                return data;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Texture load failed: {path} → {e.Message}");
                return null;
            }
        }

        private string FindTexturePath(string dataPath, string ext)
        {
            string expectedExt = _readers[ext].GetType().Name
                .ToLowerInvariant()
                .Replace("reader", "");

            string expectedPath = Path.ChangeExtension(dataPath, expectedExt);

            GD.Print("This is from FindTexturePath, expectedFilePath: " + expectedPath + "-" + " expectedExtension: " + expectedExt);

            string actual = GetActualPath(expectedPath);
            if (actual != null)
                return actual;

            string parent = Path.GetDirectoryName(expectedPath);
            if (!string.IsNullOrEmpty(parent))
            {
                string alt = Path.Combine(parent, "texture", Path.GetFileName(expectedPath));
                actual = GetActualPath(alt);
                if (actual != null)
                    return actual;
            }

            return null;
        }

        private string GetActualPath(string path)
        {
            if (File.Exists(path))
                return path;

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }

            return null;
        }

        private static TextureScript ParseScript(string fileName)
        {
            if (fileName.Contains("mu_rgb_lights.jpg", StringComparison.OrdinalIgnoreCase))
                return new TextureScript { Bright = true };

            var tokens = Path.GetFileNameWithoutExtension(fileName).Split('_');
            if (tokens.Length <= 1)
                return null;

            var script = new TextureScript();
            string token = tokens[^1].ToLowerInvariant();

            switch (token)
            {
                case "a": script.Alpha = true; break;
                case "r": script.Bright = true; break;
                case "h": script.HiddenMesh = true; break;
                case "s": script.StreamMesh = true; break;
                case "n": script.NoneBlendMesh = true; break;
                case "dc": script.ShadowMesh = 1; break;
                case "dt": script.ShadowMesh = 2; break;
                default: return null;
            }

            return script;
        }

        public TextureData Get(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(path.ToLowerInvariant(), out var v) ? v.Info : null;

        public TextureScript GetScript(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(path.ToLowerInvariant(), out var v) ? v.Script : null;

        public Texture2D GetTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string key = path.ToLowerInvariant();
            if (!_textures.TryGetValue(key, out var clientTexture))
                return null;

            if (clientTexture.Texture != null)
                return clientTexture.Texture;

            var info = clientTexture.Info;
            if (info == null || info.Width == 0 || info.Height == 0 || info.Data == null)
                return null;

            Image.Format format = info.Components == 4
                ? Image.Format.Rgba8
                : Image.Format.Rgb8;

            Image image = Image.CreateFromData(
                (int)info.Width,
                (int)info.Height,
                false,
                format,
                info.Data
            );

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            clientTexture.Texture = texture;

            return texture;
        }
    }
}
