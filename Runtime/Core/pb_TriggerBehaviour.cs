﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ProBuilder
{
	[DisallowMultipleComponent]
	class pb_TriggerBehaviour : EntityBehaviour
	{
		public override void Initialize()
		{
			var collision = gameObject.GetComponent<Collider>();

			if (!collision)
				collision = gameObject.AddComponent<MeshCollider>();

			var meshCollider = collision as MeshCollider;

			if (meshCollider)
				meshCollider.convex = true;

			collision.isTrigger = true;

			SetMaterial(BuiltinMaterials.TriggerMaterial);
		}

		public override void OnEnterPlayMode()
		{
			var r = GetComponent<Renderer>();

			if (r != null)
				r.enabled = false;
		}
	}
}