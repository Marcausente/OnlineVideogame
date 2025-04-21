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

        private NetworkRunner _runnerInstance = null;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        private string _localPlayerNickname;

        private void Start()
        {
            Debug.Log("Iniciando StartMenu");
            InitializeUI();
        }

        private void InitializeUI()
        {
            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
                Debug.Log("Panel del menú principal activado");
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
                Debug.Log("Panel del lobby desactivado");
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = false;
                _startGameButton.onClick.AddListener(StartGameFromLobby);
                Debug.Log("Botón de inicio de juego configurado");
            }

            UpdateStatusText("Listo para comenzar");
        }

        public void StartHost()
        {
            Debug.Log("Iniciando como host...");
            SetPlayerData();
            StartGame(GameMode.Host, _roomName.text);
        }

        public void StartClient()
        {
            Debug.Log("Iniciando como cliente...");
            SetPlayerData();
            StartGame(GameMode.Client, _roomName.text);
        }

        private void SetPlayerData()
        {
            _localPlayerNickname = string.IsNullOrWhiteSpace(_nickName.text) ? 
                _nickNamePlaceholder.text : _nickName.text;
            Debug.Log($"Nickname configurado: {_localPlayerNickname}");
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
                Debug.Log("Creando nueva instancia de NetworkRunner");
                _runnerInstance = Instantiate(_networkRunnerPrefab);
                DontDestroyOnLoad(_runnerInstance.gameObject);
            }

            // Configurar el NetworkRunner
            _runnerInstance.ProvideInput = true;
            _runnerInstance.AddCallbacks(this);

            // Asegurarse de que tenemos el objeto proveedor
            var objectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>();
            if (objectProvider == null)
            {
                Debug.LogWarning("No se encontró NetworkObjectPoolDefault, utilizando proveedor por defecto");
            }

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 2,
                ObjectProvider = objectProvider,
                SceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>()
            };

            Debug.Log($"Iniciando juego - Modo: {mode}, Sala: {roomName}, Escena: {_gameSceneName}");

            try
            {
                var result = await _runnerInstance.StartGame(startGameArgs);

                if (result.Ok)
                {
                    Debug.Log("¡Conexión exitosa! Mostrando lobby");
                    ShowLobby();
                }
                else
                {
                    Debug.LogError($"Error al iniciar el juego: {result.ShutdownReason}");
                    UpdateStatusText($"Error de conexión: {result.ShutdownReason}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Excepción al iniciar el juego: {e}");
                UpdateStatusText("Error al conectar");
            }
        }

        private void ShowLobby()
        {
            Debug.Log("Mostrando lobby");
            if (_mainMenuPanel != null) 
            {
                _mainMenuPanel.SetActive(false);
                Debug.Log("Panel del menú principal desactivado");
            }
            
            if (_lobbyPanel != null) 
            {
                _lobbyPanel.SetActive(true);
                Debug.Log("Panel del lobby activado");
            }
            else
            {
                Debug.LogError("¡El panel del lobby no está asignado! Verifica las referencias en el Inspector.");
            }
            
            UpdateLobbyUI();
            
            // Reiniciar el texto de espera y asegurar que el botón está correctamente desactivado inicialmente
            if (_waitingText != null)
                _waitingText.text = "Esperando jugadores...";
                
            if (_startGameButton != null)
                _startGameButton.interactable = false;
                
            Debug.Log($"UI del lobby inicializada - Jugadores conectados: {_players.Count}");
        }

        private void UpdateStatusText(string message)
        {
            Debug.Log($"Estado actualizado: {message}");
            if (_statusText != null) _statusText.text = message;
        }

        private void UpdateLobbyUI()
        {
            if (_playerCountText != null)
                _playerCountText.text = $"Jugadores: {_players.Count}/2";

            bool isHost = _runnerInstance != null && _runnerInstance.IsServer;
            bool canStart = _players.Count == 2 && isHost;

            if (_startGameButton != null)
            {
                _startGameButton.interactable = canStart;
                Debug.Log($"Botón de inicio {(canStart ? "activado" : "desactivado")}");
            }

            if (_waitingText != null)
                _waitingText.text = _players.Count < 2 ? "Esperando jugadores..." : "¡Listo para comenzar!";

            Debug.Log($"UI del lobby actualizada - Jugadores: {_players.Count}, Host: {isHost}");
        }

        public void StartGameFromLobby()
        {
            if (_runnerInstance != null && _runnerInstance.IsServer && _players.Count == 2)
            {
                Debug.Log("Iniciando partida desde el lobby");
                _runnerInstance.LoadScene(_gameSceneName);
            }
            else
            {
                Debug.LogWarning("No se puede iniciar el juego - Condiciones no cumplidas");
            }
        }

        #region Network Callbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Jugador unido - ID: {player}");
            
            if (_players.ContainsKey(player))
            {
                Debug.LogWarning($"Jugador {player} ya existe en la lista");
                return;
            }

            Vector3 spawnPosition = Vector3.zero;
            NetworkObject playerObject = runner.Spawn(_playerDataPrefab, spawnPosition, Quaternion.identity, player);
            
            if (player == runner.LocalPlayer)
            {
                var playerData = playerObject.GetComponent<PlayerData>();
                if (playerData != null)
                {
                    playerData.SetNickName(_localPlayerNickname);
                    Debug.Log($"Nickname asignado al jugador local: {_localPlayerNickname}");
                }
            }

            _players.Add(player, playerObject);
            UpdateLobbyUI();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Jugador abandonó - ID: {player}");
            
            if (_players.TryGetValue(player, out NetworkObject playerObject))
            {
                runner.Despawn(playerObject);
                _players.Remove(player);
                UpdateLobbyUI();
            }
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Conectado al servidor");
            UpdateStatusText("Conectado");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Desconectado del servidor: {reason}");
            UpdateStatusText($"Desconectado: {reason}");
            _players.Clear();
            UpdateLobbyUI();
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"Falló la conexión: {reason}");
            UpdateStatusText($"Error de conexión: {reason}");
        }

        #endregion

        #region Unused Callbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
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