using System;
using System.Threading.Tasks;
using LiteNetLib;
using RustlikeServer.Network;
using RustlikeServer.World;
using RustlikeServer.Items;

namespace RustlikeServer.Core
{
    /// <summary>
    /// ⭐ ATUALIZADO COM SISTEMA DE CRAFTING
    /// </summary>
    public class ClientHandler
    {
        private NetPeer _peer;
        private GameServer _server;
        private Player _player;
        private bool _isRunning;
        private bool _isFullyLoaded = false;

        public ClientHandler(NetPeer peer, GameServer server)
        {
            _peer = peer;
            _server = server;
            _isRunning = true;

            Console.WriteLine($"[ClientHandler] Novo ClientHandler criado para Peer ID: {peer.Id}");
        }

        public async Task ProcessPacketAsync(byte[] data)
        {
            try
            {
                Packet packet = Packet.Deserialize(data);
                if (packet == null) return;

                switch (packet.Type)
                {
                    case PacketType.ConnectionRequest:
                        await HandleConnectionRequest(packet.Data);
                        break;

                    case PacketType.ClientReady:
                        await HandleClientReady();
                        break;

                    case PacketType.PlayerMovement:
                        HandlePlayerMovement(packet.Data);
                        break;

                    case PacketType.Heartbeat:
                        HandleHeartbeat();
                        break;

                    case PacketType.PlayerDisconnect:
                        Disconnect();
                        break;

                    case PacketType.ItemUse:
                        await HandleItemUse(packet.Data);
                        break;

                    case PacketType.ItemMove:
                        await HandleItemMove(packet.Data);
                        break;

                    case PacketType.ResourceHit:
                        await HandleResourceHit(packet.Data);
                        break;

                    // ⭐ NOVO: Handlers de Crafting
                    case PacketType.CraftRequest:
                        await HandleCraftRequest(packet.Data);
                        break;

                    case PacketType.CraftCancel:
                        await HandleCraftCancel(packet.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler] Erro ao processar pacote: {ex.Message}");
            }
        }

        private async Task HandleConnectionRequest(byte[] data)
        {
            var request = ConnectionRequestPacket.Deserialize(data);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[ClientHandler] ===== NOVA CONEXÃO =====");
            Console.WriteLine($"[ClientHandler] Nome: {request.PlayerName}");
            Console.WriteLine($"[ClientHandler] Peer ID: {_peer.Id}");
            Console.WriteLine($"[ClientHandler] Endpoint: {_peer.Address}:{_peer.Port}");
            Console.ResetColor();

            _player = _server.CreatePlayer(request.PlayerName);
            Console.WriteLine($"[ClientHandler] Player criado com ID: {_player.Id}");

            _server.RegisterClient(_player.Id, _peer, this);
            Console.WriteLine($"[ClientHandler] ClientHandler registrado");

            var response = new ConnectionAcceptPacket
            {
                PlayerId = _player.Id,
                SpawnX = _player.Position.X,
                SpawnY = _player.Position.Y,
                SpawnZ = _player.Position.Z
            };

            SendPacket(PacketType.ConnectionAccept, response.Serialize());
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ ConnectionAccept ENVIADO para {_player.Name} (ID: {_player.Id})");
            Console.WriteLine($"[ClientHandler] ⏳ AGUARDANDO ClientReady do cliente...");
            Console.ResetColor();

            await Task.CompletedTask;
        }

        private async Task HandleClientReady()
        {
            _isFullyLoaded = true;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[ClientHandler] 📢 CLIENT READY RECEBIDO de {_player.Name} (ID: {_player.Id})");
            Console.WriteLine($"[ClientHandler] Cliente carregou completamente! Iniciando sincronização...");
            Console.ResetColor();

            await Task.Delay(150);

            // Envia inventário
            await SendInventoryUpdate();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[ClientHandler] 📤 Enviando players existentes para {_player.Name}...");
            Console.ResetColor();
            await _server.SendExistingPlayersTo(this);

            await Task.Delay(300);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] 🌲 Enviando recursos do mundo para {_player.Name}...");
            Console.ResetColor();
            await _server.SendResourcesToClient(this);

            await Task.Delay(300);

            // ⭐ NOVO: Envia receitas de crafting
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ClientHandler] 🔨 Enviando receitas de crafting para {_player.Name}...");
            Console.ResetColor();
            await _server.SendRecipesToClient(this);

