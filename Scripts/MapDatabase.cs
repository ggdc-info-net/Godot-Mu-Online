using System.Collections.Generic;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Provides mapping between map numbers and their names.
    /// </summary>
    public static class MapDatabase
    {
        private static readonly Dictionary<ushort, string> MapNames = InitializeMapData();

        private static Dictionary<ushort, string> InitializeMapData()
        {
            // Based on the provided map initializer names and common map IDs
            return new Dictionary<ushort, string>
            {
                { 1, "Lorencia" },
                { 2, "Dungeon" },
                { 3, "Devias" },
                { 4, "Noria" },
                { 5, "Lost Tower" },
                { 6, "Exile" },
                { 7, "Arena" },
                { 8, "Atlans" },
                { 9, "Tarkan" },
                { 10, "Devil Square" },
                { 11, "Icarus" },
                { 94, "SelectCharacterScene" },
                { 95, "LoginScene"}
            };
        }

        /// <summary>
        /// Gets the name of the map based on its number.
        /// </summary>
        /// <param name="mapId">The map number (ID).</param>
        /// <returns>The name of the map, or a default string if not found.</returns>
        public static string GetMapName(ushort mapId)
        {
            return MapNames.TryGetValue(mapId, out var name) ? name : $"Unknown Map ({mapId})";
        }
    }
}