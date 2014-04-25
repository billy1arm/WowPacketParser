using System;
using WowPacketParser.Enums;
using WowPacketParser.Enums.Version;
using WowPacketParser.Misc;
using WowPacketParser.Store;
using WowPacketParser.Store.Objects;
using Guid = WowPacketParser.Misc.Guid;

namespace WowPacketParser.Parsing.Parsers
{
    public static class QueryHandler
    {
        [Parser(Opcode.SMSG_QUERY_TIME_RESPONSE)]
        public static void HandleTimeQueryResponse(Packet packet)
        {
            packet.ReadTime("Current Time");
            packet.ReadInt32("Daily Quest Reset");
        }

        [Parser(Opcode.CMSG_NAME_QUERY, ClientVersionBuild.Zero, ClientVersionBuild.V5_4_7_17898)]
        public static void HandleNameQuery(Packet packet)
        {
            packet.ReadGuid("GUID");
        }

        [Parser(Opcode.SMSG_NAME_QUERY_RESPONSE)]
        public static void HandleNameQueryResponse(Packet packet)
        {
            Guid guid;
            if (ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_0_9767))
            {
                guid = packet.ReadPackedGuid("GUID");
                var end = packet.ReadByte("Result");
                /*
                if (end == 1)
                    DenyItem(&WDB_CACHE_NAME, v11, v12);
                if (end == 2)
                    RetryItem(&WDB_CACHE_NAME, v11, v12);
                if (end == 3)
                {
                    AddItem(&WDB_CACHE_NAME, (int)&v8, v11, v12);
                    SetTemporary(&WDB_CACHE_NAME, v11, v12);
                }
                */
                if (end != 0)
                    return;
            }
            else
                guid = packet.ReadGuid("GUID");

            var name = packet.ReadCString("Name");
            StoreGetters.AddName(guid, name);
            packet.ReadCString("Realm Name");

            TypeCode typeCode = ClientVersion.AddedInVersion(ClientVersionBuild.V3_1_0_9767) ? TypeCode.Byte : TypeCode.Int32;
            packet.ReadEnum<Race>("Race", typeCode);
            packet.ReadEnum<Gender>("Gender", typeCode);
            packet.ReadEnum<Class>("Class", typeCode);

            if (!packet.ReadBoolean("Name Declined"))
                return;

            for (var i = 0; i < 5; i++)
                packet.ReadCString("Declined Name", i);

            var objectName = new ObjectName
            {
                ObjectType = ObjectType.Player,
                Name = name,
            };
            Storage.ObjectNames.Add((uint)guid.GetLow(), objectName, packet.TimeSpan);
        }

        public static void ReadQueryHeader(ref Packet packet)
        {
            var entry = packet.ReadInt32("Entry");
            var guid = packet.ReadGuid("GUID");

            if (packet.Opcode == Opcodes.GetOpcode(Opcode.CMSG_CREATURE_QUERY) || packet.Opcode == Opcodes.GetOpcode(Opcode.CMSG_GAMEOBJECT_QUERY))
                if (guid.HasEntry() && (entry != guid.GetEntry()))
                    packet.WriteLine("Entry does not match calculated GUID entry");
        }

        [Parser(Opcode.CMSG_CREATURE_QUERY, ClientVersionBuild.Zero, ClientVersionBuild.V5_4_7_17898)]
        public static void HandleCreatureQuery(Packet packet)
        {
            ReadQueryHeader(ref packet);
        }