            await Task.Delay(300);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[ClientHandler] 📢 Broadcasting spawn de {_player.Name} para outros jogadores...");
            Console.ResetColor();
            _server.BroadcastPlayerSpawn(_player);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅✅✅ SINCRONIZAÇÃO COMPLETA: {_player.Name} (ID: {_player.Id})");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void HandlePlayerMovement(byte[] data)
        {
            if (_player == null) return;

            var movement = PlayerMovementPacket.Deserialize(data);
            
            _player.UpdatePosition(movement.PosX, movement.PosY, movement.PosZ);
            _player.UpdateRotation(movement.RotX, movement.RotY);
            _player.UpdateHeartbeat();

            _server.BroadcastPlayerMovement(_player, this);
        }

        private void HandleHeartbeat()
        {
            if (_player != null)
            {
                _player.UpdateHeartbeat();
            }
        }

        private async Task HandleItemUse(byte[] data)
        {
            if (_player == null) return;

            var packet = ItemUsePacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] 🎒 {_player.Name} tentou usar item do slot {packet.SlotIndex}");

            var itemStack = _player.Inventory.GetSlot(packet.SlotIndex);
            if (itemStack == null || itemStack.Definition == null)
            {
                Console.WriteLine($"[ClientHandler] ⚠️ Slot {packet.SlotIndex} vazio");
                return;
            }

            var itemDef = itemStack.Definition;

            if (!itemDef.IsConsumable)
            {
                Console.WriteLine($"[ClientHandler] ⚠️ Item {itemDef.Name} não é consumível");
                return;
            }

            bool canUse = CanUseItem(itemDef, _player.Stats);
            
