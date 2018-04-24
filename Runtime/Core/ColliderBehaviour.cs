﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ProBuilder
{
	/// <summary>
	/// A MonoBehaviour that automatically enables and disables the parent GameObject on exiting and enterin playmode, respectively.
	/// </summary>
	[DisallowMultipleComponent]
	class ColliderBehaviour : EntityBehaviour
	{
		public override void Initialize()
		{
			var collision = gameObject.GetComponent<Collider>();
			if (!collision)
				collision = gameObject.AddComponent<MeshCollider>();
			collision.isTrigger = false;
			SetMaterial(BuiltinMaterials.ColliderMaterial);
		}

		public override void OnEnterPlayMode()
		{
			var r = GetComponent<Renderer>();

			if (r != null)
				r.enabled = false;
		}
	}
}
