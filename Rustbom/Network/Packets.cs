using System;
using System.Text;
using System.Collections.Generic;

namespace RustlikeServer.Network
{
    // Tipos de pacotes
    public enum PacketType : byte
    {
        ConnectionRequest = 0,
        ConnectionAccept = 1,
        PlayerSpawn = 2,
        PlayerMovement = 3,
        PlayerDisconnect = 4,
        WorldState = 5,
        Heartbeat = 6,
        ClientReady = 7,
        
        // Sistema de Stats
        StatsUpdate = 8,
        PlayerDeath = 9,
        PlayerRespawn = 10,
        TakeDamage = 11,
        ConsumeItem = 12,
        
        // Sistema de Inventário
        InventoryUpdate = 13,
        ItemUse = 14,
        ItemMove = 15,
        ItemDrop = 16,
        HotbarSelect = 17,
        
        // ⭐ NOVO: Sistema de Gathering/Recursos
        ResourcesSync = 18,      // Servidor -> Cliente (sincroniza todos os recursos)
        ResourceHit = 19,        // Cliente -> Servidor (player bate no recurso)
        ResourceUpdate = 20,     // Servidor -> Todos (atualiza HP de recurso)
        ResourceDestroyed = 21,  // Servidor -> Todos (recurso foi destruído)
        ResourceRespawn = 22,    // Servidor -> Todos (recurso respawnou)
        GatherResult = 23        // Servidor -> Cliente (recursos coletados)
    }

    // Classe base para serialização de pacotes
    public class Packet
    {
        public PacketType Type { get; set; }
        public byte[] Data { get; set; }

        public Packet(PacketType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        public byte[] Serialize()
        {
            byte[] result = new byte[1 + 4 + Data.Length];
            result[0] = (byte)Type;
            BitConverter.GetBytes(Data.Length).CopyTo(result, 1);
            Data.CopyTo(result, 5);
            return result;
        }

        public static Packet Deserialize(byte[] data)
        {
            if (data.Length < 5) return null;
            
            PacketType type = (PacketType)data[0];
            int dataLength = BitConverter.ToInt32(data, 1);
            byte[] packetData = new byte[dataLength];
            Array.Copy(data, 5, packetData, 0, dataLength);
            
            return new Packet(type, packetData);
        }
    }

    // Pacote de requisição de conexão
    public class ConnectionRequestPacket
    {
        public string PlayerName { get; set; }

        public byte[] Serialize()
        {
            return Encoding.UTF8.GetBytes(PlayerName);
        }

        public static ConnectionRequestPacket Deserialize(byte[] data)
        {
            return new ConnectionRequestPacket
            {
                PlayerName = Encoding.UTF8.GetString(data)
            };
        }
    }

    // Pacote de aceitação de conexão
    public class ConnectionAcceptPacket
    {
        public int PlayerId { get; set; }
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }
        public float SpawnZ { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[16];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(SpawnX).CopyTo(data, 4);
            BitConverter.GetBytes(SpawnY).CopyTo(data, 8);
            BitConverter.GetBytes(SpawnZ).CopyTo(data, 12);
            return data;
        }

