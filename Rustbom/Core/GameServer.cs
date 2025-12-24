using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using RustlikeServer.Network;
using RustlikeServer.World;

namespace RustlikeServer.Core
{
    /// <summary>
    /// ⭐ ATUALIZADO COM SISTEMA DE GATHERING - Servidor autoritativo UDP
    /// </summary>
    public class GameServer : INetEventListener
    {
        private NetManager _netManager;
        private Dictionary<int, Player> _players;
        private Dictionary<NetPeer, ClientHandler> _clients;
        private Dictionary<int, NetPeer> _playerPeers;
        private int _nextPlayerId;
        private bool _isRunning;
        private readonly int _port;
        private readonly object _playersLock = new object();

        // ⭐ NOVO: Resource Manager
        private ResourceManager _resourceManager;

        // Stats update
        private const float STATS_UPDATE_RATE = 1f;
        private const float STATS_SYNC_RATE = 2f;
        
        // ⭐ NOVO: Resource update
        private const float RESOURCE_UPDATE_RATE = 10f; // Verifica respawns a cada 10s

        private NetDataWriter _reusableWriter;

        public GameServer(int port = 7777)
        {
            _port = port;
            _players = new Dictionary<int, Player>();
            _clients = new Dictionary<NetPeer, ClientHandler>();
            _playerPeers = new Dictionary<int, NetPeer>();
            _nextPlayerId = 1;
            _isRunning = false;
            _reusableWriter = new NetDataWriter();
            
            // ⭐ NOVO: Inicializa Resource Manager
            _resourceManager = new ResourceManager();
        }

        public async Task StartAsync()
        {
            try
            {
                _netManager = new NetManager(this)
                {
                    AutoRecycle = true,
                    UpdateTime = 15,
                    DisconnectTimeout = 10000,
                    PingInterval = 1000,
                    UnconnectedMessagesEnabled = false
                };

                _netManager.Start(_port);
                _isRunning = true;

                Console.WriteLine($"╔════════════════════════════════════════════════╗");
                Console.WriteLine($"║  SERVIDOR RUST-LIKE (LiteNetLib/UDP)           ║");
                Console.WriteLine($"║  Porta: {_port}                                    ║");
                Console.WriteLine($"║  Sistema de Sobrevivência: ATIVO               ║");
                Console.WriteLine($"║  Sistema de Gathering: ATIVO 🪓🪨             ║");
                Console.WriteLine($"║  Aguardando conexões...                        ║");
                Console.WriteLine($"╚════════════════════════════════════════════════╝");
                Console.WriteLine();

                // ⭐ NOVO: Inicializa recursos do mundo
                _resourceManager.Initialize();

                Task updateTask = UpdateLoopAsync();
                Task statsTask = UpdateStatsLoopAsync();
                Task monitorTask = MonitorPlayersAsync();
                Task resourceTask = UpdateResourcesLoopAsync(); // ⭐ NOVO

                await Task.WhenAll(updateTask, statsTask, monitorTask, resourceTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServer] Erro fatal: {ex.Message}");
            }
        }

        private async Task UpdateLoopAsync()
        {
            while (_isRunning)
            {
                _netManager.PollEvents();
                await Task.Delay(15);
            }
        }

        // ==================== LITENETLIB CALLBACKS ====================

        public void OnPeerConnected(NetPeer peer)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[GameServer] 🔗 Cliente conectado: {peer.Address}:{peer.Port}");
            Console.WriteLine($"[GameServer] Peer ID: {peer.Id} | Ping: {peer.Ping}ms");
            Console.ResetColor();

            var handler = new ClientHandler(peer, this);
            _clients[peer] = handler;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GameServer] ❌ Cliente desconectado: {peer.Address}:{peer.Port}");
            Console.WriteLine($"[GameServer] Razão: {disconnectInfo.Reason}");
            Console.ResetColor();

            if (_clients.TryGetValue(peer, out var handler))
            {
                handler.Disconnect();
                _clients.Remove(peer);
            }
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (_clients.TryGetValue(peer, out var handler))
            {
                byte[] data = reader.GetRemainingBytes();
                _ = handler.ProcessPacketAsync(data);
            }

