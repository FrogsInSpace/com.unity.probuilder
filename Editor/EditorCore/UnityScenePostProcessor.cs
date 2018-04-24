﻿using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using ProBuilder.MeshOperations;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEditor.ProBuilder
{
	/// <summary>
	/// When building the project, remove all references to pb_Objects.
	/// </summary>
	static class UnityScenePostProcessor
	{
		[PostProcessScene]
		public static void OnPostprocessScene()
		{
			var invisibleFaceMaterial = Resources.Load<Material>("Materials/InvisibleFace");

			// Hide nodraw faces if present.
			foreach(var pb in Object.FindObjectsOfType<pb_Object>())
			{
				if(pb.GetComponent<MeshRenderer>() == null)
					continue;

				if( pb.GetComponent<MeshRenderer>().sharedMaterials.Any(x => x != null && x.name.Contains("NoDraw")) )
				{
					Material[] mats = pb.GetComponent<MeshRenderer>().sharedMaterials;

					for(int i = 0; i < mats.Length; i++)
					{
						if(mats[i].name.Contains("NoDraw"))
							mats[i] = invisibleFaceMaterial;
					}

					pb.GetComponent<MeshRenderer>().sharedMaterials = mats;
				}
			}

			if(EditorApplication.isPlayingOrWillChangePlaymode)
				return;


			foreach (var entity in Resources.FindObjectsOfTypeAll<EntityBehaviour>())
			{
				if(entity.manageVisibility)
					entity.OnEnterPlayMode();
			}

			// pb_Entity is deprecated - remove someday

			foreach(var pb in Object.FindObjectsOfType<pb_Object>())
			{
				GameObject go = pb.gameObject;

				Entity entity = pb.gameObject.GetComponent<Entity>();

				if( entity == null )
					continue;

				if(entity.entityType == EntityType.Collider || entity.entityType == EntityType.Trigger)
					go.GetComponent<MeshRenderer>().enabled = false;

				// clear hideflags on prefab meshes
				if(pb.msh != null)
					pb.msh.hideFlags = HideFlags.None;

				if(!PreferencesInternal.GetBool(PreferenceKeys.pbStripProBuilderOnBuild))
				   return;

				pb.dontDestroyMeshOnDelete = true;

				Object.DestroyImmediate( pb );
				Object.DestroyImmediate( entity );
			}
		}
	}
}
