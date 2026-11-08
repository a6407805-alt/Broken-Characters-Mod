using EntityStates;
using NextMech.Components;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

// One shot per state; the skill re-fires while its button is held (mustKeyPress = false).
public abstract class GunShot : BaseSkillState
{
	protected abstract float Interval { get; }
	protected abstract float Damage { get; }
	protected virtual float Spread => 1f;
	protected virtual float Range => 200f;
	protected virtual float Force => 150f;
	protected virtual float Proc => 1f;
	protected virtual uint Pellets => 1u;
	protected virtual float Radius => 0.25f;
	protected virtual float Recoil => 0.6f;
	internal virtual CustomAudio.Clip Sound => CustomAudio.Clip.Rifle;

	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = Interval / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		if (!Arsenal.IsLeft(this)) Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 1.2f);
		Shoot();
	}

	protected virtual bool CanShoot() => true;

	protected void Shoot()
	{
		if (!CanShoot()) return;
		string muzzle = Arsenal.Muzzle(this);
		CustomAudio.PlayNear(Sound, transform.position, 70f);
		if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, muzzle, false);
		if (!isAuthority) return;
		Ray aim = GetAimRay();
		AddRecoil(-Recoil, -Recoil * 1.5f, -Recoil * 0.4f, Recoil * 0.4f);
		new BulletAttack
		{
			owner = gameObject,
			weapon = gameObject,
			origin = aim.origin,
			aimVector = aim.direction,
			minSpread = 0f,
			maxSpread = Spread,
			bulletCount = Pellets,
			damage = damageStat * Damage,
			procCoefficient = Proc,
			force = Force,
			falloffModel = BulletAttack.FalloffModel.DefaultBullet,
			maxDistance = Range,
			radius = Radius,
			isCrit = RollCrit(),
			muzzleName = muzzle,
			tracerEffectPrefab = HPAssets.Tracer,
			hitEffectPrefab = HPAssets.HitSpark,
			smartCollision = true,
		}.Fire();
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

public class FireAssaultRifle : GunShot
{
	protected override float Interval => 0.11f;
	protected override float Damage => NextConfig.AssaultRifleDamage.Value;
	protected override float Spread => 1.2f;
}

// Energy weapon: no ammo, every shot spends EN. Can't fire while redlined.
public class FirePulseGun : GunShot
{
	protected override float Interval => 0.16f;
	protected override float Damage => NextConfig.PulseGunDamage.Value;
	protected override float Spread => 0.8f;
	protected override float Recoil => 0.35f;
	protected override float Proc => 0.8f;

	public override void OnEnter()
	{
		HPStatus st = HPStatus.Of(characterBody);
		if (st && st.redline)
		{
			base.OnEnter();
			return;
		}
		if (st) st.Spend(NextConfig.PulseGunEn.Value);
		base.OnEnter();
	}

	protected override bool CanShoot()
	{
		HPStatus st = HPStatus.Of(characterBody);
		return !st || !st.redline;
	}
}