        public static ConnectionAcceptPacket Deserialize(byte[] data)
        {
            return new ConnectionAcceptPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                SpawnX = BitConverter.ToSingle(data, 4),
                SpawnY = BitConverter.ToSingle(data, 8),
                SpawnZ = BitConverter.ToSingle(data, 12)
            };
        }
    }

    // Pacote de movimento do jogador
    public class PlayerMovementPacket
    {
        public int PlayerId { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[24];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(PosX).CopyTo(data, 4);
            BitConverter.GetBytes(PosY).CopyTo(data, 8);
            BitConverter.GetBytes(PosZ).CopyTo(data, 12);
            BitConverter.GetBytes(RotX).CopyTo(data, 16);
            BitConverter.GetBytes(RotY).CopyTo(data, 20);
            return data;
        }

        public static PlayerMovementPacket Deserialize(byte[] data)
        {
            return new PlayerMovementPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                PosX = BitConverter.ToSingle(data, 4),
                PosY = BitConverter.ToSingle(data, 8),
                PosZ = BitConverter.ToSingle(data, 12),
                RotX = BitConverter.ToSingle(data, 16),
                RotY = BitConverter.ToSingle(data, 20)
            };
        }
    }

    // Pacote de spawn de jogador
    public class PlayerSpawnPacket
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(PlayerName);
            byte[] data = new byte[20 + nameBytes.Length];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(nameBytes.Length).CopyTo(data, 4);
            nameBytes.CopyTo(data, 8);
            BitConverter.GetBytes(PosX).CopyTo(data, 8 + nameBytes.Length);
            BitConverter.GetBytes(PosY).CopyTo(data, 12 + nameBytes.Length);
            BitConverter.GetBytes(PosZ).CopyTo(data, 16 + nameBytes.Length);
            return data;
        }

        public static PlayerSpawnPacket Deserialize(byte[] data)
        {
            int nameLength = BitConverter.ToInt32(data, 4);
            return new PlayerSpawnPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                PlayerName = Encoding.UTF8.GetString(data, 8, nameLength),
                PosX = BitConverter.ToSingle(data, 8 + nameLength),
                PosY = BitConverter.ToSingle(data, 12 + nameLength),
                PosZ = BitConverter.ToSingle(data, 16 + nameLength)
            };
        }
    }

    // Pacote de atualização de stats
    public class StatsUpdatePacket
    {
        public int PlayerId { get; set; }
        public float Health { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Temperature { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[20];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(Health).CopyTo(data, 4);
            BitConverter.GetBytes(Hunger).CopyTo(data, 8);
            BitConverter.GetBytes(Thirst).CopyTo(data, 12);
            BitConverter.GetBytes(Temperature).CopyTo(data, 16);
            return data;
        }

        public static StatsUpdatePacket Deserialize(byte[] data)
        {
            return new StatsUpdatePacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                Health = BitConverter.ToSingle(data, 4),
                Hunger = BitConverter.ToSingle(data, 8),
                Thirst = BitConverter.ToSingle(data, 12),
                Temperature = BitConverter.ToSingle(data, 16)
            };
        }
    }

    // Pacote de morte de jogador
    public class PlayerDeathPacket
    {
        public int PlayerId { get; set; }
        public string KillerName { get; set; }

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(KillerName ?? "");
            byte[] data = new byte[8 + nameBytes.Length];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(nameBytes.Length).CopyTo(data, 4);
            nameBytes.CopyTo(data, 8);
            return data;
        }

        public static PlayerDeathPacket Deserialize(byte[] data)
        {
            int nameLength = BitConverter.ToInt32(data, 4);
            return new PlayerDeathPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                KillerName = nameLength > 0 ? Encoding.UTF8.GetString(data, 8, nameLength) : ""
            };
        }
    }

    // Pacote de atualização de inventário
    public class InventoryUpdatePacket
    {
        public List<InventorySlotData> Slots { get; set; }

        public InventoryUpdatePacket()
        {
            Slots = new List<InventorySlotData>();
        }

        public byte[] Serialize()
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(Slots.Count));

            foreach (var slot in Slots)
            {
                data.AddRange(BitConverter.GetBytes(slot.SlotIndex));
                data.AddRange(BitConverter.GetBytes(slot.ItemId));
                data.AddRange(BitConverter.GetBytes(slot.Quantity));
            }

            return data.ToArray();
        }

        public static InventoryUpdatePacket Deserialize(byte[] data)
        {
            var packet = new InventoryUpdatePacket();
            int offset = 0;

            int slotCount = BitConverter.ToInt32(data, offset);
            offset += 4;

            for (int i = 0; i < slotCount; i++)
            {
                int slotIndex = BitConverter.ToInt32(data, offset);
                offset += 4;
                int itemId = BitConverter.ToInt32(data, offset);
                offset += 4;
                int quantity = BitConverter.ToInt32(data, offset);
                offset += 4;

                packet.Slots.Add(new InventorySlotData
                {
                    SlotIndex = slotIndex,
                    ItemId = itemId,
                    Quantity = quantity
                });
            }

            return packet;
        }
    }

    public class InventorySlotData
    {
        public int SlotIndex { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
    }

    // Pacote de uso de item
    public class ItemUsePacket
    {
        public int SlotIndex { get; set; }

        public byte[] Serialize()
        {
            return BitConverter.GetBytes(SlotIndex);
        }

        public static ItemUsePacket Deserialize(byte[] data)
        {
            return new ItemUsePacket
            {
                SlotIndex = BitConverter.ToInt32(data, 0)
            };
        }
    }

    // Pacote de mover item
    public class ItemMovePacket
    {
        public int FromSlot { get; set; }
        public int ToSlot { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[8];
            BitConverter.GetBytes(FromSlot).CopyTo(data, 0);
            BitConverter.GetBytes(ToSlot).CopyTo(data, 4);
            return data;
        }

        public static ItemMovePacket Deserialize(byte[] data)
        {
            return new ItemMovePacket
            {
                FromSlot = BitConverter.ToInt32(data, 0),
                ToSlot = BitConverter.ToInt32(data, 4)
            };
        }
    }

    // ⭐ NOVO: Pacote de sincronização de recursos
    public class ResourcesSyncPacket
    {
        public List<ResourceData> Resources { get; set; }

        public ResourcesSyncPacket()
        {
            Resources = new List<ResourceData>();
        }

        public byte[] Serialize()
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(Resources.Count));

            foreach (var res in Resources)
            {
                data.AddRange(BitConverter.GetBytes(res.Id));
                data.Add((byte)res.Type);
                data.AddRange(BitConverter.GetBytes(res.PosX));
                data.AddRange(BitConverter.GetBytes(res.PosY));
                data.AddRange(BitConverter.GetBytes(res.PosZ));
                data.AddRange(BitConverter.GetBytes(res.Health));
                data.AddRange(BitConverter.GetBytes(res.MaxHealth));
            }

            return data.ToArray();
        }

        public static ResourcesSyncPacket Deserialize(byte[] data)
        {
            var packet = new ResourcesSyncPacket();
            int offset = 0;

            int count = BitConverter.ToInt32(data, offset);
            offset += 4;

            for (int i = 0; i < count; i++)
            {
                packet.Resources.Add(new ResourceData
                {
                    Id = BitConverter.ToInt32(data, offset),
                    Type = data[offset + 4],
                    PosX = BitConverter.ToSingle(data, offset + 5),
                    PosY = BitConverter.ToSingle(data, offset + 9),
                    PosZ = BitConverter.ToSingle(data, offset + 13),
                    Health = BitConverter.ToSingle(data, offset + 17),
                    MaxHealth = BitConverter.ToSingle(data, offset + 21)
                });
                offset += 25;
            }

            return packet;
        }
    }

    public class ResourceData
    {
        public int Id { get; set; }
        public byte Type { get; set; } // 0=Tree, 1=Stone, 2=Metal, 3=Sulfur
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
    }

    // ⭐ NOVO: Pacote de hit em recurso
    public class ResourceHitPacket
    {
        public int ResourceId { get; set; }
        public float Damage { get; set; }
        public int ToolType { get; set; } // 0=Mão, 1=Machado, 2=Picareta

        public byte[] Serialize()
        {
            byte[] data = new byte[12];
            BitConverter.GetBytes(ResourceId).CopyTo(data, 0);
            BitConverter.GetBytes(Damage).CopyTo(data, 4);
            BitConverter.GetBytes(ToolType).CopyTo(data, 8);
            return data;
        }

        public static ResourceHitPacket Deserialize(byte[] data)
        {
            return new ResourceHitPacket
            {
                ResourceId = BitConverter.ToInt32(data, 0),
                Damage = BitConverter.ToSingle(data, 4),
                ToolType = BitConverter.ToInt32(data, 8)
            };
        }
    }

    // ⭐ NOVO: Pacote de atualização de recurso
    public class ResourceUpdatePacket
    {
        public int ResourceId { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[12];
            BitConverter.GetBytes(ResourceId).CopyTo(data, 0);
            BitConverter.GetBytes(Health).CopyTo(data, 4);
            BitConverter.GetBytes(MaxHealth).CopyTo(data, 8);
            return data;
        }

        public static ResourceUpdatePacket Deserialize(byte[] data)
        {
            return new ResourceUpdatePacket
            {
                ResourceId = BitConverter.ToInt32(data, 0),
                Health = BitConverter.ToSingle(data, 4),
                MaxHealth = BitConverter.ToSingle(data, 8)
            };
        }
    }

    // ⭐ NOVO: Pacote de recurso destruído
    public class ResourceDestroyedPacket
    {
        public int ResourceId { get; set; }

        public byte[] Serialize()
        {
            return BitConverter.GetBytes(ResourceId);
        }

        public static ResourceDestroyedPacket Deserialize(byte[] data)
        {
            return new ResourceDestroyedPacket
            {
                ResourceId = BitConverter.ToInt32(data, 0)
            };
        }
    }

    // ⭐ NOVO: Pacote de respawn de recurso
    public class ResourceRespawnPacket
    {
        public int ResourceId { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[12];
            BitConverter.GetBytes(ResourceId).CopyTo(data, 0);
            BitConverter.GetBytes(Health).CopyTo(data, 4);
            BitConverter.GetBytes(MaxHealth).CopyTo(data, 8);
            return data;
        }

        public static ResourceRespawnPacket Deserialize(byte[] data)
        {
            return new ResourceRespawnPacket
            {
                ResourceId = BitConverter.ToInt32(data, 0),
                Health = BitConverter.ToSingle(data, 4),
                MaxHealth = BitConverter.ToSingle(data, 8)
            };
        }
    }

    // ⭐ NOVO: Pacote de resultado de coleta
    public class GatherResultPacket
    {
        public int WoodGained { get; set; }
        public int StoneGained { get; set; }
        public int MetalGained { get; set; }
        public int SulfurGained { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[16];
            BitConverter.GetBytes(WoodGained).CopyTo(data, 0);
            BitConverter.GetBytes(StoneGained).CopyTo(data, 4);
            BitConverter.GetBytes(MetalGained).CopyTo(data, 8);
            BitConverter.GetBytes(SulfurGained).CopyTo(data, 12);
            return data;
        }

        public static GatherResultPacket Deserialize(byte[] data)
        {
            return new GatherResultPacket
            {
                WoodGained = BitConverter.ToInt32(data, 0),
                StoneGained = BitConverter.ToInt32(data, 4),
                MetalGained = BitConverter.ToInt32(data, 8),
                SulfurGained = BitConverter.ToInt32(data, 12)
            };
        }
    }
}