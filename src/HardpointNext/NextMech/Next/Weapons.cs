using EntityStates;
using NextMech.Components;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace NextMech.SkillStates;

// One shot per state; the skill re-fires while its button is held (mustKeyPress = false).
// Every gun has its own "feel": tracer, flash, impact, camera kick, spread bloom, knockback on enemies and on you.
public abstract class GunShot : BaseSkillState
{
	protected abstract float Interval { get; }
	protected abstract float Damage { get; }
	protected virtual float Spread => 0.6f;          // base spread (degrees)
	protected virtual float Bloom => 0f;             // spread added per shot while firing
	protected virtual float BloomMax => 0f;
	protected virtual float Range => 200f;
	protected virtual float Force => 150f;           // pushes enemies
	protected virtual float SelfPush => 0f;          // pushes you back (m/s), more in the air
	protected virtual float Proc => 1f;
	protected virtual uint Pellets => 1u;
	protected virtual float Radius => 0.25f;
	protected virtual float Recoil => 0.6f;          // camera kick
	protected virtual bool Pierce => false;
	protected virtual BulletAttack.FalloffModel Falloff => BulletAttack.FalloffModel.DefaultBullet;
	protected virtual GameObject TracerFx => HPAssets.Tracer;
	protected virtual GameObject FlashFx => HPAssets.MuzzleFlash;
	protected virtual GameObject HitFx => HPAssets.HitSpark;
	protected virtual float AnimRate => 1.2f;
	internal virtual CustomAudio.Clip Sound => CustomAudio.Clip.Rifle;
	internal virtual CustomAudio.Clip Sound2 => (CustomAudio.Clip)(-1);

	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = Interval / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		if (!Arsenal.IsLeft(this)) Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", AnimRate);
		Shoot();
	}

	protected virtual bool CanShoot() => true;

	protected virtual float DamageScale => 1f;

	protected void Shoot()
	{
		if (!CanShoot()) return;
		string muzzle = Arsenal.Muzzle(this);
		CustomAudio.PlayNear(Sound, transform.position, 70f);
		if ((int)Sound2 >= 0) CustomAudio.PlayNear(Sound2, transform.position, 60f);
		if (FlashFx) EffectManager.SimpleMuzzleFlash(FlashFx, gameObject, muzzle, false);
		if (!isAuthority) return;

		// spread bloom per arm, recovers when you stop firing
		HPStatus st = HPStatus.Of(characterBody);
		bool left = Arsenal.IsLeft(this);
		float bloom = 0f;
		if (st && Bloom > 0f)
		{
			bloom = left ? st.bloomL : st.bloomR;
			if (left) { st.bloomL = Mathf.Min(BloomMax, st.bloomL + Bloom); st.bloomShotL = Time.time; }
			else { st.bloomR = Mathf.Min(BloomMax, st.bloomR + Bloom); st.bloomShotR = Time.time; }
		}

		Ray aim = GetAimRay();
		AddRecoil(-Recoil, -Recoil * 1.5f, -Recoil * 0.4f, Recoil * 0.4f);
		var bullet = new BulletAttack
		{
			owner = gameObject,
			weapon = gameObject,
			origin = aim.origin,
			aimVector = aim.direction,
			minSpread = 0f,
			maxSpread = Spread + bloom,
			bulletCount = Pellets,
			damage = damageStat * Damage * DamageScale,
			procCoefficient = Proc,
			force = Force,
			falloffModel = Falloff,
			maxDistance = Range,
			radius = Radius,
			isCrit = RollCrit(),
			muzzleName = muzzle,
			tracerEffectPrefab = TracerFx,
			hitEffectPrefab = HitFx,
			smartCollision = true,
		};
		if (Pierce) bullet.stopperMask = LayerIndex.world.mask;
		bullet.Fire();

		if (SelfPush > 0f && characterMotor)
		{
			Vector3 back = -aim.direction;
			if (characterMotor.isGrounded) back.y = 0f;
			characterMotor.velocity += back.normalized * SelfPush * (characterMotor.isGrounded ? 0.4f : 1f);
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

// Assault rifle: steady full-auto, spread opens up the longer you hold it.
public class FireAssaultRifle : GunShot
{
	protected override float Interval => 0.11f;
	protected override float Damage => NextConfig.AssaultRifleDamage.Value;
	protected override float Spread => 0.4f;
	protected override float Bloom => 0.22f;
	protected override float BloomMax => 3f;
	protected override float Recoil => 0.55f;
	protected override float Force => 250f;
}

// SMG: a hose. Tiny kick, wild bloom, short range, falls off fast.
public class FireSmg : GunShot
{
	protected override float Interval => 0.06f;
	protected override float Damage => NextConfig.SmgDamage.Value;
	protected override float Spread => 1.2f;
	protected override float Bloom => 0.35f;
	protected override float BloomMax => 6f;
	protected override float Range => 70f;
	protected override float Proc => 0.6f;
	protected override float Recoil => 0.2f;
	protected override float Force => 60f;
	protected override BulletAttack.FalloffModel Falloff => BulletAttack.FalloffModel.Buckshot;
	protected override GameObject TracerFx => GunFx.Or(GunFx.TracerNails, HPAssets.Tracer);
	protected override GameObject HitFx => GunFx.Or(GunFx.HitBarrage, HPAssets.HitSpark);
	protected override float AnimRate => 2f;
}

// Shotgun: one huge boom. Big camera kick, blows enemies back and shoves you back in the air.
public class FireShotgun : GunShot
{
	protected override float Interval => NextConfig.ShotgunInterval.Value;
	protected override float Damage => NextConfig.ShotgunDamage.Value;
	protected override uint Pellets => (uint)Mathf.Max(1, NextConfig.ShotgunPellets.Value);
	protected override float Spread => 6.5f;
	protected override float Range => 60f;
	protected override float Radius => 0.45f;
	protected override float Proc => 0.7f;
	protected override float Recoil => 3.2f;
	protected override float Force => 900f;
	protected override float SelfPush => 7f;
	protected override BulletAttack.FalloffModel Falloff => BulletAttack.FalloffModel.Buckshot;
	protected override GameObject TracerFx => GunFx.Or(GunFx.TracerShotgun, HPAssets.Tracer);
	protected override GameObject FlashFx => GunFx.Or(GunFx.FlashSmoke, HPAssets.MuzzleFlash);
	protected override GameObject HitFx => GunFx.Or(GunFx.HitShotgun, HPAssets.HitSpark);
	protected override float AnimRate => 0.9f;
	internal override CustomAudio.Clip Sound2 => CustomAudio.Clip.Stomp;
}

// Linear rifle: hold to charge (up to 2.5x), release to fire a rail shot that pierces everything.
public class FireLinear : BaseSkillState
{
	private const float FullCharge = 0.9f;
	private float charge;
	private bool fired;
	private float after;

	public override void OnEnter()
	{
		base.OnEnter();
		characterBody.SetAimTimer(3f);
		CustomAudio.PlayNear(CustomAudio.Clip.ArmorCharge, transform.position, 30f);
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!fired)
		{
			charge += Time.fixedDeltaTime * attackSpeedStat;
			if (isAuthority && (!Arsenal.Held(this) || charge >= FullCharge * 1.6f)) Fire();
			return;
		}
		after += Time.fixedDeltaTime;
		if (isAuthority && after >= NextConfig.LinearInterval.Value / attackSpeedStat) outer.SetNextStateToMain();
	}

	private void Fire()
	{
		fired = true;
		float t = Mathf.Clamp01(charge / FullCharge);
		bool full = t >= 1f;
		string muzzle = Arsenal.Muzzle(this);
		if (!Arsenal.IsLeft(this)) Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 0.6f);
		CustomAudio.PlayNear(CustomAudio.Clip.Rifle, transform.position, 90f);
		if (full) CustomAudio.PlayNear(CustomAudio.Clip.ArmorBlast, transform.position, 60f);
		GameObject flash = GunFx.Or(GunFx.FlashRail, HPAssets.MuzzleFlash);
		if (flash) EffectManager.SimpleMuzzleFlash(flash, gameObject, muzzle, false);
		Ray aim = GetAimRay();
		AddRecoil(-1f - 2f * t, -1.5f - 3f * t, -0.4f, 0.4f);
		new BulletAttack
		{
			owner = gameObject,
			weapon = gameObject,
			origin = aim.origin,
			aimVector = aim.direction,
			maxSpread = 0f,
			bulletCount = 1u,
			damage = damageStat * NextConfig.LinearDamage.Value * Mathf.Lerp(0.6f, 2.5f, t),
			procCoefficient = 1f,
			force = Mathf.Lerp(400f, 3000f, t),
			falloffModel = BulletAttack.FalloffModel.None,
			maxDistance = 400f,
			radius = Mathf.Lerp(0.3f, 1.2f, t),
			isCrit = RollCrit(),
			muzzleName = muzzle,
			tracerEffectPrefab = full ? GunFx.Or(GunFx.TracerRailSuper, HPAssets.Tracer) : GunFx.Or(GunFx.TracerRailLight, HPAssets.Tracer),
			hitEffectPrefab = GunFx.Or(GunFx.HitRail, HPAssets.HitSpark),
			stopperMask = LayerIndex.world.mask,
			smartCollision = true,
		}.Fire();
		if (characterMotor && !characterMotor.isGrounded) characterMotor.velocity -= aim.direction * 4f * t;
	}

	public override void OnExit()
	{
		if (isAuthority && !fired) Fire();
		base.OnExit();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => fired ? InterruptPriority.Skill : InterruptPriority.PrioritySkill;
}

// Pulse gun: energy bolts you can see fly. No ammo, spends EN. Can't fire while redlined.
public class FirePulseGun : BaseSkillState
{
	private float duration;

	public override void OnEnter()
	{
		base.OnEnter();
		duration = 0.16f / attackSpeedStat;
		characterBody.SetAimTimer(2f);
		HPStatus st = HPStatus.Of(characterBody);
		if (st && st.redline) return;
		if (st) st.Spend(NextConfig.PulseGunEn.Value);
		string muzzle = Arsenal.Muzzle(this);
		if (!Arsenal.IsLeft(this)) Anim.Play(GetModelAnimator(), "Gesture, Override", "FireRifle", 1.6f);
		CustomAudio.PlayNear(CustomAudio.Clip.Lock, transform.position, 50f);
		GameObject flash = GunFx.Or(GunFx.FlashLightning, HPAssets.MuzzleFlash);
		if (flash) EffectManager.SimpleMuzzleFlash(flash, gameObject, muzzle, false);
		if (!isAuthority) return;
		Ray aim = GetAimRay();
		AddRecoil(-0.3f, -0.5f, -0.2f, 0.2f);
		if (GunFx.Bolt)
		{
			ProjectileManager.instance.FireProjectile(GunFx.Bolt, aim.origin, Util.QuaternionSafeLookRotation(aim.direction), gameObject, damageStat * NextConfig.PulseGunDamage.Value, 100f, RollCrit());
		}
		else
		{
			new BulletAttack { owner = gameObject, weapon = gameObject, origin = aim.origin, aimVector = aim.direction, maxSpread = 0.8f, damage = damageStat * NextConfig.PulseGunDamage.Value, procCoefficient = 0.8f, maxDistance = 150f, isCrit = RollCrit(), muzzleName = muzzle, tracerEffectPrefab = HPAssets.Tracer, hitEffectPrefab = HPAssets.HitSpark }.Fire();
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

// Bazooka: slow arcing shell, huge blast and stagger. The kick shoves you back.
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
		GameObject flash = GunFx.Or(GunFx.FlashSmoke, HPAssets.MuzzleFlash);
		if (flash) EffectManager.SimpleMuzzleFlash(flash, gameObject, Arsenal.Muzzle(this), false);
		if (isAuthority && Shell)
		{
			Ray aim = GetAimRay();
			AddRecoil(-4f, -5f, -1f, 1f);
			ProjectileManager.instance.FireProjectile(Shell, aim.origin + aim.direction * 2f, Util.QuaternionSafeLookRotation(aim.direction + Vector3.up * 0.05f), gameObject, damageStat * NextConfig.BazookaDamage.Value, 2000f, RollCrit());
			if (characterMotor)
			{
				Vector3 back = -aim.direction;
				if (characterMotor.isGrounded) back.y = 0f;
				characterMotor.velocity += back.normalized * (characterMotor.isGrounded ? 4f : 10f);
			}
		}
	}

	public override void FixedUpdate()
	{
		base.FixedUpdate();
		if (isAuthority && fixedAge >= duration) outer.SetNextStateToMain();
	}

	public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
}

// Handgun (not in the arm list right now; kept for later).
public class FireHandgun : GunShot
{
	protected override float Interval => 0.42f;
	protected override float Damage => NextConfig.HandgunDamage.Value;
	protected override float Force => 600f;
	protected override float Recoil => 1.4f;
}

// Gatling: spins up while held (slow at first, then a wall of gold tracers), wide bloom, slows you while firing.
public class FireGatling : GunShot
{
	private float spin;
	protected override float Interval => Mathf.Lerp(0.22f, 0.045f, spin);
	protected override float Damage => NextConfig.GatlingDamage.Value;
	protected override float Spread => 1f;
	protected override float Bloom => 0.12f;
	protected override float BloomMax => 3.5f;
	protected override float Proc => 0.5f;
	protected override float Recoil => 0.18f;
	protected override float Force => 120f;
	protected override GameObject TracerFx => GunFx.Or(GunFx.TracerGold, HPAssets.Tracer);
	protected override GameObject FlashFx => GunFx.Or(GunFx.FlashGold, HPAssets.MuzzleFlash);
	protected override float AnimRate => 2.5f;

	public override void OnEnter()
	{
		HPStatus st = HPStatus.Of(characterBody);
		bool left = outer && outer.customName == "LeftArm";
		if (st)
		{
			float last = left ? st.gatlingLastL : st.gatlingLastR;
			float s = left ? st.gatlingSpinL : st.gatlingSpinR;
			s = Time.time - last > 0.4f ? 0f : Mathf.Min(1f, s + 0.1f);
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