// Bazooka: slow heavy shell, big stagger and splash.
public class FireBazooka : BaseSkillState
{
	public static GameObject Shell;
	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = 1.3f / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		if (!Arsenal.IsLeft(this)) Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 0.6f);
		CustomAudio.PlayNear(CustomAudio.Clip.Missile, transform.position, 80f);
		CustomAudio.PlayNear(CustomAudio.Clip.Stomp, transform.position, 60f);
		if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, Arsenal.Muzzle(this), false);
		if (isAuthority && Shell)
		{
			Ray aim = GetAimRay();
			AddRecoil(-3f, -4f, -0.8f, 0.8f);
			ProjectileManager.instance.FireProjectile(Shell, aim.origin + aim.direction * 2f, Util.QuaternionSafeLookRotation(aim.direction), gameObject, damageStat * NextConfig.BazookaDamage.Value, 2000f, RollCrit());
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

public class FireSmg : GunShot
{
	protected override float Interval => 0.065f;
	protected override float Damage => NextConfig.SmgDamage.Value;
	protected override float Spread => 2.4f;
	protected override float Range => 90f;
	protected override float Proc => 0.6f;
	protected override float Recoil => 0.3f;
}

public class FireHandgun : GunShot
{
	protected override float Interval => 0.42f;
	protected override float Damage => NextConfig.HandgunDamage.Value;
	protected override float Spread => 0.6f;
	protected override float Force => 600f;
	protected override float Recoil => 1.4f;
}

// Spins up while held: slow at first, then a wall of lead. Slows you while firing (HPMain).
public class FireGatling : GunShot
{
	private float spin;
	protected override float Interval => Mathf.Lerp(0.2f, 0.05f, spin);
	protected override float Damage => NextConfig.GatlingDamage.Value;
	protected override float Spread => 2f;
	protected override float Proc => 0.5f;
	protected override float Recoil => 0.25f;

	public override void OnEnter()
	{
		HPStatus st = HPStatus.Of(characterBody);
		bool left = st && (outer && outer.customName == "LeftArm");
		if (st)
		{
			float last = left ? st.gatlingLastL : st.gatlingLastR;
			float s = left ? st.gatlingSpinL : st.gatlingSpinR;
			s = Time.time - last > 0.4f ? 0f : Mathf.Min(1f, s + 0.12f);
			st.gatlingSlowUntil = Time.time + 0.3f;
			if (left) { st.gatlingSpinL = s; st.gatlingLastL = Time.time; }
			else { st.gatlingSpinR = s; st.gatlingLastR = Time.time; }
			spin = s;
		}
		base.OnEnter();
	}
}

// Pile bunker: short wind-up, lunge, one enormous hit. Double damage on an ACS-overloaded target.
public class PileBunker : BaseSkillState
{
	private const float Windup = 0.3f, HitTime = 0.42f, Duration = 0.8f;
	private Vector3 forward;
	private bool hit;

	public override void OnEnter()
	{
		base.OnEnter();
		forward = Move.Flat(GetAimRay().direction, transform.forward);
		Move.Face(characterDirection, forward);
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorCharge, transform.position, 50f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && characterMotor && fixedAge >= Windup && fixedAge < HitTime)
		{
			Vector3 v = forward * 30f;
			characterMotor.velocity = new Vector3(v.x, Mathf.Max(characterMotor.velocity.y, 0f), v.z);
		}
		if (!hit && fixedAge >= HitTime)
		{
			hit = true;
			CustomAudio.PlayNear(CustomAudio.Clip.Kick, transform.position, 80f);
			CustomAudio.PlayNear(CustomAudio.Clip.Stomp, transform.position, 80f);
			if (NetworkServer.active) Hit();
			if (isAuthority && characterMotor) characterMotor.velocity *= 0.2f;
		}
		if (isAuthority && fixedAge >= Duration) outer.SetNextStateToMain();
	}

	private void Hit()
	{
		float reach = 4.5f;
		Vector3 center = characterBody.corePosition + forward * reach * 0.6f;
		bool crit = RollCrit();
		CharacterBody best = null;
		float bestD = float.MaxValue;
		foreach (CharacterBody b in HPUtil.FindEnemies(teamComponent.teamIndex, center, reach))
		{
			float d = (b.corePosition - characterBody.corePosition).sqrMagnitude;
			if (d < bestD) { bestD = d; best = b; }
		}
		if (!best) return;
		float dmg = damageStat * NextConfig.PileBunkerDamage.Value;
		if (HPContent.OverloadBuff && best.HasBuff(HPContent.OverloadBuff)) dmg *= 2f;
		HPUtil.DealDamage(gameObject, best.healthComponent, dmg, crit, best.corePosition, forward * 6000f + Vector3.up * 1200f, 1f);
		HPUtil.SpawnEffect(HPAssets.BigExplosion ? HPAssets.BigExplosion : HPAssets.Slam, best.corePosition, 2.5f);
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
}

// Grenade cannon: heavy shell, huge blast. Anything lighter than a Heavy frame braces (stops) to fire it.
public class FireGrenadeCannon : BaseSkillState
{
	public static GameObject Shell;
	private const float Brace = 0.35f, Duration = 0.8f;
	private bool fired, brace;

	public override void OnEnter()
	{
		base.OnEnter();
		brace = Garage.Frame(characterBody) != FramePart.Heavy;
		characterBody.SetAimTimer(2f);
		CustomAudio.PlayNear(CustomAudio.Clip.Stomp, transform.position, 60f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && characterMotor && brace && fixedAge < Brace + 0.15f)
		{
			characterMotor.velocity = new Vector3(0f, Mathf.Min(characterMotor.velocity.y, 0f), 0f);
		}
		if (!fired && fixedAge >= (brace ? Brace : 0.1f))
		{
			fired = true;
			CustomAudio.PlayNear(CustomAudio.Clip.ArmorBlast, transform.position, 90f);
			string muzzle = Arsenal.Muzzle(this);
			if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, muzzle, false);
			if (isAuthority && Shell)
			{
				Ray aim = GetAimRay();
				AddRecoil(-4f, -6f, -1f, 1f);
				ProjectileManager.instance.FireProjectile(Shell, aim.origin + aim.direction * 2f, Util.QuaternionSafeLookRotation(aim.direction), gameObject, damageStat * NextConfig.GrenadeDamage.Value, 2500f, RollCrit());
				if (characterMotor && !brace) characterMotor.velocity -= aim.direction * 6f;
			}
		}
		if (isAuthority && fixedAge >= Duration) outer.SetNextStateToMain();
	}

	public static void CreateShell()
	{
		Shell = MakeShell("HardpointNextGrenadeShell", NextConfig.GrenadeRadius.Value, 90f);
	}

	public static GameObject MakeShell(string name, float radius, float speed)
	{
		GameObject src = LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/CommandoGrenadeProjectile");
		if (!src)
		{
			HPPlugin.Log.LogWarning("Commando grenade not found: " + name + " disabled.");
			return null;
		}
		GameObject shell = R2API.PrefabAPI.InstantiateClone(src, name, true);
		ProjectileImpactExplosion pie = shell.GetComponent<ProjectileImpactExplosion>();
		if (pie)
		{
			pie.blastRadius = radius;
			pie.lifetime = 4f;
			pie.destroyOnEnemy = true;
			pie.destroyOnWorld = true;
			pie.impactOnWorld = true;
			if (HPAssets.BigExplosion) pie.explosionEffect = HPAssets.BigExplosion;
		}
		ProjectileSimple ps = shell.GetComponent<ProjectileSimple>();
		if (ps) ps.desiredForwardSpeed = speed;
		Rigidbody rb = shell.GetComponent<Rigidbody>();
		if (rb) rb.useGravity = true;
		R2API.ContentAddition.AddProjectile(shell);
		return shell;
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
}

// Laser cannon: charge, then a thick piercing beam. Costs EN.
public class ChargeLaserCannon : BaseSkillState
{
	private const float Charge = 0.8f, Duration = 1.15f;
	private bool fired;

	public override void OnEnter()
	{
		base.OnEnter();
		characterBody.SetAimTimer(3f);
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorCharge, transform.position, 60f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!fired && fixedAge >= Charge)
		{
			fired = true;
			HPStatus st = HPStatus.Of(characterBody);
			if (st) st.Spend(NextConfig.LaserEnCost.Value);
			CustomAudio.PlayNear(CustomAudio.Clip.ArmorBlast, transform.position, 100f);
			string muzzle = Arsenal.Muzzle(this);
			if (HPAssets.MuzzleFlash) EffectManager.SimpleMuzzleFlash(HPAssets.MuzzleFlash, gameObject, muzzle, false);
			if (isAuthority)
			{
				Ray aim = GetAimRay();
				AddRecoil(-2f, -3f, -0.5f, 0.5f);
				new BulletAttack
				{
					owner = gameObject,
					weapon = gameObject,
					origin = aim.origin,
					aimVector = aim.direction,
					maxSpread = 0f,
					bulletCount = 1u,
					damage = damageStat * NextConfig.LaserDamage.Value,
					procCoefficient = 1f,
					force = 3000f,
					falloffModel = BulletAttack.FalloffModel.None,
					maxDistance = 400f,
					radius = 1.6f,
					isCrit = RollCrit(),
					muzzleName = muzzle,
					tracerEffectPrefab = HPAssets.Tracer,
					hitEffectPrefab = HPAssets.Spark ? HPAssets.Spark : HPAssets.HitSpark,
					stopperMask = LayerIndex.world.mask,
					smartCollision = true,
				}.Fire();
			}
		}
		if (isAuthority && fixedAge >= Duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
}
