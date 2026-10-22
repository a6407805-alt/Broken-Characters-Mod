using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SpecialForces
{
    // Network messages (R2API.Networking): build requests from clients, results back, and the shared game state
    // (wave timer etc.) the HUD shows on every machine.
    internal static class SFNet
    {
        public static void Init()
        {
            NetworkingAPI.RegisterMessageType<BuildRequest>();
            NetworkingAPI.RegisterMessageType<BuildResult>();
            NetworkingAPI.RegisterMessageType<StateMessage>();
        }
    }

    public class BuildRequest : INetMessage
    {
        public NetworkInstanceId master;
        public int kind;
        public Vector3 position;
        public float yaw;

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(master);
            writer.Write(kind);
            writer.Write(position);
            writer.Write(yaw);
        }

        public void Deserialize(NetworkReader reader)
        {
            master = reader.ReadNetworkId();
            kind = reader.ReadInt32();
            position = reader.ReadVector3();
            yaw = reader.ReadSingle();
        }

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            SFBuilding.ServerHandle(this);
        }
    }

    public class BuildResult : INetMessage
    {
        public NetworkInstanceId master;
        public string token;
        public bool ok;

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(master);
            writer.Write(token ?? "");
            writer.Write(ok);
        }

        public void Deserialize(NetworkReader reader)
        {
            master = reader.ReadNetworkId();
            token = reader.ReadString();
            ok = reader.ReadBoolean();
        }

        public void OnReceived() => SFBuilding.ClientResult(this);
    }

    public class StateMessage : INetMessage
    {
        public int wave;
        public float nextWave;
        public int nestsAlive;
        public int nestsTotal;

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(wave);
            writer.Write(nextWave);
            writer.Write(nestsAlive);
            writer.Write(nestsTotal);
        }

        public void Deserialize(NetworkReader reader)
        {
            wave = reader.ReadInt32();
            nextWave = reader.ReadSingle();
            nestsAlive = reader.ReadInt32();
            nestsTotal = reader.ReadInt32();
        }

        public void OnReceived()
        {
            SFHud.Wave = wave;
            SFHud.NextWave = nextWave;
            SFHud.NestsAlive = nestsAlive;
            SFHud.NestsTotal = nestsTotal;
            SFHud.StateTime = Time.unscaledTime;
        }

        public static void Send(int wave, float nextWave, int nestsAlive, int nestsTotal)
        {
            new StateMessage { wave = wave, nextWave = nextWave, nestsAlive = nestsAlive, nestsTotal = nestsTotal }
                .Send(NetworkDestination.Clients);
        }
    }
}
