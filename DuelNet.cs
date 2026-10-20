using System;
using System.Collections.Generic;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    /// <summary>
    /// Multiplayer glue. The duel runs on the machine of whoever is dueling; anything that changes the shared
    /// game (damage to enemies, gold, healing, partner commands) goes to the host as a request, and the host
    /// applies it. The host also sends each Duelist their card deck and which partner they have out.
    /// On the host itself every call here just happens directly.
    /// </summary>
    public static class DuelNet
    {
        public static void Register()
        {
            NetworkingAPI.RegisterMessageType<DuelOpMessage>();
            NetworkingAPI.RegisterMessageType<DuelDeckMessage>();
            NetworkingAPI.RegisterMessageType<DuelTellMessage>();
            NetworkingAPI.RegisterMessageType<PartnerStateMessage>();
        }

        public static NetworkInstanceId Id(Component c)
        {
            NetworkIdentity ni = c ? c.GetComponent<NetworkIdentity>() : null;
            return ni ? ni.netId : NetworkInstanceId.Invalid;
        }

        public static GameObject Find(NetworkInstanceId id) => id == NetworkInstanceId.Invalid ? null : Util.FindNetworkObject(id);

        private static void Send(DuelOp op, Component target, Component source, float amount = 0f, bool crit = false, float extra = 0f, int arg = 0,
                                 Vector3 v1 = default(Vector3), Vector3 v2 = default(Vector3))
        {
            try
            {
                new DuelOpMessage
                {
                    Op = op, Target = Id(target), Source = Id(source), Amount = amount, Crit = crit, Extra = extra, Arg = arg, V1 = v1, V2 = v2
                }.Send(NetworkDestination.Server);
            }
            catch (Exception e) { DuelistPlugin.Log.LogWarning($"Duel network message {op} failed: {e.Message}"); }
        }

        // ------------------------------------------------------------------ requests (direct on the host)

        /// <summary>Damage an enemy; if it dies, the attacker also gets goldBonus x its gold value.</summary>
        public static void DamageEnemy(CharacterBody attacker, HealthComponent target, float dmg, bool crit, float goldBonus)
        {
            if (!target) return;
            if (NetworkServer.active) DoDamage(attacker ? attacker.gameObject : null, target, dmg, crit, goldBonus);
            else Send(DuelOp.DamageEnemy, target, attacker, dmg, crit, goldBonus);
        }

        public static void HurtBody(CharacterBody victim, float dmg, GameObject attacker)
        {
            if (!victim || !victim.healthComponent) return;
            if (NetworkServer.active) DoDamage(attacker, victim.healthComponent, dmg, false, 0f);
            else Send(DuelOp.HurtBody, victim, attacker ? attacker.transform : null, dmg);
        }

        public static void HealFraction(CharacterBody body, float fraction)
        {
            if (!body || !body.healthComponent) return;
            if (NetworkServer.active) DuelRegen.Allowed(() => body.healthComponent.HealFraction(fraction, default(ProcChainMask)));
            else Send(DuelOp.HealFraction, body, null, fraction);
        }

        public static void Heal(HealthComponent h, float amount)
        {
            if (!h) return;
            if (NetworkServer.active) DuelRegen.Allowed(() => h.Heal(amount, default(ProcChainMask), true));
            else Send(DuelOp.Heal, h, null, amount);
        }

        public static void GiveMoney(CharacterMaster m, uint amount)
        {
            if (!m || amount == 0) return;
            if (NetworkServer.active) m.GiveMoney(amount);
            else Send(DuelOp.GiveMoney, m, null, amount);
        }

        public static void TakeMoney(CharacterMaster m, uint amount)
        {
            if (!m || amount == 0) return;
            if (NetworkServer.active) m.money -= (uint)Mathf.Min((int)m.money, (int)amount);
            else Send(DuelOp.TakeMoney, m, null, amount);
        }

        public static void Invincible(CharacterBody b, float seconds)
        {
            if (!b) return;
            if (NetworkServer.active) b.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, seconds);
            else Send(DuelOp.Invincible, b, null, seconds);
        }

        public static void UsePartnerAbility(CharacterBody owner, PartnerAbility ability, Ray aim)
        {
            if (NetworkServer.active) RunAbility(owner, ability, aim);
            else Send(DuelOp.UseAbility, owner, null, 0f, false, 0f, (int)ability, aim.origin, aim.direction);
        }

        public static void PartnerFocus(CharacterBody owner, HurtBox target)
        {
            CharacterBody tb = target && target.healthComponent ? target.healthComponent.body : null;
            if (NetworkServer.active) PartnerManager.Of(owner)?.SetFocus(target);
            else Send(DuelOp.PartnerFocus, owner, tb);
        }

        /// <summary>A chat line for one player: shown on their screen only.</summary>
        public static void Tell(CharacterBody owner, string text)
        {
            if (!owner || owner.hasEffectiveAuthority) { try { Chat.AddMessage(text); } catch { } return; }
            if (!NetworkServer.active) return;
            try { new DuelTellMessage { Body = Id(owner), Text = text }.Send(NetworkDestination.Clients); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Tell failed: " + e.Message); }
        }

        /// <summary>Host → everyone: this Duelist's deck (only the owner uses it).</summary>
        public static void SyncDeck(DuelistDeck deck)
        {
            if (!NetworkServer.active || !deck) return;
            try
            {
                DuelDeckMessage m = new DuelDeckMessage { Master = Id(deck) };
                foreach (KeyValuePair<ItemIndex, int> kv in deck.ItemCards) m.Items.Add(((int)kv.Key, kv.Value));
                foreach (KeyValuePair<EquipmentIndex, int> kv in deck.EquipCards) m.Equips.Add(((int)kv.Key, kv.Value));
                foreach (KeyValuePair<BodyIndex, int> kv in deck.DroneCards) m.Drones.Add(((int)kv.Key, kv.Value));
                m.Send(NetworkDestination.Clients);
            }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Deck sync failed: " + e.Message); }
        }

        public static void RequestDeck(CharacterMaster m)
        {
            if (!m || NetworkServer.active) return;
            Send(DuelOp.DeckRequest, m, null);
        }

        public static void SendPartnerState(PartnerManager pm, PartnerKind kind, BodyIndex drone)
        {
            if (!NetworkServer.active || !pm) return;
            try { new PartnerStateMessage { Body = Id(pm), Kind = (int)kind, Drone = (int)drone }.Send(NetworkDestination.Clients); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Partner sync failed: " + e.Message); }
        }

        // ------------------------------------------------------------------ host side

        private static void DoDamage(GameObject attacker, HealthComponent h, float dmg, bool crit, float goldBonus)
        {
            if (!h || !h.alive || dmg <= 0f) return;
            DeathRewards rw = h.GetComponent<DeathRewards>();
            uint worth = rw ? rw.goldReward : 0u;
            h.TakeDamage(new DamageInfo
            {
                attacker = attacker,
                inflictor = attacker,
                damage = dmg,
                crit = crit,
                position = h.body ? h.body.corePosition : h.transform.position,
                force = Vector3.zero,
                procCoefficient = 0f,
                damageColorIndex = DamageColorIndex.Default,
                procChainMask = default(ProcChainMask)
            });
            if (!h.alive && goldBonus > 0f && attacker)
            {
                CharacterBody ab = attacker.GetComponent<CharacterBody>();
                uint bonus = (uint)Mathf.RoundToInt(worth * goldBonus);
                if (ab && ab.master && bonus > 0) ab.master.GiveMoney(bonus);
            }
        }

        private static void RunAbility(CharacterBody owner, PartnerAbility ability, Ray aim)
        {
            PartnerManager pm = PartnerManager.Of(owner);
            CharacterBody pb = pm ? pm.PartnerBody : null;
            if (!pb) return;
            try { PartnerFx.Run(ability, owner, pm, pb, aim); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning($"Partner ability {ability} failed: {e.Message}"); }
        }

        internal static void OnServer(DuelOpMessage m)
        {
            GameObject target = Find(m.Target);
            GameObject source = Find(m.Source);
            switch (m.Op)
            {
                case DuelOp.DamageEnemy:
                    DoDamage(source, target ? target.GetComponent<HealthComponent>() : null, m.Amount, m.Crit, m.Extra);
                    break;
                case DuelOp.HurtBody:
                    DoDamage(source, target ? target.GetComponent<HealthComponent>() : null, m.Amount, false, 0f);
                    break;
                case DuelOp.HealFraction:
                    if (target && target.GetComponent<HealthComponent>()) DuelRegen.Allowed(() => target.GetComponent<HealthComponent>().HealFraction(m.Amount, default(ProcChainMask)));
                    break;
                case DuelOp.Heal:
                    if (target && target.GetComponent<HealthComponent>()) DuelRegen.Allowed(() => target.GetComponent<HealthComponent>().Heal(m.Amount, default(ProcChainMask), true));
                    break;
                case DuelOp.GiveMoney:
                    if (target && target.GetComponent<CharacterMaster>()) target.GetComponent<CharacterMaster>().GiveMoney((uint)m.Amount);
                    break;
                case DuelOp.TakeMoney:
                {
                    CharacterMaster cm = target ? target.GetComponent<CharacterMaster>() : null;
                    if (cm) cm.money -= (uint)Mathf.Min((int)cm.money, (int)m.Amount);
                    break;
                }
                case DuelOp.Invincible:
                    if (target && target.GetComponent<CharacterBody>()) target.GetComponent<CharacterBody>().AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, m.Amount);
                    break;
                case DuelOp.UseAbility:
                    RunAbility(target ? target.GetComponent<CharacterBody>() : null, (PartnerAbility)m.Arg, new Ray(m.V1, m.V2));
                    break;
                case DuelOp.PartnerFocus:
                {
                    PartnerManager pm = target ? target.GetComponent<PartnerManager>() : null;
                    CharacterBody tb = source ? source.GetComponent<CharacterBody>() : null;
                    if (pm) pm.SetFocus(tb ? tb.mainHurtBox : null);
                    break;
                }
                case DuelOp.DeckRequest:
                    if (target && target.GetComponent<DuelistDeck>()) SyncDeck(target.GetComponent<DuelistDeck>());
                    break;
            }
        }
    }

    public enum DuelOp : byte
    {
        DamageEnemy, HurtBody, HealFraction, Heal, GiveMoney, TakeMoney, Invincible, UseAbility, PartnerFocus, DeckRequest
    }

    /// <summary>Client → host: one request.</summary>
    public class DuelOpMessage : INetMessage
    {
        public DuelOp Op;
        public NetworkInstanceId Target, Source;
        public float Amount, Extra;
        public bool Crit;
        public int Arg;
        public Vector3 V1, V2;

        public void Serialize(NetworkWriter w)
        {
            w.Write((byte)Op);
            w.Write(Target);
            w.Write(Source);
            w.Write(Amount);
            w.Write(Extra);
            w.Write(Crit);
            w.Write(Arg);
            w.Write(V1);
            w.Write(V2);
        }

        public void Deserialize(NetworkReader r)
        {
            Op = (DuelOp)r.ReadByte();
            Target = r.ReadNetworkId();
            Source = r.ReadNetworkId();
            Amount = r.ReadSingle();
            Extra = r.ReadSingle();
            Crit = r.ReadBoolean();
            Arg = r.ReadInt32();
            V1 = r.ReadVector3();
            V2 = r.ReadVector3();
        }

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            try { DuelNet.OnServer(this); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning($"Duel request {Op} failed on host: {e.Message}"); }
        }
    }

    /// <summary>Host → clients: a Duelist's card deck.</summary>
    public class DuelDeckMessage : INetMessage
    {
        public NetworkInstanceId Master;
        public readonly List<(int, int)> Items = new List<(int, int)>();
        public readonly List<(int, int)> Equips = new List<(int, int)>();
        public readonly List<(int, int)> Drones = new List<(int, int)>();

        private static void WriteList(NetworkWriter w, List<(int, int)> list)
        {
            w.Write(list.Count);
            foreach ((int a, int b) in list) { w.Write(a); w.Write(b); }
        }

        private static void ReadList(NetworkReader r, List<(int, int)> list)
        {
            list.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) list.Add((r.ReadInt32(), r.ReadInt32()));
        }

        public void Serialize(NetworkWriter w) { w.Write(Master); WriteList(w, Items); WriteList(w, Equips); WriteList(w, Drones); }
        public void Deserialize(NetworkReader r) { Master = r.ReadNetworkId(); ReadList(r, Items); ReadList(r, Equips); ReadList(r, Drones); }

        public void OnReceived()
        {
            if (NetworkServer.active) return;      // the host already has the real deck
            GameObject go = DuelNet.Find(Master);
            DuelistDeck deck = go ? go.GetComponent<DuelistDeck>() : null;
            if (deck) deck.ApplySync(Items, Equips, Drones);
        }
    }

    /// <summary>Host → clients: a chat line meant for one player.</summary>
    public class DuelTellMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public string Text;

        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Text ?? ""); }
        public void Deserialize(NetworkReader r) { Body = r.ReadNetworkId(); Text = r.ReadString(); }

        public void OnReceived()
        {
            GameObject go = DuelNet.Find(Body);
            CharacterBody b = go ? go.GetComponent<CharacterBody>() : null;
            if (b && b.hasEffectiveAuthority && !NetworkServer.active) { try { Chat.AddMessage(Text); } catch { } }
        }
    }

    /// <summary>Host → clients: which partner a Duelist has out (so their M1/M2/R switch over).</summary>
    public class PartnerStateMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public int Kind, Drone;

        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Kind); w.Write(Drone); }
        public void Deserialize(NetworkReader r) { Body = r.ReadNetworkId(); Kind = r.ReadInt32(); Drone = r.ReadInt32(); }

        public void OnReceived()
        {
            if (NetworkServer.active) return;
            GameObject go = DuelNet.Find(Body);
            PartnerManager pm = go ? go.GetComponent<PartnerManager>() : null;
            if (pm) pm.ApplyRemoteState((PartnerKind)Kind, (BodyIndex)Drone);
        }
    }
}
