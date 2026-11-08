using GravitosNext.Components;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace GravitosNext.Networking;

public class GlideMessage : INetMessage, ISerializableObject
{
	private NetworkInstanceId netId;

	private Vector3 end;

	private float duration;

	public GlideMessage()
	{
	}

	public GlideMessage(NetworkInstanceId netId, Vector3 end, float duration)
	{
		this.netId = netId;
		this.end = end;
		this.duration = duration;
	}

	public static void StartGlide(GameObject target, Vector3 end, float duration)
	{
		if (NetworkServer.active && (bool)target)
		{
			ObjectGlide.Begin(target, end, duration);
			NetworkIdentity component = target.GetComponent<NetworkIdentity>();
			if ((bool)component)
			{
				new GlideMessage(component.netId, end, duration).Send(NetworkDestination.Clients);
			}
		}
	}

	public void Serialize(NetworkWriter writer)
	{
		writer.Write(netId);
		writer.Write(end);
		writer.Write(duration);
	}

	public void Deserialize(NetworkReader reader)
	{
		netId = reader.ReadNetworkId();
		end = reader.ReadVector3();
		duration = reader.ReadSingle();
	}

	public void OnReceived()
	{
		if (!NetworkServer.active)
		{
			GameObject gameObject = Util.FindNetworkObject(netId);
			if ((bool)gameObject)
			{
				ObjectGlide.Begin(gameObject, end, duration);
			}
		}
	}
}
