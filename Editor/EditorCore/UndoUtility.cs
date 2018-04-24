using UnityEngine;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEditor.ProBuilder
{
	[InitializeOnLoad]
	static class UndoUtility
	{
		static UndoUtility()
		{
			Undo.undoRedoPerformed += UndoRedoPerformed;
		}

		static void UndoRedoPerformed()
		{
			// material preview when dragging in sceneview is done by applying then undoing changes. we don't want to
			// rebuild the mesh every single frame when dragging.
			if (SceneDragAndDropListener.IsDragging())
				return;

			foreach(pb_Object pb in pb_Util.GetComponents<pb_Object>(Selection.transforms))
			{
				pb.ToMesh();
				pb.Refresh();
				pb.Optimize();

				// because undo after subdivide causes verify to fire, the face references aren't the same anymoore - so reset them
				if( ProBuilderEditor.instance != null && pb.SelectedFaces.Length > 0 )
					pb.SetSelectedFaces(
						System.Array.FindAll(pb.faces, x => pb_Util.ContainsMatch(x.distinctIndices, Face.AllTriangles(pb.SelectedFaces))));
			}

			ProBuilderEditor.Refresh(true);
			SceneView.RepaintAll();
		}

		/**
		 * Since Undo calls can potentially hang the main thread, store states when the diff
		 * will large.
		 */
		public static void RecordSelection(pb_Object pb, string msg)
		{
			if( pb.vertexCount > 256 )
				RegisterCompleteObjectUndo(pb, msg);
			else
				Undo.RecordObject(pb, msg);
		}

		/**
		 *	Tests if any pb_Object in the selection has more than 512 vertices, and if so records the entire object
		 * 	instead of diffing the serialized object (which is very slow for large arrays).
		 */
		public static void RecordSelection(pb_Object[] pb, string msg)
		{
			if (pb == null || pb.Length < 1)
				return;

			if( pb.Any(x => { return x.vertexCount > 256; }) )
				RegisterCompleteObjectUndo(pb, msg);
			else
				Undo.RecordObjects(pb, msg);
		}

		/**
		 * Record an object for Undo.
		 */
		public static void RecordObject(Object obj, string msg)
		{
			if(obj is pb_Object && ((pb_Object)obj).vertexCount > 256)
			{
#if PB_DEBUG
				Debug.LogWarning("RecordObject()  ->  " + ((pb_Object)obj).vertexCount);
#endif
				RegisterCompleteObjectUndo(obj as pb_Object, msg);
			}
			else
			{
				Undo.RecordObject(obj, msg);
			}
		}

		/**
		 * Record objects for Undo.
		 */
		public static void RecordObjects(Object[] objs, string msg)
		{
			if(objs == null)
				return;

			Object[] obj = objs.Where(x => !(x is pb_Object)).ToArray();
			pb_Object[] pb = objs.Where(x => x is pb_Object).Cast<pb_Object>().ToArray();

			Undo.RecordObjects(obj, msg);
			UndoUtility.RecordSelection(pb, msg);
		}

		/**
		 * Undo.RegisterCompleteObjectUndo
		 */
		public static void RegisterCompleteObjectUndo(Object objs, string msg)
		{
			Undo.RegisterCompleteObjectUndo(objs, msg);
		}

		/**
		 * Undo.RegisterCompleteObjectUndo
		 */
		public static void RegisterCompleteObjectUndo(Object[] objs, string msg)
		{
			Undo.RegisterCompleteObjectUndo(objs, msg);
		}

		/**
		 * Record object prior to deletion.
		 */
		public static void DestroyImmediate(Object obj, string msg)
		{
			Undo.DestroyObjectImmediate(obj);
		}

		public static void RegisterCreatedObjectUndo(Object obj, string msg)
		{
			Undo.RegisterCreatedObjectUndo(obj, msg);
		}
	}
}
