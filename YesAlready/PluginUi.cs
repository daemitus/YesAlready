using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace YesAlready
{
    internal class PluginUI : IDisposable
    {
        private readonly YesAlreadyPlugin plugin;

        public PluginUI(YesAlreadyPlugin plugin)
        {
            this.plugin = plugin;

            plugin.Interface.UiBuilder.OpenConfigUi += UiBuilder_OnOpenConfigUi;
            plugin.Interface.UiBuilder.Draw += UiBuilder_OnBuildUi_Config;
            plugin.Interface.UiBuilder.Draw += UiBuilder_OnBuildUi_ZoneList;
        }

        public void Dispose()
        {
            plugin.Interface.UiBuilder.OpenConfigUi -= UiBuilder_OnOpenConfigUi;
            plugin.Interface.UiBuilder.Draw -= UiBuilder_OnBuildUi_ZoneList;
            plugin.Interface.UiBuilder.Draw -= UiBuilder_OnBuildUi_Config;
        }


#if DEBUG
        private bool IsImguiConfigOpen = true;
#else
        private bool IsImguiConfigOpen = false;
#endif

        public void OpenConfig() => IsImguiConfigOpen = true;

        public void UiBuilder_OnOpenConfigUi() => IsImguiConfigOpen = true;

        public void UiBuilder_OnBuildUi_Config()
        {
            if (!IsImguiConfigOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(525, 600), ImGuiCond.FirstUseEver);

            ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);

            ImGui.Begin(plugin.Name, ref IsImguiConfigOpen);

#if DEBUG
            UiBuilder_TestButton();
#endif

            if (ImGui.Checkbox($"Enabled", ref plugin.Configuration.Enabled))
                plugin.SaveConfiguration();

            UiBuilder_TextNodeButtons();
            UiBuilder_TextNodes();
            ResolveAddRemoveTextNodes();

            UiBuilder_ItemsWithoutText();

            ImGui.End();

            ImGui.PopStyleColor();
        }

        #region Testing

        private string DebugClickName = "";
        private string AgentAddonName = "";

        private void UiBuilder_TestButton()
        {
            ImGui.InputText("ClickName", ref DebugClickName, 100);
            ImGui.SameLine();
            if (ImGuiEx.IconButton(FontAwesomeIcon.Check, "Submit"))
            {
                try
                {
                    DebugClickName ??= "";
                    ClickLib.Click.SendClick(DebugClickName.Trim());
                    plugin.PrintMessage($"Clicked {DebugClickName} successfully.");
                }
                catch (ClickLib.ClickNotFoundError ex)
                {
                    plugin.PrintError(ex.Message);
                }
                catch (ClickLib.InvalidClickException ex)
                {
                    plugin.PrintError(ex.Message);
                }
            }

            ImGui.InputText("GetAgentAddonName", ref AgentAddonName, 100);
            ImGui.SameLine();
            if (ImGuiEx.IconButton(FontAwesomeIcon.CheckCircle, "Submit"))
            {
                try
                {
                    var addr = FindAgentInterface(AgentAddonName);
                    plugin.PrintMessage($"Agent{AgentAddonName}={addr.ToInt64():X}");
                }
                catch (Exception ex)
                {
                    plugin.PrintError(ex.Message);
                }
            }
        }

        public unsafe IntPtr FindAgentInterface(string addonName)
        {
            var addon = plugin.GameGui.GetAddonByName(addonName, 1);
            if (addon == IntPtr.Zero) return IntPtr.Zero;
            var id = *(short*)(addon + 0x1CE);
            if (id == 0) id = *(short*)(addon + 0x1CC);
            var framework = plugin.Framework.Address.BaseAddress;
            var uiModule = *(IntPtr*)(framework + 0x29F8);
            var agentModule = uiModule + 0xC3E78;
            for (var i = 0; i < 379; i++)
            {
                var agent = *(IntPtr*)(agentModule + 0x20 + (i * 8));
                if (agent == IntPtr.Zero)
                    continue;
                if (*(short*)(agent + 0x20) == id)
                    return agent;
            }
            return IntPtr.Zero;
        }

        #endregion

        private void UiBuilder_TextNodeButtons()
        {
            var style = ImGui.GetStyle();
            var newStyle = new Vector2(style.ItemSpacing.X / 2, style.ItemSpacing.Y);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, newStyle);

            if (ImGuiEx.IconButton(FontAwesomeIcon.Plus, "Add new entry"))
            {
                var newNode = new TextEntryNode { Enabled = false, Text = "Your text goes here" };
                AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = plugin.Configuration.RootFolder, Index = -1 });
            }

            ImGui.SameLine();
            if (ImGuiEx.IconButton(FontAwesomeIcon.SearchPlus, "Add last seen as new entry"))
            {
                var newNode = new TextEntryNode { Enabled = true, Text = plugin.LastSeenDialogText };
                AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = plugin.Configuration.RootFolder, Index = -1 });
            }

            ImGui.SameLine();
            if (ImGuiEx.IconButton(FontAwesomeIcon.FolderPlus, "Add folder"))
            {
                var newNode = new TextFolderNode { Name = "Untitled folder" };
                AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = plugin.Configuration.RootFolder, Index = -1 });
            }

            var sb = new StringBuilder();
            sb.AppendLine("Enter into the input all or part of the text inside a dialog.");
            sb.AppendLine("For example: \"Teleport to \" for the teleport dialog.");
            sb.AppendLine();
            sb.AppendLine("Alternatively, wrap your text in forward slashes to use as a regex.");
            sb.AppendLine("As such: \"/Teleport to .*? for \\d+ gil\\?/\"");
            sb.AppendLine();
            sb.AppendLine("If it matches, the yes button (and checkbox if present) will be clicked.");
            sb.AppendLine();
            sb.AppendLine("Right click a line to view options.");
            sb.AppendLine("Double click an entry for quick enable/disable.");
            sb.AppendLine("Ctrl-Shift right click a line to delete it and any children.");
            sb.AppendLine();
            sb.AppendLine("Currently supported text addons:");
            sb.AppendLine("  - SelectYesNo");
            sb.AppendLine();
            sb.AppendLine("Non-text addons are each listed separately in the lower config section.");

            ImGui.SameLine();
            ImGuiEx.IconButton(FontAwesomeIcon.QuestionCircle, sb.ToString());

            ImGui.PopStyleVar(); // ItemSpacing
        }

        private void UiBuilder_TextNodes()
        {
            if (ImGui.CollapsingHeader("Text Entries"))
            {
                var root = plugin.Configuration.RootFolder;
                TextNodeDragDrop(root);

                if (root.Children.Count == 0)
                {
                    root.Children.Add(new TextEntryNode() { Enabled = false, Text = "Add some text here!" });
                    plugin.SaveConfiguration();
                }

                foreach (var node in root.Children)
                    UiBuilder_DisplayTextNode(node);
            }
        }

        private void UiBuilder_DisplayTextNode(ITextNode node)
        {
            if (node is TextFolderNode folderNode)
                DisplayTextFolderNode(folderNode);
            else if (node is TextEntryNode macroNode)
                UiBuilder_DisplayTextEntryNode(macroNode);
        }

        private void UiBuilder_DisplayTextEntryNode(TextEntryNode node)
        {
            var validRegex = (node.IsTextRegex && node.TextRegex != null) || !node.IsTextRegex;
            var validZone = !node.ZoneRestricted || (node.ZoneIsRegex && node.ZoneRegex != null) || !node.ZoneIsRegex;

            if (!node.Enabled && (!validRegex || !validZone))
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.5f, 0, 0, 1));
            else if (!node.Enabled)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.5f, .5f, .5f, 1));
            else if (!validRegex || !validZone)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));

            ImGui.TreeNodeEx($"{node.Name}##{node.Name}-tree", ImGuiTreeNodeFlags.Leaf);
            ImGui.TreePop();

            if (!node.Enabled || !validRegex || !validZone)
                ImGui.PopStyleColor();

            if (!validRegex && !validZone)
                ImGuiEx.TextTooltip("Invalid Text and Zone Regex");
            else if (!validRegex)
                ImGuiEx.TextTooltip("Invalid Text Regex");
            else if (!validZone)
                ImGuiEx.TextTooltip("Invalid Zone Regex");


            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    node.Enabled = !node.Enabled;
                    plugin.SaveConfiguration();
                    return;
                }
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    var io = ImGui.GetIO();
                    if (io.KeyCtrl && io.KeyShift)
                    {
                        if (plugin.Configuration.TryFindParent(node, out var parent))
                            RemoveTextNode.Add(new() { Node = node, ParentNode = parent });
                        return;
                    }
                    else
                    {
                        ImGui.OpenPopup($"{node.GetHashCode()}-popup");
                    }
                }
            }

            TextNodePopup(node);
            TextNodeDragDrop(node);
        }

        private void DisplayTextFolderNode(TextFolderNode node)
        {
            var expanded = ImGui.TreeNodeEx($"{node.Name}##{node.GetHashCode()}-tree");

            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    var io = ImGui.GetIO();
                    if (io.KeyCtrl && io.KeyShift)
                    {
                        if (plugin.Configuration.TryFindParent(node, out var parent))
                            RemoveTextNode.Add(new() { Node = node, ParentNode = parent });
                        return;
                    }
                    else
                    {
                        ImGui.OpenPopup($"{node.GetHashCode()}-popup");
                    }
                }
            }

            TextNodePopup(node);
            TextNodeDragDrop(node);

            if (expanded)
            {
                foreach (var childNode in node.Children)
                    UiBuilder_DisplayTextNode(childNode);

                ImGui.TreePop();
            }
        }

        private void TextNodePopup(ITextNode node)
        {
            var style = ImGui.GetStyle();
            var newItemSpacing = new Vector2(style.ItemSpacing.X / 2, style.ItemSpacing.Y);

            if (ImGui.BeginPopup($"{node.GetHashCode()}-popup"))
            {
                if (node is TextEntryNode entryNode)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, newItemSpacing);

                    var enabled = entryNode.Enabled;
                    if (ImGui.Checkbox("Enabled", ref enabled))
                    {
                        entryNode.Enabled = enabled;
                        plugin.SaveConfiguration();
                    }

                    ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGuiEx.GetIconButtonWidth(FontAwesomeIcon.TrashAlt));
                    if (ImGuiEx.IconButton(FontAwesomeIcon.TrashAlt, "Delete"))
                    {
                        if (plugin.Configuration.TryFindParent(node, out var parentNode))
                        {
                            RemoveTextNode.Add(new RemoveTextNodeOperation { Node = node, ParentNode = parentNode });
                        }
                    }

                    var matchText = entryNode.Text;
                    if (ImGui.InputText($"##{node.Name}-matchText", ref matchText, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        entryNode.Text = matchText;
                        plugin.SaveConfiguration();
                    }

                    var zoneRestricted = entryNode.ZoneRestricted;
                    if (ImGui.Checkbox("Zone Restricted", ref zoneRestricted))
                    {
                        entryNode.ZoneRestricted = zoneRestricted;
                        plugin.SaveConfiguration();
                    }

                    var searchWidth = ImGuiEx.GetIconButtonWidth(FontAwesomeIcon.Search);
                    var searchPlusWidth = ImGuiEx.GetIconButtonWidth(FontAwesomeIcon.SearchPlus);

                    ImGui.SameLine(ImGui.GetContentRegionMax().X - searchWidth);
                    if (ImGuiEx.IconButton(FontAwesomeIcon.Search, "Zone List"))
                    {
                        IsImguiZoneListOpen = true;
                    }

                    ImGui.SameLine(ImGui.GetContentRegionMax().X - searchWidth - searchPlusWidth - newItemSpacing.X);
                    if (ImGuiEx.IconButton(FontAwesomeIcon.SearchPlus, "Fill with current zone"))
                    {
                        var currentID = plugin.ClientState.TerritoryType;
                        if (plugin.TerritoryNames.TryGetValue(currentID, out var zoneName))
                        {
                            entryNode.ZoneText = zoneName;
                        }
                        else
                        {
                            entryNode.ZoneText = "Could not find name";
                        }
                    }

                    ImGui.PopStyleVar(); // ItemSpacing

                    var zoneText = entryNode.ZoneText;
                    if (ImGui.InputText($"##{node.Name}-zoneText", ref zoneText, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        entryNode.ZoneText = zoneText;
                        plugin.SaveConfiguration();
                    }
                }

                if (node is TextFolderNode folderNode)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, newItemSpacing);

                    if (ImGuiEx.IconButton(FontAwesomeIcon.Plus, "Add entry"))
                    {
                        var newNode = new TextEntryNode { Enabled = false, Text = "Your text goes here" };
                        AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = folderNode, Index = -1 });
                    }

                    ImGui.SameLine();
                    if (ImGuiEx.IconButton(FontAwesomeIcon.SearchPlus, "Add last seen as new entry"))
                    {
                        var newNode = new TextEntryNode() { Enabled = true, Text = plugin.LastSeenDialogText };
                        AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = folderNode, Index = -1 });
                    }

                    ImGui.SameLine();
                    if (ImGuiEx.IconButton(FontAwesomeIcon.FolderPlus, "Add folder"))
                    {
                        var newNode = new TextFolderNode { Name = "Untitled folder" };
                        AddTextNode.Add(new AddTextNodeOperation { Node = newNode, ParentNode = folderNode, Index = -1 });
                    }

                    var trashWidth = ImGuiEx.GetIconButtonWidth(FontAwesomeIcon.TrashAlt);
                    ImGui.SameLine(ImGui.GetContentRegionMax().X - trashWidth);
                    if (ImGuiEx.IconButton(FontAwesomeIcon.TrashAlt, "Delete"))
                    {
                        if (plugin.Configuration.TryFindParent(node, out var parentNode))
                        {
                            RemoveTextNode.Add(new RemoveTextNodeOperation { Node = node, ParentNode = parentNode });
                        }
                    }

                    ImGui.PopStyleVar(); // ItemSpacing

                    var folderName = folderNode.Name;
                    if (ImGui.InputText($"##{node.Name}-rename", ref folderName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        folderNode.Name = folderName;
                        plugin.SaveConfiguration();
                    }
                }

                ImGui.EndPopup();
            }
        }

        private void TextNodeDragDrop(ITextNode node)
        {
            if (node != plugin.Configuration.RootFolder && ImGui.BeginDragDropSource())
            {
                DraggedNode = node;

                ImGui.Text(node.Name);
                ImGui.SetDragDropPayload("TextNodePayload", IntPtr.Zero, 0);
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("TextNodePayload");

                bool nullPtr;
                unsafe { nullPtr = payload.NativePtr == null; }

                var targetNode = node;
                if (!nullPtr && payload.IsDelivery() && DraggedNode != null)
                {
                    if (plugin.Configuration.TryFindParent(DraggedNode, out var draggedNodeParent))
                    {
                        if (targetNode is TextFolderNode targetFolderNode)
                        {
                            AddTextNode.Add(new AddTextNodeOperation { Node = DraggedNode, ParentNode = targetFolderNode, Index = -1 });
                            RemoveTextNode.Add(new RemoveTextNodeOperation { Node = DraggedNode, ParentNode = draggedNodeParent });
                        }
                        else
                        {
                            if (plugin.Configuration.TryFindParent(targetNode, out var targetNodeParent))
                            {
                                var targetNodeIndex = targetNodeParent.Children.IndexOf(targetNode);
                                if (targetNodeParent == draggedNodeParent)
                                {
                                    var draggedNodeIndex = targetNodeParent.Children.IndexOf(DraggedNode);
                                    if (draggedNodeIndex < targetNodeIndex)
                                    {
                                        targetNodeIndex -= 1;
                                    }
                                }
                                AddTextNode.Add(new AddTextNodeOperation { Node = DraggedNode, ParentNode = targetNodeParent, Index = targetNodeIndex });
                                RemoveTextNode.Add(new RemoveTextNodeOperation { Node = DraggedNode, ParentNode = draggedNodeParent });
                            }
                            else
                            {
                                throw new Exception($"Could not find parent of node \"{targetNode.Name}\"");
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"Could not find parent of node \"{DraggedNode.Name}\"");
                    }

                    DraggedNode = null;
                }

                ImGui.EndDragDropTarget();
            }
        }

        private void ResolveAddRemoveTextNodes()
        {
            if (RemoveTextNode.Count > 0)
            {
                foreach (var inst in RemoveTextNode)
                {
                    inst.ParentNode.Children.Remove(inst.Node);
                }
                RemoveTextNode.Clear();
                plugin.SaveConfiguration();
            }

            if (AddTextNode.Count > 0)
            {
                foreach (var inst in AddTextNode)
                {
                    if (inst.Index < 0)
                        inst.ParentNode.Children.Add(inst.Node);
                    else
                        inst.ParentNode.Children.Insert(inst.Index, inst.Node);
                }
                AddTextNode.Clear();
                plugin.SaveConfiguration();
            }
        }

        private void UiBuilder_ItemsWithoutText()
        {
            if (!ImGui.CollapsingHeader("Non-text Matching"))
                return;

            if (ImGui.Checkbox("Desynthesis", ref plugin.Configuration.DesynthDialogEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("Don't blame me when you destroy something important.");

            if (ImGui.Checkbox("Desynthesis (Bulk)", ref plugin.Configuration.DesynthBulkDialogEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("That little checkbox when wanting to bulk desynthesize. The checkbox isn't actually clicked, but it works.");

            if (ImGui.Checkbox("Materialize", ref plugin.Configuration.MaterializeDialogEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("The dialog that extracts materia from items.");

            if (ImGui.Checkbox("Item Inspection Result", ref plugin.Configuration.ItemInspectionResultEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("Eureka/Bozja lockboxes, forgotten fragments, and more.");

            if (ImGui.Checkbox("Assign on Retainer Venture Request", ref plugin.Configuration.RetainerTaskAskEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("The final dialog before sending out a retainer.");

            if (ImGui.Checkbox("Reassign on Retainer Venture Result", ref plugin.Configuration.RetainerTaskResultEnabled))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("Where you receive the item and can resend on the same task.");

            if (ImGui.Checkbox("Grand Company Expert Delivery Reward", ref plugin.Configuration.GrandCompanySupplyReward))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("Don't blame me when you give away something important.");

            if (ImGui.Checkbox("Shop Card Dialog", ref plugin.Configuration.ShopCardDialog))
                plugin.SaveConfiguration();
            ImGuiEx.TextTooltip("When selling Triple Triad cards.");
        }

        #region TextNode DragDrop

        private ITextNode DraggedNode = null;
        private readonly List<AddTextNodeOperation> AddTextNode = new();
        private readonly List<RemoveTextNodeOperation> RemoveTextNode = new();

        private struct AddTextNodeOperation
        {
            public ITextNode Node;
            public TextFolderNode ParentNode;
            public int Index;
        }

        private struct RemoveTextNodeOperation
        {
            public ITextNode Node;
            public TextFolderNode ParentNode;
        }

        #endregion

        private bool IsImguiZoneListOpen = false;
        private bool SortZoneByName = false;

        public void OpenZoneList() => IsImguiZoneListOpen = true;

        public void UiBuilder_OnBuildUi_ZoneList()
        {
            if (!IsImguiZoneListOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(525, 600), ImGuiCond.FirstUseEver);

            ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);

            ImGui.Begin($"{plugin.Name} Zone List", ref IsImguiZoneListOpen);

            ImGui.Text($"Current ID: {plugin.ClientState.TerritoryType}");

            ImGui.Checkbox("Sort by Name", ref SortZoneByName);

            ImGui.Columns(2);

            ImGui.Text("ID");
            ImGui.NextColumn();

            ImGui.Text("Name");
            ImGui.NextColumn();

            ImGui.Separator();

            var names = plugin.TerritoryNames.AsEnumerable();
            if (SortZoneByName)
                names = names.ToList().OrderBy(kvp => kvp.Value);

            foreach (var kvp in names)
            {
                ImGui.Text($"{kvp.Key}");
                ImGui.NextColumn();

                ImGui.Text($"{kvp.Value}");
                ImGui.NextColumn();
            }

            ImGui.Columns(1);

            ImGui.End();

            ImGui.PopStyleColor();
        }
    }

    internal static class ImGuiEx
    {
        public static bool IconButton(FontAwesomeIcon icon) => IconButton(icon);

        public static bool IconButton(FontAwesomeIcon icon, string tooltip, int width = -1)
        {
            ImGui.PushFont(UiBuilder.IconFont);

            if (width > 0)
                ImGui.SetNextItemWidth(32);

            var result = ImGui.Button($"{icon.ToIconString()}##{icon.ToIconString()}-{tooltip}");
            ImGui.PopFont();

            if (tooltip != null)
                TextTooltip(tooltip);

            return result;
        }

        public static float GetIconWidth(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);

            var width = ImGui.CalcTextSize($"{icon.ToIconString()}").X;

            ImGui.PopFont();

            return width;
        }

        public static float GetIconButtonWidth(FontAwesomeIcon icon)
        {
            var style = ImGui.GetStyle();

            return GetIconWidth(icon) + (style.FramePadding.X * 2);
        }

        public static void TextTooltip(string text)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(text);
                ImGui.EndTooltip();
            }
        }

        #region rotation

        private static int rotation_start_index;

        public static Vector2 Min(Vector2 lhs, Vector2 rhs) => new(lhs.X < rhs.X ? lhs.X : rhs.X, lhs.Y < rhs.Y ? lhs.Y : rhs.Y);

        public static Vector2 Max(Vector2 lhs, Vector2 rhs) => new(lhs.X >= rhs.X ? lhs.X : rhs.X, lhs.Y >= rhs.Y ? lhs.Y : rhs.Y);

        private static Vector2 Rotate(Vector2 v, float cos_a, float sin_a) => new((v.X * cos_a) - (v.Y * sin_a), (v.X * sin_a) + (v.Y * cos_a));

        public static void RotateStart()
        {
            rotation_start_index = ImGui.GetWindowDrawList().VtxBuffer.Size;
        }

        public static void RotateEnd(double rad) => RotateEnd(rad, RotationCenter());

        public static void RotateEnd(double rad, Vector2 center)
        {
            var sin = (float)Math.Sin(rad);
            var cos = (float)Math.Cos(rad);
            center = Rotate(center, sin, cos) - center;

            var buf = ImGui.GetWindowDrawList().VtxBuffer;
            for (int i = rotation_start_index; i < buf.Size; i++)
                buf[i].pos = Rotate(buf[i].pos, sin, cos) - center;
        }

        private static Vector2 RotationCenter()
        {
            var l = new Vector2(float.MaxValue, float.MaxValue);
            var u = new Vector2(float.MinValue, float.MinValue);

            var buf = ImGui.GetWindowDrawList().VtxBuffer;
            for (int i = rotation_start_index; i < buf.Size; i++)
            {
                l = Min(l, buf[i].pos);
                u = Max(u, buf[i].pos);
            }

            return new Vector2((l.X + u.X) / 2, (l.Y + u.Y) / 2);
        }

        #endregion
    }
}
