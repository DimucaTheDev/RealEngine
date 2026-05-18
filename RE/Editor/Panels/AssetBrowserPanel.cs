using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using RE.Core.Assets;
using RE.Rendering.Texturing;
using RE.Utils;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Editor.Panels
{
    internal class AssetBrowserPanel
    {
        private static readonly string _assetRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        private static string _currentDirectory = Directory.Exists(_assetRootPath) ? _assetRootPath : ".";
        private static Dictionary<Regex, Texture> _iconOverrides = new();
        private static Texture _dirIconFull;
        private static Texture _dirIconEmpty; 
        private static int _tileSize = 75;

        public AssetBrowserPanel()
        {
            _dirIconFull = new StaticTexture("Assets/Editor/Sprites/folderFull.png");
            _dirIconEmpty = new StaticTexture("Assets/Editor/Sprites/folderEmpty.png"); 
            LoadIconOverrides();
        }

        public void Draw()
        {
            SetNextWindowPos(new Vector2(8, 692), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(894, 309), ImGuiCond.FirstUseEver);

            if (Begin("Asset browser"
                    /*, ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize*/))
            {
                Text("todo: project workspace");
                End();
                return;
                float treePaneWidth = 200f;

                BeginChild("##AssetTreePane", new Vector2(treePaneWidth, 0), ImGuiChildFlags.ResizeX);
                {
                    DrawDirectoryTree(_assetRootPath);
                }
                EndChild();

                SameLine();

                BeginChild("##AssetContentsPane", Vector2.Zero, ImGuiChildFlags.None);
                {
                    DrawDirectoryContents(_currentDirectory);
                }
                EndChild();
            }
            End();
        }

        private static void DrawDirectoryTree(string path)
        {
            string folderName = new DirectoryInfo(path).Name;

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow;

            if (path == _currentDirectory)
            {
                flags |= ImGuiTreeNodeFlags.Selected;
            }

            bool nodeOpen = TreeNodeEx(folderName, flags);

            if (IsItemClicked())
            {
                _currentDirectory = path;
            }

            if (nodeOpen)
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    DrawDirectoryTree(dir);
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    string fileName = Path.GetFileName(file);

                    ImGuiTreeNodeFlags fileFlags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                    TreeNodeEx(fileName, fileFlags);

                    if (IsItemClicked())
                    { }
                }

                TreePop();
            }
        }

        private static void DrawDirectoryContents(string path)
        {
            var pathSegments = Path.GetRelativePath(_assetRootPath, path).Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

            string currentPathAccumulator = _assetRootPath;

            if (Button("[root]"))
            {
                _currentDirectory = _assetRootPath;
            }

            foreach (var dir in pathSegments.Except(["."]))
            {
                SameLine();
                Text("/");

                currentPathAccumulator = Path.Combine(currentPathAccumulator, dir);

                SameLine();

                if (Button(dir))
                {
                    _currentDirectory = currentPathAccumulator;
                }
            }

            SameLine();
            const string tileSizeText = "Tile size:";

            SetCursorPosX(GetWindowSize().X - 150 - CalcTextSize(tileSizeText).X - 10);
            Text(tileSizeText);
            SameLine();
            SetNextItemWidth(150);
            SliderInt("##tileSize", ref _tileSize, 10, 100);

            Separator();

            float availableWidth = GetContentRegionAvail().X;
            float tileSize = _tileSize;
            float cellPadding = 15.0f;

            int columns = (int)Math.Max(1, Math.Floor(availableWidth / (tileSize + cellPadding)));

            ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY;

            var items = Directory.GetDirectories(path)
                .Select(dir => (Path: dir, IsDirectory: true))
                .Concat(Directory.GetFiles(path).Select(file => (Path: file, IsDirectory: false)))
                .ToList();

            if (BeginTable("##AssetGrid", columns, flags))
            {
                for (int i = 0; i < columns; i++)
                {
                    TableSetupColumn($"Col{i}", ImGuiTableColumnFlags.WidthFixed, tileSize + cellPadding);
                }

                foreach (var item in items)
                {
                    TableNextColumn();

                    string name = item.IsDirectory ? new DirectoryInfo(item.Path).Name : Path.GetFileName(item.Path);

                    DrawTileInTable(name, item.IsDirectory, tileSize, item.Path);
                }

                EndTable();
            }
        }

        private static void DrawTileInTable(string name, bool isDirectory, float size, string fullPath)
        {
            PushID(fullPath);

            BeginGroup();

            var iconIntPtr = GetTileIcon(fullPath, isDirectory); 
            bool isClicked = ImageButton(
                $"##IconBtn{name}",
                iconIntPtr,
                new Vector2(size, size),
                Vector2.Zero,
                Vector2.One,
                new Vector4(0, 0, 0, 0));
            if (IsItemHovered() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                if (isDirectory)
                {
                    _currentDirectory = fullPath;
                }
                else
                {
                    Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
                }
            }

            string displayName = TrimFileName(name, _tileSize);
            float textWidth = CalcTextSize(displayName).X;
            SetCursorPosX(GetCursorPosX() + (size - textWidth) * 0.5f);
            Text(displayName);
            if (IsItemHovered())
                SetTooltip(name);

            EndGroup();
            PopID();

        }

        private static ImTextureRef GetTileIcon(string fullPath, bool isDirectory)
        {
            foreach (var (regex, icon) in _iconOverrides)
            {
                if (regex.IsMatch(fullPath))
                    return icon;
            }

            if (isDirectory)
            {
                return Directory.EnumerateFileSystemEntries(fullPath).Any()
                    ? _dirIconFull
                    : _dirIconEmpty;
            }

            return new ImTextureRef
            {
                TexID = WindowsShell.GetFileIcon(fullPath)
            };
        }

        public static void LoadIconOverrides()
        {
            if (!ContentManager.Exists("Assets/Editor/FileIconOverride/overrides.json"))
                return;

            var json = ContentManager.GetString("Assets/Editor/FileIconOverride/overrides.json");

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            foreach (var kv in dict)
            {
                var regex = new Regex(kv.Key, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                _iconOverrides.Add(regex, new StaticTexture(kv.Value));
            }
        }

        // cuts string until it matches tile size
        private static string TrimFileName(string name, float tileSize)
        {
            const string suffix = "..";
            float ellipsisWidth = CalcTextSize(suffix).X;
            float availableWidthForText = tileSize - ellipsisWidth;

            if (CalcTextSize(name).X <= tileSize)
            {
                return name;
            }

            string currentText = name;

            while (currentText.Length > 0 && CalcTextSize(currentText).X > availableWidthForText)
            {
                currentText = currentText[..^1];
            }

            if (currentText.Length < name.Length)
            {
                return currentText + suffix;
            }

            return suffix;
        }
    }
}
