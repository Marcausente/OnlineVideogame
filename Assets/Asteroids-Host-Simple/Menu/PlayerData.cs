using System;
using UnityEngine;
using Fusion;
using Random = UnityEngine.Random;

namespace Asteroids.HostSimple
{
    // This class functions as an Instance Singleton (no-static references)
    // and holds information about the local player in-between scene loads.
    public class PlayerData : NetworkBehaviour
    {
        [Networked(OnChanged = nameof(OnNickNameChanged))]
        public NetworkString<_32> NickName { get; set; }

        private void Start()
        {
            var count = FindObjectsOfType<PlayerData>().Length;
            if (count > 1)
            {
                Destroy(gameObject);
                return;
            }

            if (Object == null || Object.HasStateAuthority)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void SetNickName(string nickName)
        {
            if (Object == null || Object.HasStateAuthority)
            {
                NickName = string.IsNullOrWhiteSpace(nickName) ? GetRandomNickName() : nickName;
            }
        }

        public string GetNickName()
        {
            return NickName.ToString();
        }

        public static string GetRandomNickName()
        {
            var rngPlayerNumber = Random.Range(0, 9999);
            return $"Player {rngPlayerNumber:0000}";
        }

        public static void OnNickNameChanged(Changed<PlayerData> changed)
        {
            Debug.Log($"Nickname cambiado a: {changed.Behaviour.NickName}");
        }
    }
}