        [HasSniffData]
        [Parser(Opcode.SMSG_CREATURE_QUERY_RESPONSE, ClientVersionBuild.Zero, ClientVersionBuild.V5_4_7_17898)]
        public static void HandleCreatureQueryResponse(Packet packet)
        {
            var entry = packet.ReadEntry("Entry");
            var hasData = packet.ReadBit();
            if (!hasData)
                return; // nothing to do

            var creature = new UnitTemplate();

            creature.RacialLeader = packet.ReadBit("Racial Leader");
            var iconLenS = (int)packet.ReadBits(6);
            var unkLens = (int)packet.ReadBits(11);
            var nameLens = (int)packet.ReadBits(11);
            for (int i = 0; i < 6; i++)
                unkLens = (int)packet.ReadBits(11);

            var qItemCount = packet.ReadBits(22);
            var subLenS = (int)packet.ReadBits(11);
            unkLens = (int)packet.ReadBits(11);

            packet.ResetBitReader();

            creature.Modifier2 = packet.ReadSingle("Modifier 2");

            creature.Name = packet.ReadCString("Name");
            creature.Modifier1 = packet.ReadSingle("Modifier 1");
            creature.KillCredits = new uint[2];
            creature.KillCredits[1] = packet.ReadUInt32("KillCredit 2");
            creature.DisplayIds = new uint[4];
            creature.DisplayIds[1] = packet.ReadUInt32("Display ID 1");
            creature.QuestItems = new uint[qItemCount];

            for (var i = 0; i < qItemCount; ++i)
                creature.QuestItems[i] = (uint)packet.ReadEntryWithName<Int32>(StoreNameType.Item, "Quest Item", i);

            creature.Type = packet.ReadEnum<CreatureType>("Type", TypeCode.Int32);

            if (iconLenS > 1)
                creature.IconName = packet.ReadCString("Icon Name");

            creature.TypeFlags2 = packet.ReadUInt32("Creature Type Flags 2"); // Missing enum
            creature.TypeFlags = packet.ReadEnum<CreatureTypeFlag>("Type Flags", TypeCode.UInt32);
            creature.KillCredits[0] = packet.ReadUInt32("KillCredit 1");
            creature.Family = packet.ReadEnum<CreatureFamily>("Family", TypeCode.Int32);
            creature.MovementId = packet.ReadUInt32("Movement ID");
            creature.Expansion = packet.ReadEnum<ClientType>("Expansion", TypeCode.UInt32);
            creature.DisplayIds[0] = packet.ReadUInt32("Display ID 0");
            creature.DisplayIds[2] = packet.ReadUInt32("Display ID 2");
            creature.Rank = packet.ReadEnum<CreatureRank>("Rank", TypeCode.Int32);
            if (subLenS > 1)
                creature.SubName = packet.ReadCString("Sub Name");
            creature.DisplayIds[3] = packet.ReadUInt32("Display ID 3");

            packet.AddSniffData(StoreNameType.Unit, entry.Key, "QUERY_RESPONSE");

            Storage.UnitTemplates.Add((uint)entry.Key, creature, packet.TimeSpan);

            var objectName = new ObjectName
            {
                ObjectType = ObjectType.Unit,
                Name = creature.Name,
            };
            Storage.ObjectNames.Add((uint)entry.Key, objectName, packet.TimeSpan); 

        }

        [Parser(Opcode.CMSG_PAGE_TEXT_QUERY)]
        public static void HandlePageTextQuery(Packet packet)
        {
            ReadQueryHeader(ref packet);
        }

        [HasSniffData]
        [Parser(Opcode.SMSG_PAGE_TEXT_QUERY_RESPONSE)]
        public static void HandlePageTextResponse(Packet packet)
        {
            var pageText = new PageText();

            var entry = packet.ReadUInt32("Entry");

            pageText.Text = packet.ReadCString("Page Text");

            pageText.NextPageId = packet.ReadUInt32("Next Page");

            packet.AddSniffData(StoreNameType.PageText, (int)entry, "QUERY_RESPONSE");

            Storage.PageTexts.Add(entry, pageText, packet.TimeSpan);
        }

        [Parser(Opcode.CMSG_NPC_TEXT_QUERY, ClientVersionBuild.Zero, ClientVersionBuild.V5_4_7_17898)]
        public static void HandleNpcTextQuery(Packet packet)
        {
            var entry = packet.ReadInt32("Entry");

            var GUID = new byte[8];
            GUID = packet.StartBitStream(5, 6, 7, 4, 3, 0, 2, 1);
            packet.ParseBitStream(GUID, 0, 7, 1, 4, 3, 5, 2, 6);
            packet.WriteGuid("GUID", GUID);
        }

        [HasSniffData]
        [Parser(Opcode.SMSG_NPC_TEXT_UPDATE)]
        public static void HandleNpcTextUpdate(Packet packet)
        {

            var npcText = new NpcText();

            var hasData = packet.ReadBit("hasData");
            var entry = packet.ReadEntry("TextID");
            if (entry.Value) // Can be masked
                return;

            if (!hasData)
                return; // nothing to do

            var size = packet.ReadInt32("Size");

            npcText.Probabilities = new float[8];
            for (var i = 0; i < 8; ++i)
                npcText.Probabilities[i] = packet.ReadSingle("Probability", i);
            for (var i = 0; i < 8; ++i)
                packet.ReadInt32("Unknown Id", i);

            packet.AddSniffData(StoreNameType.NpcText, entry.Key, "QUERY_RESPONSE");

            Storage.NpcTexts.Add((uint)entry.Key, npcText, packet.TimeSpan);
        }
    }
}
