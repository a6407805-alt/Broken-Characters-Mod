using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;

namespace GravitosNext.Components;

public class GravitosTracker : MonoBehaviour
{
	private const float UpdateInterval = 0.1f;

	private CharacterBody body;

	private InputBankTest inputBank;

	private Indicator indicator;

	private HurtBox trackingTarget;

	private float stopwatch;

	private readonly BullseyeSearch search = new BullseyeSearch();

	public HurtBox GetTrackingTarget()
	{
		return trackingTarget;
	}

	private void Awake()
	{
		if ((bool)GravitosAssets.TrackingIndicator)
		{
			indicator = new Indicator(base.gameObject, GravitosAssets.TrackingIndicator);
		}
	}

	private void Start()
	{
		body = GetComponent<CharacterBody>();
		inputBank = GetComponent<InputBankTest>();
	}

	private void OnEnable()
	{
		if (indicator != null)
		{
			indicator.active = true;
		}
	}

	private void OnDisable()
	{
		if (indicator != null)
		{
			indicator.active = false;
		}
	}

	private void FixedUpdate()
	{
		stopwatch += Time.fixedDeltaTime;
		if (stopwatch < 0.1f)
		{
			return;
		}
		stopwatch = 0f;
		if ((bool)body && (bool)inputBank)
		{
			Search(new Ray(inputBank.aimOrigin, inputBank.aimDirection));
			if (indicator != null)
			{
				indicator.targetTransform = (trackingTarget ? trackingTarget.transform : null);
			}
		}
	}

	private void Search(Ray aimRay)
	{
		search.teamMaskFilter = TeamMask.GetUnprotectedTeams(body.teamComponent.teamIndex);
		search.filterByLoS = true;
		search.searchOrigin = aimRay.origin;
		search.searchDirection = aimRay.direction;
		search.sortMode = BullseyeSearch.SortMode.Angle;
		search.maxDistanceFilter = GravitosConfig.SelectRange.Value;
		search.maxAngleFilter = GravitosConfig.SelectAngle.Value;
		search.RefreshCandidates();
		search.FilterOutGameObject(base.gameObject);
		IEnumerable<HurtBox> source = search.GetResults();
		if (GravitosConfig.SelectSmallOnly.Value)
		{
			source = source.Where((HurtBox h) => (bool)h.healthComponent && GravityUtil.IsSmall(h.healthComponent.body));
		}
		trackingTarget = source.FirstOrDefault();
	}
}
