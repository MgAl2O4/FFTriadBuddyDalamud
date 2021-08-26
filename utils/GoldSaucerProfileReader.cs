using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TriadBuddyPlugin
{
    public unsafe class GoldSaucerProfileReader
    {
        public class PlayerDeck
        {
            public string name;
            public int id;
            public ushort[] cardIds = new ushort[5];
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr GetGSProfileDataDelegate(IntPtr uiObject);

        [StructLayout(LayoutKind.Explicit, Size = 0x3A)]
        public unsafe struct GSProfileDeck
        {
            [FieldOffset(0x0)] public fixed byte NameBuffer[32];    // 15 chars + null, can it be unicode? (JA/KO/ZH)
            [FieldOffset(0x30)] public ushort Card0;
            [FieldOffset(0x32)] public ushort Card1;
            [FieldOffset(0x34)] public ushort Card2;
            [FieldOffset(0x36)] public ushort Card3;
            [FieldOffset(0x38)] public ushort Card4;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x284)]           // it's more than that, but i'm not reading anything else so meh, it's good
        public unsafe struct GSProfileData
        {
            [FieldOffset(0x30)] public fixed byte NameBuffer[8];    // "GS.DAT"
            [FieldOffset(0x40)] public GSProfileDeck Deck0;
            [FieldOffset(0x7A)] public GSProfileDeck Deck1;
            [FieldOffset(0xB4)] public GSProfileDeck Deck2;
            [FieldOffset(0xEE)] public GSProfileDeck Deck3;
            [FieldOffset(0x128)] public GSProfileDeck Deck4;
            [FieldOffset(0x162)] public GSProfileDeck Deck5;
            [FieldOffset(0x19C)] public GSProfileDeck Deck6;
            [FieldOffset(0x1D6)] public GSProfileDeck Deck7;
            [FieldOffset(0x210)] public GSProfileDeck Deck8;
            [FieldOffset(0x24A)] public GSProfileDeck Deck9;

            // +0x2B4: 8 bytes about card being viewed already?
        }

        private readonly GameGui gameGui;
        private bool hasAccessFailures;

        public GoldSaucerProfileReader(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public PlayerDeck[] GetPlayerDecks()
        {
            if (hasAccessFailures)
            {
                // hard nope, reverse code again.
                return null;
            }

            try
            {
                // .text: 83 fa 09 77 1d 41 83 f8 04
                // SetCardInDeck(void* GSProfileData, uint deckIdx, uint cardIdx, ushort cardId)
                // 
                // GSProfileData = uiModule.vf28()
                //     e.g. used by GoldSaucerInfo addon in .text: 48 8b f1 48 8b 49 10 48 8b 01 ff 90 e0
                //     SaveDeckToProfile(void* agentPtr)
                //
                //     5.58: addr = uiModulePtr + 0x90dd0, this function is just getter for member var holding pointer

                var uiModulePtr = (gameGui != null) ? gameGui.GetUIModule() : IntPtr.Zero;
                if (uiModulePtr != IntPtr.Zero)
                {
                    // would be a nice place to use gameGui.address.GetVirtualFunction<> :(
                    var getGSProfileDataPtr = new IntPtr(((UIModule*)uiModulePtr)->vfunc[28]);
                    var getGSProfileData = Marshal.GetDelegateForFunctionPointer<GetGSProfileDataDelegate>(getGSProfileDataPtr);

                    var profileDataPtr = getGSProfileData(uiModulePtr);
                    var profileData = Marshal.PtrToStructure<GSProfileData>(profileDataPtr);

                    Func<GSProfileDeck, int, PlayerDeck> ConvertToPlayerDeck = (deckMem, deckId) =>
                    {
                        if (deckMem.Card0 != 0 && deckMem.Card1 != 0 && deckMem.Card2 != 0 && deckMem.Card3 != 0 && deckMem.Card4 != 0)
                        {
                            PlayerDeck deckOb = new() { id = deckId };

                            deckOb.name = GetStringFromBytes(deckMem.NameBuffer, 32);
                            deckOb.cardIds[0] = deckMem.Card0;
                            deckOb.cardIds[1] = deckMem.Card1;
                            deckOb.cardIds[2] = deckMem.Card2;
                            deckOb.cardIds[3] = deckMem.Card3;
                            deckOb.cardIds[4] = deckMem.Card4;

                            return deckOb;
                        }

                        return null;
                    };

                    // just 5 decks, no idea what other 5..9 are used for
                    return new PlayerDeck[]
                    {
                        ConvertToPlayerDeck(profileData.Deck0, 0),
                        ConvertToPlayerDeck(profileData.Deck1, 1),
                        ConvertToPlayerDeck(profileData.Deck2, 2),
                        ConvertToPlayerDeck(profileData.Deck3, 3),
                        ConvertToPlayerDeck(profileData.Deck4, 4)
                    };
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to read GS profile data, turning reader off");
                hasAccessFailures = true;
            }

            return null;
        }

        private string GetStringFromBytes(byte* data, int size)
        {
            byte[] buffer = new byte[size];
            for (int idx = 0; idx < size; idx++)
            {
                buffer[idx] = data[idx];
            }

            return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }
    }
}
