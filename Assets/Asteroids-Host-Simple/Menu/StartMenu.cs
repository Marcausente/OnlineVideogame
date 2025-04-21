using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine.UI;

namespace Asteroids.HostSimple
{
    // A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
    public class StartMenu : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
        [SerializeField] private NetworkPrefabRef _playerDataPrefab;

        [Header("Menu Principal")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private TMP_InputField _nickName = null;
        [SerializeField] private TextMeshProUGUI _nickNamePlaceholder = null;
        [SerializeField] private TMP_InputField _roomName = null;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private string _gameSceneName = "AsteroidsSimple-Game";

        [Header("Lobby")]
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _waitingText;

        // Prefabs y referencias para crear UI dinámicamente si es necesario
        [Header("Prefabs (Automático)")]
        [SerializeField] private GameObject _lobbyPanelPrefab;
        [SerializeField] private Canvas _mainCanvas;

        // Campos para debug
        private bool _debugMode = true;
        private string _playerListText = "";

        private NetworkRunner _runnerInstance = null;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        private string _localPlayerNickname;

        private void Awake()
        {
            // Asegurarse de que tenemos una referencia al canvas
            if (_mainCanvas == null)
            {
                _mainCanvas = FindObjectOfType<Canvas>();
                Debug.Log($"Canvas encontrado: {(_mainCanvas != null ? "Sí" : "No")}");
            }
        }

        private void Start()
        {
            Debug.Log("[StartMenu] Iniciando StartMenu");
            InitializeUI();
        }

        private void InitializeUI()
        {
            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
                Debug.Log("[StartMenu] Panel del menú principal activado");
            }
            else
            {
                Debug.LogError("[StartMenu] El panel del menú principal no está asignado");
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
                Debug.Log("[StartMenu] Panel del lobby desactivado");
            }
            else
            {
                Debug.LogError("[StartMenu] El panel del lobby no está asignado");
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = false;
                _startGameButton.onClick.RemoveAllListeners();
                _startGameButton.onClick.AddListener(StartGameFromLobby);
                Debug.Log("[StartMenu] Botón de inicio de juego configurado");
            }

            if (_statusText != null)
            {
                _statusText.text = "Listo para comenzar";
            }
        }

        private void OnGUI()
        {
            if (!_debugMode) return;

            // Mostrar estado de la conexión y jugadores en modo debug
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            
            GUILayout.Label("Estado de conexión:");
            GUILayout.Label($"Runner: {(_runnerInstance != null ? "Activo" : "Null")}");
            GUILayout.Label($"Es servidor: {(_runnerInstance != null && _runnerInstance.IsServer ? "Sí" : "No")}");
            GUILayout.Label($"Jugadores conectados: {_players.Count}");
            
            GUILayout.Label("Lista de jugadores:");
            GUILayout.Label(_playerListText);
            
            GUILayout.Label("Elementos UI:");
            GUILayout.Label($"Lobby Panel: {(_lobbyPanel != null ? (_lobbyPanel.activeSelf ? "Activo" : "Inactivo") : "Null")}");
            GUILayout.Label($"Main Menu Panel: {(_mainMenuPanel != null ? (_mainMenuPanel.activeSelf ? "Activo" : "Inactivo") : "Null")}");
            
            GUILayout.EndArea();
        }

        public void StartHost()
        {
            Debug.Log("[StartMenu] Iniciando como host...");
            SetPlayerData();
            StartGame(GameMode.Host, _roomName.text);
        }

        public void StartClient()
        {
            Debug.Log("[StartMenu] Iniciando como cliente...");
            SetPlayerData();
            StartGame(GameMode.Client, _roomName.text);
        }

        private void SetPlayerData()
        {
            _localPlayerNickname = string.IsNullOrWhiteSpace(_nickName.text) ? 
                _nickNamePlaceholder.text : _nickName.text;
            Debug.Log($"[StartMenu] Nickname configurado: {_localPlayerNickname}");
        }

        private async void StartGame(GameMode mode, string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                UpdateStatusText("Error: Se requiere nombre de sala");
                return;
            }

            UpdateStatusText("Conectando...");

