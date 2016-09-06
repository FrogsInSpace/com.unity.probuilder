﻿using UnityEngine;
using UnityEditor;
using System.Collections;

namespace ProBuilder2.EditorCommon
{
	public class pb_ImportIcons : AssetPostprocessor
	{
		public void OnPostprocessTexture(Texture2D tex)
		{
			if( assetPath.IndexOf("ProBuilder/Icons") < 0 )
				return;

			TextureImporter ti = (TextureImporter) assetImporter;

#if !UNITY_5_5
			ti.textureType = TextureImporterType.Advanced;
			ti.textureFormat = TextureImporterFormat.AutomaticTruecolor;
#endif
			ti.npotScale = TextureImporterNPOTScale.None;
			ti.filterMode = FilterMode.Point;
			ti.wrapMode = TextureWrapMode.Clamp;
			ti.mipmapEnabled = false;
			ti.maxTextureSize = 64;
		}
	}
}