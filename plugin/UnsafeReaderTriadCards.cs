using Dalamud.Game;
using Dalamud.Logging;
using System;
using System.Runtime.InteropServices;

namespace TriadBuddyPlugin
{
    public class UnsafeReaderTriadCards
    {
        public bool HasErrors { get; private set; }

        private delegate byte IsCardOwnedDelegate(IntPtr uiState, ushort cardId);

        private readonly IsCardOwnedDelegate IsCardOwnedFunc;
        private readonly IntPtr UIStatePtr;

        public UnsafeReaderTriadCards(SigScanner sigScanner)
        {
            IntPtr IsCardOwnedPtr = IntPtr.Zero;
            if (sigScanner != null)
            {
                // IsTriadCardOwned(void* uiState, ushort cardId)
                //   used by GSInfoCardList's agent, function preparing card lists
                //   +0x30 ptr to end of list, +0x10c used filter (all, only owned, only missing)
                //   break on end of list write, check loops counting cards at function start in filter == 1 scope

                IsCardOwnedPtr = sigScanner.ScanText("40 53 48 83 ec 20 48 8b d9 66 85 d2 74 3b 0f");

                // UIState addr, use LEA opcode before calling IsTriadCardOwned, same function as described above
                UIStatePtr = sigScanner.GetStaticAddressFromSig("48 8d 0d ?? ?? ?? ?? e8 ?? ?? ?? ?? 84 c0 74 0f 8b cb");
            }

            HasErrors = (IsCardOwnedPtr == IntPtr.Zero) || (UIStatePtr == IntPtr.Zero);
            if (!HasErrors)
            {
                IsCardOwnedFunc = Marshal.GetDelegateForFunctionPointer<IsCardOwnedDelegate>(IsCardOwnedPtr);
            }
            else
            {
                PluginLog.Error("Failed to find triad card functions, turning reader off");
            }
        }

        public bool IsCardOwned(int cardId)
        {
            if (HasErrors || cardId <= 0 || cardId > 65535)
            {
                return false;
            }

            return !HasErrors && IsCardOwnedFunc(UIStatePtr, (ushort)cardId) != 0;
        }
    }
}