            // Crear o obtener la instancia del NetworkRunner
            if (_runnerInstance == null)
            {
                Debug.Log("[StartMenu] Creando nueva instancia de NetworkRunner");
                _runnerInstance = Instantiate(_networkRunnerPrefab);
                DontDestroyOnLoad(_runnerInstance.gameObject);
            }

            // Configurar el NetworkRunner
            _runnerInstance.ProvideInput = true;
            
            // Eliminar todos los callbacks anteriores para evitar duplicados
            _runnerInstance.RemoveCallbacks(this);
            _runnerInstance.AddCallbacks(this);

            // Asegurarse de que tenemos el objeto proveedor
            var objectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>();
            if (objectProvider == null)
            {
                Debug.LogWarning("[StartMenu] No se encontró NetworkObjectPoolDefault, utilizando proveedor por defecto");
            }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 2,
                ObjectProvider = objectProvider,
                SceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>()
            };

            Debug.Log($"[StartMenu] Iniciando juego - Modo: {mode}, Sala: {roomName}, Escena: {_gameSceneName}");

            try
            {
                var result = await _runnerInstance.StartGame(startGameArgs);

                if (result.Ok)
                {
                    Debug.Log("[StartMenu] ¡Conexión exitosa! Mostrando lobby");
                    ShowLobby();
                }
                else
                {
                    Debug.LogError($"[StartMenu] Error al iniciar el juego: {result.ShutdownReason}");
                    UpdateStatusText($"Error de conexión: {result.ShutdownReason}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[StartMenu] Excepción al iniciar el juego: {e}");
                UpdateStatusText("Error al conectar");
            }
        }

        private void ShowLobby()
        {
            Debug.Log("[StartMenu] Mostrando lobby");
            
            if (_mainMenuPanel != null) 
            {
                _mainMenuPanel.SetActive(false);
                Debug.Log("[StartMenu] Panel del menú principal desactivado");
            }
            
            if (_lobbyPanel != null) 
            {
                _lobbyPanel.SetActive(true);
                Debug.Log("[StartMenu] Panel del lobby activado");
            }
            else
            {
                Debug.LogError("[StartMenu] ¡El panel del lobby no está asignado! Verifica las referencias en el Inspector.");
                return;
            }
            
            // Asegurarse de que las referencias son válidas
            if (_playerCountText != null)
                _playerCountText.text = $"Jugadores: {_players.Count}/2";
            else
                Debug.LogError("[StartMenu] PlayerCountText no está asignado");
                
            if (_waitingText != null)
                _waitingText.text = "Esperando jugadores...";
            else
                Debug.LogError("[StartMenu] WaitingText no está asignado");
                
            if (_startGameButton != null)
            {
                _startGameButton.interactable = false;
                Debug.Log("[StartMenu] Botón de inicio desactivado inicialmente");
            }
            else
                Debug.LogError("[StartMenu] StartGameButton no está asignado");
                
            UpdateLobbyUI();
            Debug.Log($"[StartMenu] UI del lobby inicializada - Jugadores conectados: {_players.Count}");
        }

        private void UpdateStatusText(string message)
        {
            Debug.Log($"[StartMenu] Estado actualizado: {message}");
            if (_statusText != null) _statusText.text = message;
        }

        private void UpdateLobbyUI()
        {
            if (_playerCountText != null)
                _playerCountText.text = $"Jugadores: {_players.Count}/2";
            else
                Debug.LogError("[StartMenu] PlayerCountText es null en UpdateLobbyUI");

            bool isHost = _runnerInstance != null && _runnerInstance.IsServer;
            bool canStart = _players.Count == 2 && isHost;

            if (_startGameButton != null)
            {
                _startGameButton.interactable = canStart;
                Debug.Log($"[StartMenu] Botón de inicio {(canStart ? "activado" : "desactivado")}");
            }
            else
                Debug.LogError("[StartMenu] StartGameButton es null en UpdateLobbyUI");

            if (_waitingText != null)
                _waitingText.text = _players.Count < 2 ? "Esperando jugadores..." : "¡Listo para comenzar!";
            else
                Debug.LogError("[StartMenu] WaitingText es null en UpdateLobbyUI");

            // Actualizar texto de debug
            _playerListText = "";
            foreach (var player in _players)
            {
                var playerData = player.Value.GetComponent<PlayerData>();
                string nickname = playerData != null ? playerData.GetNickName() : "Desconocido";
                _playerListText += $"Jugador {player.Key}: {nickname}\n";
            }

            Debug.Log($"[StartMenu] UI del lobby actualizada - Jugadores: {_players.Count}, Host: {isHost}, CanStart: {canStart}");
        }

        public void StartGameFromLobby()
        {
            Debug.Log($"[StartMenu] Intentando iniciar juego desde lobby - Runner: {_runnerInstance != null}, IsServer: {_runnerInstance?.IsServer}, Players: {_players.Count}");
            
            if (_runnerInstance != null && _runnerInstance.IsServer && _players.Count == 2)
            {
                Debug.Log($"[StartMenu] Iniciando partida desde el lobby - Escena: {_gameSceneName}");
                _runnerInstance.LoadScene(_gameSceneName);
            }
            else
            {
                string reason = _runnerInstance == null ? "NetworkRunner es null" :
                                !_runnerInstance.IsServer ? "No eres el host" :
                                _players.Count != 2 ? $"Insuficientes jugadores ({_players.Count}/2)" : "Razón desconocida";
                                
                Debug.LogWarning($"[StartMenu] No se puede iniciar el juego - {reason}");
                
                if (_waitingText != null)
                    _waitingText.text = $"No se puede iniciar: {reason}";
            }
        }

        #region Network Callbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[StartMenu] Jugador unido - ID: {player}");
            
            if (_players.ContainsKey(player))
            {
                Debug.LogWarning($"[StartMenu] Jugador {player} ya existe en la lista");
                return;
            }

            try
            {
                Vector3 spawnPosition = Vector3.zero;
                NetworkObject playerObject = runner.Spawn(_playerDataPrefab, spawnPosition, Quaternion.identity, player);
                
                if (player == runner.LocalPlayer)
                {
                    var playerData = playerObject.GetComponent<PlayerData>();
                    if (playerData != null)
                    {
                        playerData.SetNickName(_localPlayerNickname);
                        Debug.Log($"[StartMenu] Nickname asignado al jugador local: {_localPlayerNickname}");
                    }
                    else
                    {
                        Debug.LogError("[StartMenu] No se pudo obtener el componente PlayerData");
                    }
                }

                _players.Add(player, playerObject);
                Debug.Log($"[StartMenu] Jugador añadido correctamente - Total: {_players.Count}");
                
                // Asegurar que el lobby está visible al unirse
                if (_mainMenuPanel != null && _mainMenuPanel.activeSelf)
                {
                    ShowLobby();
                }
                else
                {
                    UpdateLobbyUI();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[StartMenu] Error al procesar OnPlayerJoined: {e}");
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[StartMenu] Jugador abandonó - ID: {player}");
            
            if (_players.TryGetValue(player, out NetworkObject playerObject))
            {
                try
                {
                    runner.Despawn(playerObject);
                    _players.Remove(player);
                    Debug.Log($"[StartMenu] Jugador eliminado correctamente - Restantes: {_players.Count}");
                    UpdateLobbyUI();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StartMenu] Error al procesar OnPlayerLeft: {e}");
                }
            }
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[StartMenu] Conectado al servidor");
            UpdateStatusText("Conectado");
            
            // Mostrar el lobby al conectarse
            ShowLobby();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[StartMenu] Desconectado del servidor: {reason}");
            UpdateStatusText($"Desconectado: {reason}");
            
            // Limpiar la lista de jugadores
            _players.Clear();
            
            // Volver al menú principal
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
                
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(false);
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[StartMenu] Falló la conexión: {reason}");
            UpdateStatusText($"Error de conexión: {reason}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[StartMenu] Shutdown del runner: {shutdownReason}");
            
            // Limpiar y volver al menú principal
            _players.Clear();
            
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
                
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(false);
                
            UpdateStatusText($"Conexión cerrada: {shutdownReason}");
        }

        #endregion

        #region Unused Callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        #endregion
    }
}