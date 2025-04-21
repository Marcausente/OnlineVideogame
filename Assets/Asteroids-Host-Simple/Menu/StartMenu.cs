using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Asteroids.HostSimple
{
    // A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
    public class StartMenu : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
        [SerializeField] private NetworkPrefabRef _playerDataPrefab;

        [SerializeField] private TMP_InputField _nickName = null;

        // The Placeholder Text is not accessible through the TMP_InputField component so need a direct reference
        [SerializeField] private TextMeshProUGUI _nickNamePlaceholder = null;

        [SerializeField] private TMP_InputField _roomName = null;
        [SerializeField] private string _gameSceneName = null;

        // Nuevos elementos UI para el lobby
        [Header("Lobby UI")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _waitingText;
        [SerializeField] private TextMeshProUGUI _statusText;

        private NetworkRunner _runnerInstance = null;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        private string _localPlayerNickname;

        private void Start()
        {
            if (_startGameButton != null)
                _startGameButton.interactable = false;

            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(false);

            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(true);
        }

        // Attempts to start a new game session 
        public void StartHost()
        {
            Debug.Log("Intentando crear sala como host...");
            SetPlayerData();
            StartGame(GameMode.Host, _roomName.text, _gameSceneName);
        }

        public void StartClient()
        {
            Debug.Log("Intentando unirse como cliente...");
            SetPlayerData();
            StartGame(GameMode.Client, _roomName.text, _gameSceneName);
        }

        private void SetPlayerData()
        {
            _localPlayerNickname = string.IsNullOrWhiteSpace(_nickName.text) ? 
                _nickNamePlaceholder.text : _nickName.text;
            
            Debug.Log($"Nickname local configurado: {_localPlayerNickname}");
        }

        private async void StartGame(GameMode mode, string roomName, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                UpdateStatusText("¡Error! Nombre de sala requerido");
                return;
            }

            UpdateStatusText("Conectando...");

            _runnerInstance = FindObjectOfType<NetworkRunner>();
            if (_runnerInstance == null)
            {
                _runnerInstance = Instantiate(_networkRunnerPrefab);
            }

            _runnerInstance.ProvideInput = true;
            _runnerInstance.AddCallbacks(this);

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                ObjectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>(),
                SceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>()
            };

            Debug.Log($"Iniciando juego en modo: {mode}, Sala: {roomName}");

            try
            {
                var result = await _runnerInstance.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log("Conexión exitosa!");
                    ShowLobby();
                }
                else
                {
                    UpdateStatusText($"Error al conectar: {result.ShutdownReason}");
                    Debug.LogError($"Error al iniciar el juego: {result.ShutdownReason}");
                }
            }
            catch (System.Exception e)
            {
                UpdateStatusText("Error al conectar");
                Debug.LogError($"Excepción al iniciar el juego: {e.Message}");
            }
        }

        private void ShowLobby()
        {
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(false);
            
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(true);

            UpdateLobbyUI();
        }

        private void UpdateStatusText(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
            Debug.Log($"Estado: {message}");
        }

        private void UpdateLobbyUI()
        {
            if (_playerCountText != null)
                _playerCountText.text = $"Jugadores: {_players.Count}/2";

            if (_startGameButton != null)
                _startGameButton.interactable = _players.Count == 2 && _runnerInstance.IsServer;

            if (_waitingText != null)
                _waitingText.text = _players.Count < 2 ? "Esperando jugadores..." : "¡Listo para comenzar!";

            Debug.Log($"Lobby actualizado - Jugadores: {_players.Count}");
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Jugador unido - ID: {player}");
            
            if (_players.ContainsKey(player)) return;
            
            Vector3 spawnPosition = Vector3.zero;
            NetworkObject playerObject = runner.Spawn(_playerDataPrefab, spawnPosition, Quaternion.identity, player);
            
            if (player == runner.LocalPlayer)
            {
                var playerData = playerObject.GetComponent<PlayerData>();
                if (playerData != null)
                {
                    playerData.SetNickName(_localPlayerNickname);
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
            }
            UpdateLobbyUI();
        }

        public void StartGameFromLobby()
        {
            if (_runnerInstance.IsServer && _players.Count == 2)
            {
                Debug.Log("Iniciando partida desde el lobby...");
                _runnerInstance.LoadScene(_gameSceneName);
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

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data)
        {
            // No se necesita implementación para este juego
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            // No se necesita implementación para este juego
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            // No se necesita implementación para este juego
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            // No se necesita implementación para este juego
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            // No se necesita implementación para este juego
        }

        // Implementación de otros métodos de INetworkRunnerCallbacks
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
    }
}