using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Asteroids.HostSimple
{
    // A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
    public class StartMenu : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
        [SerializeField] private PlayerData _playerDataPrefab = null;

        [SerializeField] private TMP_InputField _nickName = null;

        // The Placeholder Text is not accessible through the TMP_InputField component so need a direct reference
        [SerializeField] private TextMeshProUGUI _nickNamePlaceholder = null;

        [SerializeField] private TMP_InputField _roomName = null;
        [SerializeField] private string _gameSceneName = null;

        // Nuevos elementos UI para el lobby
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _waitingText;

        private NetworkRunner _runnerInstance = null;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();

        private void Start()
        {
            // Desactivar el botón de inicio al principio
            if (_startGameButton != null)
                _startGameButton.interactable = false;
        }

        // Attempts to start a new game session 
        public void StartHost()
        {
            SetPlayerData();
            StartGame(GameMode.AutoHostOrClient, _roomName.text, _gameSceneName);
        }

        public void StartClient()
        {
            SetPlayerData();
            StartGame(GameMode.Client, _roomName.text, _gameSceneName);
        }

        private void SetPlayerData()
        {
            var playerData = FindObjectOfType<PlayerData>();
            if (playerData == null)
            {
                playerData = Instantiate(_playerDataPrefab);
            }

            if (string.IsNullOrWhiteSpace(_nickName.text))
            {
                playerData.SetNickName(_nickNamePlaceholder.text);
            }
            else
            {
                playerData.SetNickName(_nickName.text);
            }
        }

        private async void StartGame(GameMode mode, string roomName, string sceneName)
        {
            _runnerInstance = FindObjectOfType<NetworkRunner>();
            if (_runnerInstance == null)
            {
                _runnerInstance = Instantiate(_networkRunnerPrefab);
            }

            // Let the Fusion Runner know that we will be providing user input
            _runnerInstance.ProvideInput = true;
            _runnerInstance.AddCallbacks(this);

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                ObjectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>(),
                SceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>()
            };

            // GameMode.Host = Start a session with a specific name
            // GameMode.Client = Join a session with a specific name
            await _runnerInstance.StartGame(startGameArgs);

            // Mostrar el panel de lobby
            if (_lobbyPanel != null)
                _lobbyPanel.SetActive(true);
        }

        private void UpdateLobbyUI()
        {
            if (_playerCountText != null)
                _playerCountText.text = $"Jugadores: {_players.Count}/2";

            if (_startGameButton != null)
                _startGameButton.interactable = _players.Count == 2 && _runnerInstance.IsServer;

            if (_waitingText != null)
                _waitingText.text = _players.Count < 2 ? "Esperando jugadores..." : "¡Listo para comenzar!";
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (_players.ContainsKey(player)) return;
            
            // Crear datos del jugador
            var playerData = runner.Spawn(_playerDataPrefab, Vector3.zero, Quaternion.identity, player);
            _players.Add(player, playerData);
            
            UpdateLobbyUI();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
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
                _runnerInstance.LoadScene(_gameSceneName);
            }
        }

        // Implementación de otros métodos de INetworkRunnerCallbacks
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}