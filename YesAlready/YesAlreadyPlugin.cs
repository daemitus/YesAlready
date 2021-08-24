using ClickLib;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace YesAlready
{
    public sealed class YesAlreadyPlugin : IDalamudPlugin
    {
        public string Name => "Yes Already";
        public string Command => "/pyes";

        internal const int CURRENT_CONFIG_VERSION = 2;

        internal YesAlreadyConfiguration Configuration { get; private set; }
        internal PluginAddressResolver Address { get; private set; }
        internal DalamudPluginInterface Interface { get; private set; }
        internal ChatGui ChatGui { get; private set; }
        internal ClientState ClientState { get; private set; }
        internal CommandManager CommandManager { get; private set; }
        internal DataManager DataManager { get; private set; }
        internal Framework Framework { get; private set; }
        internal GameGui GameGui { get; private set; }
        internal SeStringManager SeStringManager { get; private set; }

        private readonly PluginUI PluginUi;

        private readonly List<Hook<OnSetupDelegate>> OnSetupHooks = new();
        private readonly Hook<OnSetupDelegate> AddonSelectYesNoOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonSalvageDialogOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonMaterializeDialogOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonItemInspectionResultOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonRetainerTaskAskOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonRetainerTaskResultOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonGrandCompanySupplyRewardOnSetupHook;
        private readonly Hook<OnSetupDelegate> AddonShopCardDialogOnSetupHook;

        internal readonly Dictionary<uint, string> TerritoryNames = new();

        public YesAlreadyPlugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ChatGui chatGui,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] DataManager dataManager,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] SeStringManager seStringManager)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            ChatGui = chatGui;
            ClientState = clientState;
            CommandManager = commandManager;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            SeStringManager = seStringManager;

            Configuration = YesAlreadyConfiguration.Load(pluginInterface);
            if (Configuration.Version < CURRENT_CONFIG_VERSION)
            {
                Configuration.Upgrade();
                SaveConfiguration();
            }

            CommandManager.AddHandler(Command, new CommandInfo(OnChatCommand)
            {
                HelpMessage = "Open a window to edit various settings.",
                ShowInHelp = true
            });

            Address = new PluginAddressResolver();
            Address.Setup();

            LoadTerritories();

            PluginUi = new PluginUI(this);

            Click.Initialize();

            OnSetupHooks.Add(AddonSelectYesNoOnSetupHook = new(Address.AddonSelectYesNoOnSetupAddress, AddonSelectYesNoOnSetupDetour));
            OnSetupHooks.Add(AddonSalvageDialogOnSetupHook = new(Address.AddonSalvageDialongOnSetupAddress, AddonSalvageDialogOnSetupDetour));
            OnSetupHooks.Add(AddonMaterializeDialogOnSetupHook = new(Address.AddonMaterializeDialongOnSetupAddress, AddonMaterializeDialogOnSetupDetour));
            OnSetupHooks.Add(AddonItemInspectionResultOnSetupHook = new(Address.AddonItemInspectionResultOnSetupAddress, AddonItemInspectionResultOnSetupDetour));
            OnSetupHooks.Add(AddonRetainerTaskAskOnSetupHook = new(Address.AddonRetainerTaskAskOnSetupAddress, AddonRetainerTaskAskOnSetupDetour));
            OnSetupHooks.Add(AddonRetainerTaskResultOnSetupHook = new(Address.AddonRetainerTaskResultOnSetupAddress, AddonRetainerTaskResultOnSetupDetour));
            OnSetupHooks.Add(AddonGrandCompanySupplyRewardOnSetupHook = new(Address.AddonGrandCompanySupplyRewardOnSetupAddress, AddonGrandCompanySupplyRewardOnSetupDetour));
            OnSetupHooks.Add(AddonShopCardDialogOnSetupHook = new(Address.AddonShopCardDialogOnSetupAddress, AddonShopCardDialogOnSetupDetour));
            OnSetupHooks.ForEach(hook => hook.Enable());
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler(Command);

            OnSetupHooks.ForEach(hook => hook.Dispose());

            PluginUi.Dispose();
        }

        private void LoadTerritories()
        {
            var sheet = DataManager.GetExcelSheet<TerritoryType>();
            foreach (var row in sheet)
            {
                var zone = row.PlaceName.Value;
                if (zone == null)
                    continue;

                var text = GetSeStringText(GetSeString(zone.Name));
                if (string.IsNullOrEmpty(text))
                    continue;

                TerritoryNames.Add(row.RowId, text);
            }
        }

        internal void PrintMessage(string message) => ChatGui.Print($"[{Name}] {message}");

        internal void PrintMessage(SeString message)
        {
            message.Payloads.Insert(0, new TextPayload($"[{Name}] "));
            ChatGui.Print(message);
        }

        internal void PrintError(string message) => ChatGui.PrintError($"[{Name}] {message}");

        internal void SaveConfiguration() => Interface.SavePluginConfig(Configuration);

        #region SeString

        private unsafe SeString GetSeString(byte* textPtr) => GetSeString((IntPtr)textPtr);

        private SeString GetSeString(IntPtr textPtr)
        {
            var size = 0;
            while (Marshal.ReadByte(textPtr, size) != 0)
                size++;

            var bytes = new byte[size];
            Marshal.Copy(textPtr, bytes, 0, size);

            return GetSeString(bytes);
        }

        private SeString GetSeString(Lumina.Text.SeString luminaString)
        {
            var bytes = Encoding.UTF8.GetBytes(luminaString.RawString);
            return GetSeString(bytes);
        }

        private SeString GetSeString(byte[] bytes)
        {
            return SeStringManager.Parse(bytes);

        }

        private string GetSeStringText(SeString sestring)
        {
            var pieces = sestring.Payloads.OfType<TextPayload>().Select(t => t.Text);
            var text = string.Join("", pieces).Replace('\n', ' ').Trim();
            return text;
        }

        #endregion

        #region YesNo

        internal string LastSeenDialogText { get; set; } = "";

        [StructLayout(LayoutKind.Explicit, Size = 0x10)]
        private struct AddonSelectYesNoOnSetupData
        {
            [FieldOffset(0x8)] public IntPtr textPtr;
        }

        private bool EntryMatchesText(TextEntryNode node, string text)
        {
            return (node.IsTextRegex && (node.TextRegex?.IsMatch(text) ?? false)) ||
                  (!node.IsTextRegex && text.Contains(node.Text));
        }

        private bool EntryMatchesZoneName(TextEntryNode node, string zoneName)
        {
            return (node.ZoneIsRegex && (node.ZoneRegex?.IsMatch(zoneName) ?? false)) ||
                  (!node.ZoneIsRegex && zoneName.Contains(node.ZoneText));
        }

        private void AddonSelectYesNoExecute(IntPtr addon)
        {
            unsafe
            {
                var addonObj = (AddonSelectYesno*)addon;
                var yesButton = addonObj->YesButton;
                if (yesButton != null && !yesButton->IsEnabled)
                {
                    PluginLog.Debug($"AddonSelectYesNo: Enabling yes button");
                    yesButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                }
            }

            PluginLog.Debug($"AddonSelectYesNo: Selecting yes");
            Click.SendClick("select_yes", addon);
        }

        private IntPtr AddonSelectYesNoOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonSelectYesNo.OnSetup");
            var result = AddonSelectYesNoOnSetupHook.Original(addon, a2, dataPtr);

            try
            {
                var data = Marshal.PtrToStructure<AddonSelectYesNoOnSetupData>(dataPtr);
                var text = LastSeenDialogText = GetSeStringText(GetSeString(data.textPtr));

                PluginLog.Debug($"AddonSelectYesNo: text={text}");

                if (Configuration.Enabled)
                {
                    var nodes = Configuration.GetAllNodes().OfType<TextEntryNode>();
                    var zoneWarnOnce = true;
                    foreach (var node in nodes)
                    {
                        if (node.Enabled && !string.IsNullOrEmpty(node.Text) && EntryMatchesText(node, text))
                        {
                            if (node.ZoneRestricted && !string.IsNullOrEmpty(node.ZoneText))
                            {
                                if (!TerritoryNames.TryGetValue(ClientState.TerritoryType, out var zoneName))
                                {
                                    if (zoneWarnOnce && !(zoneWarnOnce = false))
                                    {
                                        PluginLog.Debug("Unable to verify Zone Restricted entry, ZoneID was not set yet");
                                        PrintMessage($"Unable to verify Zone Restricted entry, change zones to update value");
                                    }
                                    zoneName = "";
                                }

                                if (!string.IsNullOrEmpty(zoneName) && EntryMatchesZoneName(node, zoneName))
                                {
                                    PluginLog.Debug($"AddonSelectYesNo: Matched on {node.Text} ({node.ZoneText})");
                                    AddonSelectYesNoExecute(addon);
                                    break;
                                }
                            }
                            else
                            {
                                PluginLog.Debug($"AddonSelectYesNo: Matched on {node.Text}");
                                AddonSelectYesNoExecute(addon);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }

        #endregion

        #region Non-text matching

        private void SendClicks(bool enabled, params string[] clicks)
        {
            try
            {
                if (Configuration.Enabled && enabled)
                    foreach (var click in clicks)
                        Click.SendClick(click);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }
        }

        private IntPtr AddonSalvageDialogOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonSalvageDialog.OnSetup");

            var result = AddonSalvageDialogOnSetupHook.Original(addon, a2, dataPtr);

            try
            {
                if (Configuration.Enabled && Configuration.DesynthBulkDialogEnabled)
                {
                    unsafe
                    {
                        ((AddonSalvageDialog*)addon)->BulkDesynthEnabled = true;
                    }
                }

                if (Configuration.Enabled && Configuration.DesynthDialogEnabled)
                {
                    Click.SendClick("desynthesize_checkbox");
                    Click.SendClick("desynthesize");

                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Don't crash the game");
            }

            return result;
        }

        private IntPtr AddonMaterializeDialogOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonMaterializeDialog.OnSetupDetour");
            var result = AddonMaterializeDialogOnSetupHook.Original(addon, a2, dataPtr);
            SendClicks(Configuration.MaterializeDialogEnabled, "materialize");
            return result;
        }

        private IntPtr AddonItemInspectionResultOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonItemInspectionResult.OnSetup");

            var result = AddonItemInspectionResultOnSetupHook.Original(addon, a2, dataPtr);

            if (Configuration.ItemInspectionResultEnabled)
            {
                unsafe
                {
                    var addonPtr = (AddonItemInspectionResult*)addon;

                    if (addonPtr->AtkUnitBase.UldManager.NodeListCount >= 64)
                    {
                        var nameNode = (AtkTextNode*)addonPtr->AtkUnitBase.UldManager.NodeList[64];
                        var descNode = (AtkTextNode*)addonPtr->AtkUnitBase.UldManager.NodeList[55];
                        if (nameNode->AtkResNode.IsVisible && descNode->AtkResNode.IsVisible)
                        {
                            var nameText = GetSeString(nameNode->NodeText.StringPtr);
                            var descText = GetSeStringText(GetSeString(descNode->NodeText.StringPtr));
                            if (descText.Contains("※")  // This is hackish, but works well enough (for now)
                                || descText.Contains("liées à Garde-la-Reine"))  // French doesn't have the widget
                            {
                                nameText.Payloads.Insert(0, new TextPayload("Received: "));
                                PrintMessage(nameText);
                            }
                        }
                    }
                }
            }

            SendClicks(Configuration.ItemInspectionResultEnabled, "item_inspection_result_next");

            return result;
        }

        private IntPtr AddonRetainerTaskAskOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonRetainerTaskAsk.OnSetup");
            var result = AddonRetainerTaskAskOnSetupHook.Original(addon, a2, dataPtr);
            SendClicks(Configuration.RetainerTaskAskEnabled, "retainer_venture_ask_assign");
            return result;
        }

        private IntPtr AddonRetainerTaskResultOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonRetainerTaskResult.OnSetup");
            var result = AddonRetainerTaskResultOnSetupHook.Original(addon, a2, dataPtr);
            SendClicks(Configuration.RetainerTaskResultEnabled, "retainer_venture_result_reassign", "retainer_venture_result_reassign");
            return result;
        }

        private IntPtr AddonGrandCompanySupplyRewardOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonGrandCompanySupplyReward.OnSetup");
            var result = AddonGrandCompanySupplyRewardOnSetupHook.Original(addon, a2, dataPtr);
            SendClicks(Configuration.GrandCompanySupplyReward, "grand_company_expert_delivery_deliver");
            return result;
        }

        private IntPtr AddonShopCardDialogOnSetupDetour(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            PluginLog.Debug($"AddonShopCardDialog.OnSetup");
            var result = AddonShopCardDialogOnSetupHook.Original(addon, a2, dataPtr);
            SendClicks(Configuration.ShopCardDialog, "sell_triple_triad_card");
            return result;
        }

        #endregion

        private void OnChatCommand(string command, string arguments)
        {
            PluginUi.OpenConfig();
        }

    }
}