            if (!canUse)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ClientHandler] ⚠️ {_player.Name} tentou usar {itemDef.Name} mas stats já estão cheias");
                Console.ResetColor();
                return;
            }

            var consumedItem = _player.Inventory.ConsumeItem(packet.SlotIndex);
            if (consumedItem == null)
            {
                Console.WriteLine($"[ClientHandler] ❌ Erro ao consumir item do slot {packet.SlotIndex}");
                return;
            }

            if (consumedItem.HealthRestore > 0)
                _player.Stats.Heal(consumedItem.HealthRestore);
            
            if (consumedItem.HungerRestore > 0)
                _player.Stats.Eat(consumedItem.HungerRestore);
            
            if (consumedItem.ThirstRestore > 0)
                _player.Stats.Drink(consumedItem.ThirstRestore);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ {_player.Name} usou {consumedItem.Name}");
            Console.ResetColor();

            await SendInventoryUpdate();
        }

        private bool CanUseItem(ItemDefinition item, PlayerStats stats)
        {
            if (item.HealthRestore > 0 && stats.Health < 100f)
                return true;

            if (item.HungerRestore > 0 && stats.Hunger < 100f)
                return true;

            if (item.ThirstRestore > 0 && stats.Thirst < 100f)
                return true;

            return false;
        }

        private async Task HandleItemMove(byte[] data)
        {
            if (_player == null) return;

            var packet = ItemMovePacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] 🎒 {_player.Name} moveu item: {packet.FromSlot} → {packet.ToSlot}");

            bool success = _player.Inventory.MoveItem(packet.FromSlot, packet.ToSlot);
            if (success)
            {
                await SendInventoryUpdate();
            }
        }

        private async Task HandleResourceHit(byte[] data)
        {
            if (_player == null) return;

            var packet = ResourceHitPacket.Deserialize(data);
            
            var result = _server.GatherResource(packet.ResourceId, packet.Damage, packet.ToolType, _player);
            
            if (result != null)
            {
                bool success = false;
                
                if (result.WoodGained > 0)
                    success |= _player.Inventory.AddItem(100, result.WoodGained);
                
                if (result.StoneGained > 0)
                    success |= _player.Inventory.AddItem(101, result.StoneGained);
                
                if (result.MetalGained > 0)
                    success |= _player.Inventory.AddItem(102, result.MetalGained);
                
                if (result.SulfurGained > 0)
                    success |= _player.Inventory.AddItem(103, result.SulfurGained);

                var gatherPacket = new GatherResultPacket
                {
                    WoodGained = result.WoodGained,
                    StoneGained = result.StoneGained,
                    MetalGained = result.MetalGained,
                    SulfurGained = result.SulfurGained
                };
                
                SendPacket(PacketType.GatherResult, gatherPacket.Serialize());

                if (success)
                {
                    await SendInventoryUpdate();
                }

                _server.BroadcastResourceUpdate(packet.ResourceId);

                if (result.WasDestroyed)
                {
                    _server.BroadcastResourceDestroyed(packet.ResourceId);
                }
            }

            await Task.CompletedTask;
        }

        // ==================== ⭐ NOVOS HANDLERS DE CRAFTING ====================

        /// <summary>
        /// ⭐ NOVO: Handle de solicitação de crafting
        /// </summary>
        private async Task HandleCraftRequest(byte[] data)
        {
            if (_player == null) return;

            var packet = CraftRequestPacket.Deserialize(data);
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[ClientHandler] 🔨 {_player.Name} solicitou crafting da receita {packet.RecipeId}");
            Console.ResetColor();

            var result = _server.StartCrafting(_player.Id, packet.RecipeId);

            // Envia resposta
            var response = new CraftStartedPacket
            {
                RecipeId = packet.RecipeId,
                Duration = result.Duration,
                Success = result.Success,
                Message = result.Message
            };

            SendPacket(PacketType.CraftStarted, response.Serialize());

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[ClientHandler] ✅ Crafting iniciado para {_player.Name}");
                Console.ResetColor();

                // Atualiza inventário (recursos foram consumidos)
                await SendInventoryUpdate();

                // Envia fila de crafting atualizada
                await SendCraftQueueUpdate();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ClientHandler] ❌ Falha no crafting: {result.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// ⭐ NOVO: Handle de cancelamento de crafting
        /// </summary>
        private async Task HandleCraftCancel(byte[] data)
        {
            if (_player == null) return;

            var packet = CraftCancelPacket.Deserialize(data);
            
            Console.WriteLine($"[ClientHandler] ❌ {_player.Name} cancelou crafting no índice {packet.QueueIndex}");

            bool success = _server.CancelCrafting(_player.Id, packet.QueueIndex);

            if (success)
            {
                await SendCraftQueueUpdate();
            }
        }

        /// <summary>
        /// ⭐ NOVO: Envia fila de crafting atualizada
        /// </summary>
        public async Task SendCraftQueueUpdate()
        {
            if (_player == null) return;

            var queue = _server.GetPlayerCraftQueue(_player.Id);
            var packet = new CraftQueueUpdatePacket();

            foreach (var progress in queue)
            {
                packet.QueueItems.Add(new CraftQueueItem
                {
                    RecipeId = progress.RecipeId,
                    Progress = progress.GetProgress(),
                    RemainingTime = progress.GetRemainingTime()
                });
            }

            SendPacket(PacketType.CraftQueueUpdate, packet.Serialize());
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// ⭐ NOVO: Notifica que crafting foi completo
        /// </summary>
        public async Task NotifyCraftComplete(int recipeId, int resultItemId, int resultQuantity)
        {
            var packet = new CraftCompletePacket
            {
                RecipeId = recipeId,
                ResultItemId = resultItemId,
                ResultQuantity = resultQuantity
            };

            SendPacket(PacketType.CraftComplete, packet.Serialize());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ Notificou {_player.Name} que crafting foi completo");
            Console.ResetColor();

            // Atualiza inventário
            await SendInventoryUpdate();

            // Atualiza fila
            await SendCraftQueueUpdate();
        }

        // ==================== MÉTODOS AUXILIARES ====================

        private async Task SendInventoryUpdate()
        {
            var inventoryPacket = new InventoryUpdatePacket();
            var slots = _player.Inventory.GetAllSlots();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    inventoryPacket.Slots.Add(new InventorySlotData
                    {
                        SlotIndex = i,
                        ItemId = slots[i].ItemId,
                        Quantity = slots[i].Quantity
                    });
                }
            }

            SendPacket(PacketType.InventoryUpdate, inventoryPacket.Serialize());
            Console.WriteLine($"[ClientHandler] 📦 Inventário sincronizado: {inventoryPacket.Slots.Count} slots com itens");

            await Task.CompletedTask;
        }

        public void SendPacket(PacketType type, byte[] data, LiteNetLib.DeliveryMethod method = LiteNetLib.DeliveryMethod.ReliableOrdered)
        {
            try
            {
                if (_peer == null || _peer.ConnectionState != ConnectionState.Connected)
                    return;

                _server.SendPacket(_peer, type, data, method);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ClientHandler] ❌ Erro ao enviar pacote: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Disconnect()
        {
            if (!_isRunning) return;

            _isRunning = false;

            if (_player != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ClientHandler] ❌ Jogador {_player.Name} (ID: {_player.Id}) desconectado");
                Console.ResetColor();
                _server.RemovePlayer(_player.Id);
            }

            if (_peer != null && _peer.ConnectionState == ConnectionState.Connected)
            {
                _peer.Disconnect();
            }
        }

        public Player GetPlayer() => _player;
        public NetPeer GetPeer() => _peer;
        public bool IsConnected() => _isRunning && _peer != null && _peer.ConnectionState == ConnectionState.Connected;
        public bool IsFullyLoaded() => _isFullyLoaded;
    }
}