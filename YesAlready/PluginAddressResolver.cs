using Dalamud.Game;
using Dalamud.Game.Internal;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;

namespace YesAlready
{
    internal delegate IntPtr OnSetupDelegate(IntPtr addon, uint a2, IntPtr dataPtr);

    internal class PluginAddressResolver : BaseAddressResolver
    {
        public IntPtr AddonSelectYesNoOnSetupAddress { get; private set; }
        public IntPtr AddonSalvageDialongOnSetupAddress { get; private set; }
        public IntPtr AddonMaterializeDialongOnSetupAddress { get; private set; }
        public IntPtr AddonItemInspectionResultOnSetupAddress { get; private set; }
        public IntPtr AddonRetainerTaskAskOnSetupAddress { get; private set; }
        public IntPtr AddonRetainerTaskResultOnSetupAddress { get; private set; }
        public IntPtr AddonGrandCompanySupplyRewardOnSetupAddress { get; private set; }
        public IntPtr AddonShopCardDialogOnSetupAddress { get; private set; }
        public IntPtr AddonTalkVf46Address { get; private set; }

        private const string AddonSelectYesNoOnSetupSignature =  // Client::UI::AddonSelectYesno.OnSetup
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 40 44 8B F2 0F 29 74 24 ??";
        private const string AddonSalvageDialogOnSetupSignature =  // Client::UI::AddonSalvageDialog.OnSetup
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 30 44 8B F2 49 8B E8";
        private const string AddonMaterializeDialogOnSetupSignature =  // Client::UI::AddonMaterializeDialog.OnSetup
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 50 44 8B F2 49 8B E8 BA ?? ?? ?? ??";
        private const string AddonItemInspectionResultOnSetupSignature =  // Client::UI::AddonItemInspectionResult.OnSetup
             "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B F2 49 8B F8 BA ?? ?? ?? ?? 48 8B D9 E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 48 8B D0";
        private const string AddonRetainerTaskAskOnSetupSignature =  // Client::UI::AddonRetainerTaskAsk.OnSetup
            "40 53 48 83 EC 30 48 8B D9 83 FA 03 7C 53 49 8B C8 E8 ?? ?? ?? ??";
        private const string AddonRetainerTaskResultOnSetupSignature =  // Client::UI::AddonRetainerTaskResult.OnSetup
            "48 89 5C 24 ?? 55 56 57 48 83 EC 40 8B F2 49 8B F8 BA ?? ?? ?? ?? 48 8B D9 E8 ?? ?? ?? ??";
        private const string AddonGrandCompanySupplyRewardOnSetupSignature =  // Client::UI::AddonGrandCompanySupplyReward.OnSetup
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 30 BA ?? ?? ?? ?? 4D 8B E8 4C 8B F9";
        private const string AddonShopCardDialogOnSetupSignature =  // Client::UI::AddonShopCardDialog.OnSetup
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 54 41 56 41 57 48 83 EC 50 48 8B F9 49 8B F0";
        private const string AddonTalkVf46Signature =  // Client::UI::AddonTalk.vf46
            "4C 8B DC 55 57 41 55 49 8D 6B 98 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4";

        protected override void Setup64Bit(SigScanner scanner)
        {
            AddonSelectYesNoOnSetupAddress = scanner.ScanText(AddonSelectYesNoOnSetupSignature);
            AddonSalvageDialongOnSetupAddress = scanner.ScanText(AddonSalvageDialogOnSetupSignature);
            AddonMaterializeDialongOnSetupAddress = scanner.ScanText(AddonMaterializeDialogOnSetupSignature);
            AddonItemInspectionResultOnSetupAddress = scanner.ScanText(AddonItemInspectionResultOnSetupSignature);
            AddonRetainerTaskAskOnSetupAddress = scanner.ScanText(AddonRetainerTaskAskOnSetupSignature);
            AddonRetainerTaskResultOnSetupAddress = scanner.ScanText(AddonRetainerTaskResultOnSetupSignature);
            AddonGrandCompanySupplyRewardOnSetupAddress = scanner.ScanText(AddonGrandCompanySupplyRewardOnSetupSignature);
            AddonShopCardDialogOnSetupAddress = scanner.ScanText(AddonShopCardDialogOnSetupSignature);
            AddonTalkVf46Address = scanner.ScanText(AddonTalkVf46Signature);

            PluginLog.Verbose("===== YES ALREADY =====");
            PluginLog.Verbose($"{nameof(AddonSelectYesNoOnSetupAddress)} {AddonSelectYesNoOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonSalvageDialongOnSetupAddress)} {AddonSalvageDialongOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonMaterializeDialongOnSetupAddress)} {AddonMaterializeDialongOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonItemInspectionResultOnSetupAddress)} {AddonItemInspectionResultOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonRetainerTaskAskOnSetupAddress)} {AddonRetainerTaskAskOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonRetainerTaskResultOnSetupAddress)} {AddonRetainerTaskResultOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonGrandCompanySupplyRewardOnSetupAddress)} {AddonGrandCompanySupplyRewardOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonShopCardDialogOnSetupAddress)} {AddonShopCardDialogOnSetupAddress.ToInt64():X}");
            PluginLog.Verbose($"{nameof(AddonTalkVf46Address)} {AddonTalkVf46Address.ToInt64():X}");
        }
    }

}