            reader.Recycle();
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            Console.WriteLine($"[GameServer] Erro de rede: {socketError} em {endPoint}");
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.Accept();
        }

        // ==================== MÉTODOS PÚBLICOS ====================

        public Player CreatePlayer(string name)
        {
            int id = _nextPlayerId++;
            Player player = new Player(id, name);
            
            lock (_playersLock)
            {
                _players[id] = player;
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✅ [GameServer] NOVO PLAYER CRIADO:");
            Console.WriteLine($"   → Nome: {name}");
            Console.WriteLine($"   → ID: {id}");
            Console.WriteLine($"   → Stats iniciais: {player.Stats}");
            Console.WriteLine($"   → Total de jogadores: {_players.Count}");
            Console.ResetColor();
            
            return player;
        }

        public void RemovePlayer(int playerId)
        {
            string playerName = "";
            bool removed = false;
            NetPeer peerToRemove = null;

            lock (_playersLock)
            {
                if (_players.ContainsKey(playerId))
                {
                    playerName = _players[playerId].Name;
                    _players.Remove(playerId);
                    removed = true;
                }

                if (_playerPeers.TryGetValue(playerId, out peerToRemove))
                {
                    _playerPeers.Remove(playerId);
                }
            }

            if (removed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ [GameServer] PLAYER REMOVIDO:");
                Console.WriteLine($"   → Nome: {playerName}");
                Console.WriteLine($"   → ID: {playerId}");
                Console.WriteLine($"   → Jogadores restantes: {_players.Count}");
                Console.ResetColor();
                
                if (peerToRemove != null && _clients.ContainsKey(peerToRemove))
                {
                    _clients[peerToRemove].Disconnect();
                    _clients.Remove(peerToRemove);
                }

                BroadcastPlayerDisconnect(playerId);
            }
        }

        public void RegisterClient(int playerId, NetPeer peer, ClientHandler handler)
        {
            _playerPeers[playerId] = peer;
            _clients[peer] = handler;
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[GameServer] ClientHandler registrado: Player ID {playerId} | Total: {_clients.Count}");
            Console.ResetColor();
        }

        public void SendPacket(NetPeer peer, PacketType type, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            _reusableWriter.Reset();
            _reusableWriter.Put((byte)type);
            _reusableWriter.Put(data.Length);
            _reusableWriter.Put(data);
            
            peer.Send(_reusableWriter, method);
        }

        public void BroadcastToAll(PacketType type, byte[] data, int excludePlayerId = -1, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            _reusableWriter.Reset();
            _reusableWriter.Put((byte)type);
            _reusableWriter.Put(data.Length);
            _reusableWriter.Put(data);

            int sentCount = 0;

            foreach (var kvp in _playerPeers)
            {
                if (kvp.Key == excludePlayerId) continue;
                
                kvp.Value.Send(_reusableWriter, method);
                sentCount++;
            }

            if (sentCount > 0 && type != PacketType.PlayerMovement && type != PacketType.StatsUpdate && type != PacketType.ResourceUpdate)
            {
                Console.WriteLine($"[GameServer] Broadcast {type} enviado para {sentCount} jogadores (method: {method})");
            }
        }

        public void BroadcastPlayerSpawn(Player player)
        {
            var spawnPacket = new PlayerSpawnPacket
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z
            };

            byte[] data = spawnPacket.Serialize();
            Console.WriteLine($"[GameServer] Broadcasting spawn de {player.Name} (ID: {player.Id})");
            BroadcastToAll(PacketType.PlayerSpawn, data, player.Id, DeliveryMethod.ReliableOrdered);
        }

        public void BroadcastPlayerMovement(Player player, ClientHandler sender)
        {
            var movementPacket = new PlayerMovementPacket
            {
                PlayerId = player.Id,
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z,
                RotX = player.Rotation.X,
                RotY = player.Rotation.Y
            };

            byte[] data = movementPacket.Serialize();
            BroadcastToAll(PacketType.PlayerMovement, data, player.Id, DeliveryMethod.Sequenced);
        }

        public void BroadcastPlayerDisconnect(int playerId)
        {
            byte[] data = BitConverter.GetBytes(playerId);
            Console.WriteLine($"[GameServer] Broadcasting disconnect de Player ID: {playerId}");
            BroadcastToAll(PacketType.PlayerDisconnect, data, playerId, DeliveryMethod.ReliableOrdered);
        }

        public async Task SendExistingPlayersTo(ClientHandler newClient)
        {
            var newPlayerId = newClient.GetPlayer()?.Id ?? -1;
            var newPeer = newClient.GetPeer();
            
            int count = 0;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[GameServer] ========== ENVIANDO PLAYERS EXISTENTES ==========");
            Console.WriteLine($"[GameServer] Novo player ID: {newPlayerId}");
            Console.ResetColor();

            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
                Console.WriteLine($"[GameServer] Total de players no servidor: {playersSnapshot.Count}");
            }

            foreach (var player in playersSnapshot)
            {
                if (player.Id == newPlayerId) continue;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[GameServer]   → ✅ Enviando spawn de {player.Name} para novo player...");
                Console.ResetColor();

                var spawnPacket = new PlayerSpawnPacket
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    PosX = player.Position.X,
                    PosY = player.Position.Y,
                    PosZ = player.Position.Z
                };

                byte[] data = spawnPacket.Serialize();

                try
                {
                    SendPacket(newPeer, PacketType.PlayerSpawn, data, DeliveryMethod.ReliableOrdered);
                    await Task.Delay(50);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[GameServer]      ✅ ENVIADO COM SUCESSO!");
                    Console.ResetColor();
                    count++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[GameServer]      ❌ ERRO: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[GameServer] ========== FIM DO ENVIO ==========");
            Console.WriteLine($"[GameServer] Total de players enviados: {count}");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ⭐ NOVO: Envia recursos para um cliente
        public async Task SendResourcesToClient(ClientHandler client)
        {
            var resources = _resourceManager.GetAllResources();
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[GameServer] Enviando {resources.Count} recursos para {client.GetPlayer()?.Name}");
            Console.ResetColor();

            var packet = new ResourcesSyncPacket();
            
            foreach (var resource in resources)
            {
                packet.Resources.Add(new ResourceData
                {
                    Id = resource.Id,
                    Type = (byte)resource.Type,
                    PosX = resource.Position.X,
                    PosY = resource.Position.Y,
                    PosZ = resource.Position.Z,
                    Health = resource.Health,
                    MaxHealth = resource.MaxHealth
                });
            }

            SendPacket(client.GetPeer(), PacketType.ResourcesSync, packet.Serialize(), DeliveryMethod.ReliableOrdered);
            
            await Task.CompletedTask;
        }

        // ⭐ NOVO: Processa coleta de recurso
        public GatherResult GatherResource(int resourceId, float damage, int toolType, Player player)
        {
            return _resourceManager.GatherResource(resourceId, damage, toolType, player);
        }

        // ⭐ NOVO: Broadcast update de recurso
        public void BroadcastResourceUpdate(int resourceId)
        {
            var resource = _resourceManager.GetResource(resourceId);
            if (resource == null || !resource.IsAlive) return;

            var packet = new ResourceUpdatePacket
            {
                ResourceId = resourceId,
                Health = resource.Health,
                MaxHealth = resource.MaxHealth
            };

            BroadcastToAll(PacketType.ResourceUpdate, packet.Serialize(), -1, DeliveryMethod.Unreliable);
        }

        // ⭐ NOVO: Broadcast de recurso destruído
        public void BroadcastResourceDestroyed(int resourceId)
        {
            var packet = new ResourceDestroyedPacket
            {
                ResourceId = resourceId
            };

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[GameServer] 💥 Broadcasting destruição do recurso {resourceId}");
            Console.ResetColor();

            BroadcastToAll(PacketType.ResourceDestroyed, packet.Serialize(), -1, DeliveryMethod.ReliableOrdered);
        }

        // ⭐ NOVO: Broadcast de recurso respawnado
        public void BroadcastResourceRespawn(int resourceId)
        {
            var resource = _resourceManager.GetResource(resourceId);
            if (resource == null || !resource.IsAlive) return;

            var packet = new ResourceRespawnPacket
            {
                ResourceId = resourceId,
                Health = resource.Health,
                MaxHealth = resource.MaxHealth
            };

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[GameServer] ♻️ Broadcasting respawn do recurso {resourceId}");
            Console.ResetColor();

            BroadcastToAll(PacketType.ResourceRespawn, packet.Serialize(), -1, DeliveryMethod.ReliableOrdered);
        }

        // ==================== STATS SYSTEM ====================

        private async Task UpdateStatsLoopAsync()
        {
            DateTime lastStatsUpdate = DateTime.Now;
            DateTime lastStatsSync = DateTime.Now;

            while (_isRunning)
            {
                await Task.Delay(100);

                DateTime now = DateTime.Now;

                if ((now - lastStatsUpdate).TotalSeconds >= STATS_UPDATE_RATE)
                {
                    lastStatsUpdate = now;
                    UpdateAllPlayersStats();
                }

                if ((now - lastStatsSync).TotalSeconds >= STATS_SYNC_RATE)
                {
                    lastStatsSync = now;
                    SyncAllPlayersStats();
                }
            }
        }

        private void UpdateAllPlayersStats()
        {
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
            }

            foreach (var player in playersSnapshot)
            {
                player.UpdateStats();

                if (player.IsDead())
                {
                    HandlePlayerDeath(player);
                }
            }
        }

        private void SyncAllPlayersStats()
        {
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
            }

            foreach (var player in playersSnapshot)
            {
                if (_playerPeers.TryGetValue(player.Id, out var peer))
                {
                    var statsPacket = new StatsUpdatePacket
                    {
                        PlayerId = player.Id,
                        Health = player.Stats.Health,
                        Hunger = player.Stats.Hunger,
                        Thirst = player.Stats.Thirst,
                        Temperature = player.Stats.Temperature
                    };

                    SendPacket(peer, PacketType.StatsUpdate, statsPacket.Serialize(), DeliveryMethod.Unreliable);
                }
            }
        }

        private void HandlePlayerDeath(Player player)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GameServer] ☠️  MORTE: {player.Name} (ID: {player.Id})");
            Console.WriteLine($"[GameServer] Causa: {GetDeathCause(player)}");
            Console.ResetColor();

            var deathPacket = new PlayerDeathPacket
            {
                PlayerId = player.Id,
                KillerName = ""
            };

            BroadcastToAll(PacketType.PlayerDeath, deathPacket.Serialize(), -1, DeliveryMethod.ReliableOrdered);
        }

        private string GetDeathCause(Player player)
        {
            var stats = player.Stats;
            if (stats.Hunger <= 0) return "Fome";
            if (stats.Thirst <= 0) return "Sede";
            if (stats.Temperature < 0) return "Frio Extremo";
            if (stats.Temperature > 40) return "Calor Extremo";
            return "Desconhecida";
        }

        // ⭐ NOVO: Update de recursos (respawns)
        private async Task UpdateResourcesLoopAsync()
        {
            DateTime lastUpdate = DateTime.Now;

            while (_isRunning)
            {
                await Task.Delay(1000); // Verifica a cada 1 segundo

                DateTime now = DateTime.Now;

                if ((now - lastUpdate).TotalSeconds >= RESOURCE_UPDATE_RATE)
                {
                    lastUpdate = now;
                    _resourceManager.Update();
                }
            }
        }

        // ==================== MONITORING ====================

        private async Task MonitorPlayersAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(5000);

                List<Player> timedOutPlayers;
                lock (_playersLock)
                {
                    timedOutPlayers = _players.Values.Where(p => p.IsTimedOut()).ToList();
                }
                
                foreach (var player in timedOutPlayers)
                {
                    Console.WriteLine($"[GameServer] Jogador {player.Name} (ID: {player.Id}) timeout");
                    RemovePlayer(player.Id);
                }

                lock (_playersLock)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n╔════════════════════════════════════════════════╗");
                    Console.WriteLine($"║  JOGADORES ONLINE: {_players.Count,-2}                         ║");
                    Console.WriteLine($"║  CLIENTS CONECTADOS: {_clients.Count,-2}                      ║");
                    Console.WriteLine($"╚════════════════════════════════════════════════╝");
                    
                    if (_players.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n→ Lista de Jogadores:");
                        foreach (var player in _players.Values)
                        {
                            NetPeer peer = _playerPeers.TryGetValue(player.Id, out var p) ? p : null;
                            Console.WriteLine($"   • ID {player.Id}: {player.Name}");
                            Console.WriteLine($"     Posição: ({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");
                            Console.WriteLine($"     Stats: {player.Stats}");
                            Console.WriteLine($"     Ping: {(peer != null ? $"{peer.Ping}ms" : "N/A")}");
                            Console.WriteLine($"     Status: {(player.IsDead() ? "☠️ MORTO" : "✅ VIVO")}");
                        }
                    }
                    
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
        }

        public Player GetPlayer(int playerId)
        {
            lock (_playersLock)
            {
                return _players.TryGetValue(playerId, out var player) ? player : null;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            
            foreach (var client in _clients.Values)
            {
                client.Disconnect();
            }

            _netManager?.Stop();
            Console.WriteLine("[GameServer] Servidor encerrado");
        }
    }
}