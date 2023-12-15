using HexaEngine.ImGuiNET;
using Prowl.Icons;
using Prowl.Runtime;
using Prowl.Runtime.ImGUI.Widgets;
using Prowl.Runtime.SceneManagement;
using Raylib_cs;
using static Prowl.Editor.EditorConfiguration;

namespace Prowl.Editor.EditorWindows;

public delegate void OnSelect(GameObject go);

public class HierarchyWindow : EditorWindow {

    protected override ImGuiWindowFlags Flags => ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

    public HierarchyWindow() : base()
    {
        Title = FontAwesome6.FolderTree + " Hierarchy";
    }
    
    private string _searchText = "";
    private GameObject m_RenamingEntity = null;
    private bool clicked = false;

    protected override void Draw()
    {
        ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ContextMenuInBody | ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY;

        float lineHeight = ImGui.GetTextLineHeight();
        float contentWidth = ImGui.GetContentRegionAvail().X;
        const float addButtonSize = 48f;
        System.Numerics.Vector2 padding = ImGui.GetStyle().FramePadding;

        float filterCursorPosX = ImGui.GetCursorPosX();
        GUIHelper.Search("##searchBox", ref _searchText, contentWidth - addButtonSize - 7);

        ImGui.SameLine();

        ImGui.SetCursorPosX(contentWidth - addButtonSize);
        if (ImGui.Button("  " + FontAwesome6.Plus + " Add  "))
            ImGui.OpenPopup("SceneHierarchyContextWindow");

        if (string.IsNullOrEmpty(_searchText))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(filterCursorPosX + ImGui.GetFontSize() * 0.5f);
            ImGui.TextUnformatted(FontAwesome6.MagnifyingGlass + " Search...");
        }
        clicked = false;
        if (ImGui.BeginTable("HierarchyTable", 4, tableFlags))
        {
            if (ImGui.BeginPopupContextWindow("SceneHierarchyContextWindow", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                DrawContextMenu();
                ImGui.EndPopup();
            }

            ImGui.TableSetupColumn("  Label", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoClip, contentWidth);
            ImGui.TableSetupColumn(" " + FontAwesome6.Tag, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);
            ImGui.TableSetupColumn(" " + FontAwesome6.LayerGroup, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);
            ImGui.TableSetupColumn(" " + FontAwesome6.Eye, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);

            ImGui.TableSetupScrollFreeze(0, 1);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers, ImGui.GetFrameHeight());
            for (int column = 0; column < 4; ++column)
            {
                ImGui.TableSetColumnIndex(column);
                string columnName = ImGui.TableGetColumnNameS(column);
                ImGui.PushID(column);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + padding.Y);
                ImGui.TableHeader(columnName);
                ImGui.PopID();
            }

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.0f); 
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0f, 0f));
            for (int i = 0; i < SceneManager.AllGameObjects.Count; i++)
            {
                var go = SceneManager.AllGameObjects[i];
                if (go.Parent == null)
                    DrawEntityNode(go, 0, false);
            }
            ImGui.PopStyleVar(2);

            ImGui.EndTable();

            if (!clicked && ImGui.IsItemClicked(0))
                Selection.Clear();

            if (EditorApplication.IsHotkeyDown("Duplicate", new Hotkey() { Key = KeyboardKey.KEY_D, Ctrl = true }))
                DuplicateSelected();

            unsafe
            {
                if (DragnDrop.ReceiveReference<GameObject>(out var go))
                {
                    go.SetParent(null);
                    Selection.Select(go);
                }

                if (DragnDrop.ReceiveAsset<GameObject>(out var original))
                {
                    GameObject clone = (GameObject)EngineObject.Instantiate(original.Res!, true);
                    clone.AssetID = Guid.Empty; // Remove AssetID so it's not a Prefab - These are just GameObjects like Models
                    clone.Position = original.Res!.Position;
                    clone.Orientation = original.Res!.Orientation;
                    clone.SetParent(null);
                    clone.Recalculate();
                    Selection.Select(clone);
                }
                if (DragnDrop.ReceiveAsset<Prefab>(out var prefab))
                {
                    Selection.Select(prefab.Res.Instantiate());
                }
            }

        }
    }

    void DrawEntityNode(GameObject entity, uint depth, bool isPartOfPrefab)
    {
        if (entity == null) return;
        if (entity.hideFlags.HasFlag(HideFlags.Hide) || entity.hideFlags.HasFlag(HideFlags.HideAndDontSave)) return;

        if (!string.IsNullOrEmpty(_searchText) && !entity.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < entity.Children.Count; i++)
                DrawEntityNode(entity.Children[i], depth, isPartOfPrefab);
            return;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool isPrefab = entity.AssetID != Guid.Empty;
        bool isSelected = Selection.IsSelected(entity);

        ImGuiTreeNodeFlags flags = CalculateFlags(entity, isSelected);

        int colPushCount = 0;
        if (isSelected)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            colPushCount += 2;
        }
        else if (isPrefab)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.0f, 0.3f, 1.0f)));
        else if (isPartOfPrefab)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.0f, 0.3f, 0.5f)));

        if (entity.EnabledInHierarchy == false)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            colPushCount += 1;
        }

        var opened = ImGui.TreeNodeEx(entity.Name + "##" + entity.InstanceID, flags);
        ImGui.PopStyleColor(colPushCount);

        // Select
        if (!ImGui.IsItemToggledOpen() && ImGui.IsItemClicked(0))
        {
            clicked = true;
            Selection.Select(entity, true);
            if (ImGui.IsMouseDoubleClicked(0))
                m_RenamingEntity = entity;
        }

        DrawGameObjectContextMenu(entity);

        // Drag Drop
        HandleDragnDrop(entity);

        if (entity == m_RenamingEntity)
        {
            ImGui.SetKeyboardFocusHere();
            string name = entity.Name;
            ImGui.InputText("##Tag", ref name, 0x100);
            if (ImGui.IsItemDeactivated())
            {
                m_RenamingEntity = null;
                Selection.Foreach<GameObject>((go) =>
                {
                    go.Name = name;
                });
            }
        }

        ImGui.TableNextColumn();
        DrawTagIcon(entity);
        ImGui.TableNextColumn();
        DrawLayerIcon(entity);
        ImGui.TableNextColumn();
        DrawVisibilityToggle(entity);

        // Open
        if (opened)
        {
            for (int i = 0; i < entity.Children.Count; i++)
                DrawEntityNode(entity.Children[i], depth + 1, isPartOfPrefab || isPrefab);

            if (opened && entity.Children.Count > 0)
                ImGui.TreePop();
        }
    }

    private static ImGuiTreeNodeFlags CalculateFlags(GameObject entity, bool isSelected)
    {
        return (isSelected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)
            | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding
            | (entity.Children.Count == 0 ? ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen : 0);
    }

    private static void HandleDragnDrop(GameObject entity)
    {
        // GameObject from Hierarchy
        if (DragnDrop.ReceiveReference<GameObject>(out var go))
        {
            go.SetParent(entity);
            Selection.Select(go);
        }
        // GameObject from Assets
        if (DragnDrop.ReceiveAsset<GameObject>(out var original))
        {
            GameObject clone = (GameObject)EngineObject.Instantiate(original.Res!, true);
            clone.AssetID = Guid.Empty; // Remove AssetID so it's not a Prefab - These are just GameObjects like Models
            clone.Position = original.Res!.Position;
            clone.Orientation = original.Res!.Orientation;
            clone.SetParent(entity);
            clone.Recalculate();
            Selection.Select(clone);
        }
        // Prefab from Assets
        if (DragnDrop.ReceiveAsset<Prefab>(out var prefab))
            Selection.Select(prefab.Res.Instantiate());
        // Scene from Assets
        if (DragnDrop.ReceiveAsset<Scene>(out var scene))
            SceneManager.LoadScene(scene);
        // Offer GameObject up from Hierarchy for Drag And Drop
        DragnDrop.OfferReference(entity);
    }

    private static void DrawTagIcon(GameObject entity)
    {
        ImGui.Text(" " + FontAwesome6.Tag);
        if (ImGui.IsItemHovered())
        {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                ImGui.OpenPopup("GameObjecTags##" + entity.InstanceID);
        }
        GUIHelper.Tooltip("Tag: " + TagLayerManager.tags[entity.tagIndex]);

        if (ImGui.BeginPopup("GameObjecTags##" + entity.InstanceID))
        {
            ImGui.Combo("##Tag", ref entity.tagIndex, TagLayerManager.tags.ToArray(), TagLayerManager.tags.Count);
            ImGui.EndPopup();
        }
    }

    private static void DrawLayerIcon(GameObject entity)
    {
        ImGui.Text(" " + FontAwesome6.LayerGroup);
        if (ImGui.IsItemHovered()) 
        {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                ImGui.OpenPopup("GameObjectLayers##"+entity.InstanceID);
        }
        GUIHelper.Tooltip("Layer: " + TagLayerManager.layers[entity.layerIndex]);

        if (ImGui.BeginPopup("GameObjectLayers##" + entity.InstanceID))
        {
            ImGui.Combo("##Layers", ref entity.layerIndex, TagLayerManager.layers.ToArray(), TagLayerManager.layers.Count);
            ImGui.EndPopup();
        }
    }

    private static void DrawVisibilityToggle(GameObject entity)
    {
        ImGui.Text(" " + (entity.Enabled ? FontAwesome6.Eye : FontAwesome6.EyeSlash));
        if (ImGui.IsItemHovered())
        {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                entity.Enabled = !entity.Enabled;
        }
        GUIHelper.Tooltip("Visibility: " + (entity.Enabled ? "Visible" : "Hidden"));
    }

    void DrawContextMenu(GameObject context = null)
    {
        if (ImGui.MenuItem("New Gameobject"))
        {
            GameObject go = new GameObject("New Gameobject");
            if (context != null)
                go.SetParent(context);
            go.Position = System.Numerics.Vector3.Zero;
            go.Orientation = Prowl.Runtime.Quaternion.Identity;
            go.Scale = System.Numerics.Vector3.One;
            Selection.Select(go);
            ImGui.CloseCurrentPopup();
        }

        ImGui.Separator();
        MenuItem.DrawMenuRoot("Template");
    }

    void DrawGameObjectContextMenu(GameObject entity)
    {
        if (ImGui.BeginPopupContextItem())
        {
            if (!Selection.IsSelected(entity))
                Selection.Select(entity, true);

            if (ImGui.MenuItem("Rename"))
                m_RenamingEntity = entity;
            if (ImGui.MenuItem("Duplicate"))
                DuplicateSelected();
            if (Selection.Count > 0 && ImGui.MenuItem("Align With View"))
            {
                Selection.Foreach<GameObject>((go) =>
                {
                    Camera cam = ViewportWindow.LastFocusedCamera;
                    go.GlobalPosition = cam.GameObject.GlobalPosition;
                    go.GlobalOrientation = cam.GameObject.GlobalOrientation;
                });
            }
            if (Selection.Count == 1 && ImGui.MenuItem("Align View With"))
            {
                Camera cam = ViewportWindow.LastFocusedCamera;
                cam.GameObject.GlobalPosition = entity.GlobalPosition;
                cam.GameObject.GlobalOrientation = entity.GlobalOrientation;
            }

            ImGui.Separator();
            if (ImGui.MenuItem("Delete", "Del"))
                entity.Destroy();
            if (Selection.Count > 0 && ImGui.MenuItem("Delete All"))
            {
                Selection.Foreach<GameObject>((go) =>
                {
                    go.Destroy();
                });
                Selection.Clear();
            }

            DrawContextMenu(entity);

            ImGui.EndPopup();
        }
    }

    public void DuplicateSelected()
    {
        Selection.Foreach<GameObject>((go) =>
        {
            // Duplicating, Easiest way to duplicate is to Serialize then Deserialize
            var serialized = TagSerializer.Serialize(go);
            var deserialized = TagSerializer.Deserialize<GameObject>(serialized);
            deserialized.SetParent(go.Parent);
                Selection.Select(deserialized);
        });
    }
}
