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
        [Networked]
        public NetworkString<_32> NickName { get; set; }

        public override void Spawned()
        {
            base.Spawned();
            var count = FindObjectsOfType<PlayerData>().Length;
            if (count > 1)
            {
                Runner.Despawn(Object);
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
                var newNickName = string.IsNullOrWhiteSpace(nickName) ? GetRandomNickName() : nickName;
                NickName = newNickName;
                Debug.Log($"Nickname establecido a: {newNickName}");
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

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                // Aquí puedes agregar lógica adicional si es necesario
            }
        }
    }
}