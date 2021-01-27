using System;
using System.Linq;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.ProBuilder.Shapes;
using UObject = UnityEngine.Object;
#if UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools.ToolManager;
#else
using ToolManager = UnityEditor.EditorTools.EditorTools;
#endif

namespace UnityEditor.ProBuilder
{
    class DrawShapeTool : EditorTool
    {
        ShapeState m_CurrentState;

        internal ShapeComponent m_LastShapeCreated = null;

        internal ShapeComponent m_ShapeComponent;
        internal bool m_IsShapeInit;

        internal GameObject m_DuplicateGO = null;
        Material m_ShapePreviewMaterial;
        static readonly Color previewColor = new Color(.5f, .9f, 1f, .56f);

        Editor m_ShapeEditor;

        // plane of interaction
        internal UnityEngine.Plane m_Plane;
        internal Vector3 m_PlaneForward;
        internal Vector3 m_PlaneRight;
        internal Quaternion m_PlaneRotation;
        internal Vector3 m_BB_Origin, m_BB_OppositeCorner, m_BB_HeightCorner;

        internal bool m_IsOnGrid;

        internal Bounds m_Bounds;
        internal static readonly Color k_BoundsColor = new Color(.2f, .4f, .8f, 1f);

        readonly GUIContent k_ShapeTitle = new GUIContent("Draw Shape");

        internal static Pref<int> s_ActiveShapeIndex = new Pref<int>("ShapeBuilder.ActiveShapeIndex", 0);
        public static Pref<bool> s_SettingsEnabled = new Pref<bool>("ShapeComponent.SettingsEnabled", false);

        internal static Pref<int> s_LastPivotLocation = new Pref<int>("ShapeBuilder.LastPivotLocation", (int)PivotLocation.FirstCorner);
        internal static Pref<Vector3> s_LastPivotPosition = new Pref<Vector3>("ShapeBuilder.LastPivotPosition", Vector3.zero);
        internal static Pref<Vector3> s_LastSize = new Pref<Vector3>("ShapeBuilder.LastSize", Vector3.one);
        internal static Pref<Quaternion> s_LastRotation = new Pref<Quaternion>("ShapeBuilder.LastRotation", Quaternion.identity);

        int m_ControlID;
        // ideally this would be owned by the state machine
        public int controlID => m_ControlID;

        //Styling
        static class Styles
        {
            public static GUIStyle command = "command";
        }
        GUIStyle m_BoldCenteredStyle = null;

        //EditorTools
        GUIContent m_IconContent;
        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }

        public static Type activeShapeType
        {
            get { return s_ActiveShapeIndex < 0 ? typeof(Cube) : EditorShapeUtility.availableShapeTypes[s_ActiveShapeIndex]; }
        }

        internal ShapeComponent currentShapeInOverlay
        {
            get
            {
                if(m_CurrentState is ShapeState_InitShape  && m_LastShapeCreated != null)
                    return m_LastShapeCreated;

                if(m_CurrentState is ShapeState_DrawBaseShape && m_DuplicateGO != null)
                    return m_DuplicateGO.GetComponent<ShapeComponent>();

                if(m_ShapeComponent == null)
                {
                    m_ShapeComponent = new GameObject("Shape", typeof(ShapeComponent)).GetComponent<ShapeComponent>();
                    m_ShapeComponent.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    m_ShapeComponent.hideFlags = HideFlags.None;
                    m_ShapeComponent.SetShape(EditorShapeUtility.CreateShape(activeShapeType),EditorUtility.newShapePivotLocation);
                    m_ShapeComponent.pivotLocation = (PivotLocation)s_LastPivotLocation.value;
                    m_ShapeComponent.pivotLocalPosition = s_LastPivotPosition.value;
                    m_ShapeComponent.size = s_LastSize.value;
                    m_ShapeComponent.rotation = s_LastRotation.value;
                }
                return m_ShapeComponent;
            }
        }

        void OnEnable()
        {
            m_CurrentState = InitStateMachine();

            m_IconContent = new GUIContent()
            {
                image = IconUtility.GetIcon("Tools/ShapeTool/Arch"),
                text = "Draw Shape Tool",
                tooltip = "Draw Shape Tool"
            };

            Undo.undoRedoPerformed += HandleUndoRedoPerformed;
            MeshSelection.objectSelectionChanged += OnSelectionChanged;
            ToolManager.activeToolChanged += OnActiveToolChanged;

            m_ShapePreviewMaterial = new Material(BuiltinMaterials.defaultMaterial.shader);
            m_ShapePreviewMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (m_ShapePreviewMaterial.HasProperty("_MainTex"))
                m_ShapePreviewMaterial.mainTexture = (Texture2D)Resources.Load("Textures/GridBox_Default");

            if (m_ShapePreviewMaterial.HasProperty("_Color"))
                m_ShapePreviewMaterial.SetColor("_Color", previewColor);
        }

        void OnDestroy()
        {
            MeshSelection.objectSelectionChanged -= OnSelectionChanged;
            if(m_ShapePreviewMaterial)
                DestroyImmediate(m_ShapePreviewMaterial);
        }

        void OnActiveToolChanged()
        {
            if(ToolManager.IsActiveTool(this))
                SetBounds(currentShapeInOverlay.size);
        }

        void HandleUndoRedoPerformed()
        {
            if(ToolManager.IsActiveTool(this))
                m_CurrentState = ShapeState.ResetState();
        }

        void OnSelectionChanged()
        {
            if(ToolManager.IsActiveTool(this))
            {
                if(Selection.activeGameObject != null
                   && MeshSelection.activeMesh != currentShapeInOverlay.mesh)
                {
                    m_CurrentState = ShapeState.ResetState();
                    ToolManager.RestorePreviousTool();
                }
            }
        }

        ShapeState InitStateMachine()
        {
            ShapeState.tool = this;
            ShapeState initState = new ShapeState_InitShape();
            ShapeState drawBaseState = new ShapeState_DrawBaseShape();
            ShapeState drawHeightState = new ShapeState_DrawHeightShape();
            ShapeState.s_defaultState = initState;
            initState.m_nextState = drawBaseState;
            drawBaseState.m_nextState = drawHeightState;
            drawHeightState.m_nextState = initState;

            return ShapeState.StartStateMachine();
        }

        void OnDisable()
        {
            if(m_ShapeEditor != null)
                DestroyImmediate(m_ShapeEditor);
            if (m_ShapeComponent != null && m_ShapeComponent.gameObject.hideFlags == HideFlags.HideAndDontSave)
                DestroyImmediate(m_ShapeComponent.gameObject);
        }

        internal static void SaveShapeParams(ShapeComponent shapeComponent)
        {
            s_LastPivotLocation.value = (int)shapeComponent.pivotLocation;
            s_LastPivotPosition.value = shapeComponent.pivotLocalPosition;
            s_LastSize.value = shapeComponent.size;
            s_LastRotation.value = shapeComponent.rotation;

            EditorShapeUtility.SaveParams(shapeComponent.shape);
        }

        internal static void ApplyPrefsSettings(ShapeComponent shapeComponent)
        {
            shapeComponent.pivotLocation = (PivotLocation)s_LastPivotLocation.value;
            shapeComponent.pivotLocalPosition = s_LastPivotPosition.value;
            shapeComponent.size = s_LastSize.value;
            shapeComponent.rotation = s_LastRotation.value;
        }

        // Returns a local space point,
        public Vector3 GetPoint(Vector3 point, bool useIncrementSnap = false)
        {
            if(useIncrementSnap)
                return ProBuilderSnapping.Snap(point, EditorSnapping.incrementalSnapMoveValue);

            if (m_IsOnGrid)
                return ProBuilderSnapping.Snap(point, EditorSnapping.activeMoveSnapValue);

            return point;
        }

        internal void SetBounds(Vector3 size)
        {
            //Keep orientation created using mouse drag
            var dragDirection = m_BB_OppositeCorner - m_BB_Origin;
            float x = dragDirection.x < 0 ? -size.x : size.x;
            float z = dragDirection.z < 0 ? -size.z : size.z;

            m_BB_OppositeCorner = m_BB_Origin + new Vector3(x, 0, z);
            m_BB_HeightCorner = m_BB_Origin + size;
        }

        internal void DuplicatePreview(Vector3 position)
        {
            if(position.Equals(Vector3.positiveInfinity))
                return;

            var pivotLocation = (PivotLocation)s_LastPivotLocation.value;
            var size = currentShapeInOverlay.size;

            m_Bounds.size = size;

            Vector3 cornerPosition;
            switch(pivotLocation)
            {
                case PivotLocation.FirstCorner:
                    cornerPosition = GetPoint(position);
                    m_PlaneRotation = Quaternion.LookRotation(m_PlaneForward,m_Plane.normal);
                    m_Bounds.center = cornerPosition + m_PlaneRotation * size / 2f;

                    m_BB_Origin = cornerPosition;
                    m_BB_HeightCorner = m_Bounds.center + m_PlaneRotation * (size / 2f);
                    m_BB_OppositeCorner = m_BB_HeightCorner - m_PlaneRotation * new Vector3(0, size.y, 0);
                    break;

                case PivotLocation.Center:
                default:
                    position = GetPoint(position);
                    cornerPosition = position - size / 2f;
                    cornerPosition.y = position.y;
                    m_Bounds.center = cornerPosition + new Vector3(size.x/2f,0, size.z/2f) + (size.y / 2f) * m_Plane.normal;
                    m_PlaneRotation = Quaternion.LookRotation(m_PlaneForward,m_Plane.normal);

                    m_BB_Origin = m_Bounds.center - m_PlaneRotation * (size / 2f);
                    m_BB_HeightCorner = m_Bounds.center + m_PlaneRotation * (size / 2f);
                    m_BB_OppositeCorner = m_BB_HeightCorner - m_PlaneRotation * new Vector3(0, size.y, 0);
                    break;
            }

            ShapeComponent shape;
            if(m_DuplicateGO == null)
            {
                shape = ShapeFactory.Instantiate(activeShapeType, ( (PivotLocation)s_LastPivotLocation.value )).GetComponent<ShapeComponent>();
                m_DuplicateGO = shape.gameObject;
                m_DuplicateGO.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                ApplyPrefsSettings(shape);
                shape.GetComponent<MeshRenderer>().sharedMaterial = m_ShapePreviewMaterial;
            }
            else
                shape = m_DuplicateGO.GetComponent<ShapeComponent>();

            EditorShapeUtility.CopyLastParams(shape.shape, shape.shape.GetType());
            shape.Rebuild(m_Bounds, m_PlaneRotation);
            ProBuilderEditor.Refresh(false);
        }

        void RecalculateBounds()
        {
            var forward = HandleUtility.PointOnLineParameter(m_BB_OppositeCorner, m_BB_Origin, m_PlaneForward);
            var right = HandleUtility.PointOnLineParameter(m_BB_OppositeCorner, m_BB_Origin, m_PlaneRight);

            var localHeight = Quaternion.Inverse(m_PlaneRotation) * (m_BB_HeightCorner - m_BB_OppositeCorner);
            var height = localHeight.y;

            m_Bounds.size = forward * Vector3.forward + right * Vector3.right + height * Vector3.up;
            m_Bounds.center = m_BB_Origin + 0.5f * ( m_BB_OppositeCorner - m_BB_Origin ) + 0.5f * (m_BB_HeightCorner - m_BB_OppositeCorner);

            //Prevent Z-fighting with the drawing surface
            if(Mathf.Abs(m_Bounds.center.y) < 0.0001f)
                m_Bounds.center = m_Bounds.center + 0.0001f * Vector3.up;

            m_PlaneRotation = Quaternion.LookRotation(m_PlaneForward,m_Plane.normal);
        }

        internal void RebuildShape()
        {
            RecalculateBounds();

            if(m_Bounds.size.sqrMagnitude < .01f
               || Mathf.Abs(m_Bounds.extents.x) < 0.001f
               || Mathf.Abs(m_Bounds.extents.z) < 0.001f)
            {
                if(m_ShapeComponent.mesh.vertexCount > 0)
                {
                    m_ShapeComponent.mesh.Clear();
                    m_ShapeComponent.mesh.Rebuild();
                    ProBuilderEditor.Refresh(true);
                }
                return;
            }

            if (!m_IsShapeInit)
            {
                EditorShapeUtility.CopyLastParams(m_ShapeComponent.shape, m_ShapeComponent.shape.GetType());
                m_ShapeComponent.gameObject.hideFlags = HideFlags.None;
                UndoUtility.RegisterCreatedObjectUndo(m_ShapeComponent.gameObject, "Draw Shape");
            }

            m_ShapeComponent.Rebuild(m_Bounds, m_PlaneRotation);
            ProBuilderEditor.Refresh(false);

            if (!m_IsShapeInit)
            {
                EditorUtility.InitObject(m_ShapeComponent.mesh);
                m_IsShapeInit = true;
            }

            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            SceneViewOverlay.Window(k_ShapeTitle, OnOverlayGUI, 0, SceneViewOverlay.WindowDisplayOption.OneWindowPerTitle);

            var evt = Event.current;

            if (EditorHandleUtility.SceneViewInUse(evt))
                return;

            m_ControlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(m_ControlID);

            m_CurrentState = m_CurrentState.DoState(evt);
        }

        internal void DrawBoundingBox(bool drawCorners = true)
        {
            using (new Handles.DrawingScope(k_BoundsColor, Matrix4x4.TRS(m_Bounds.center, m_PlaneRotation.normalized, Vector3.one)))
            {
                Handles.DrawWireCube(Vector3.zero, m_Bounds.size);
            }

            if(!drawCorners)
                return;

            using (new Handles.DrawingScope(Color.white))
            {
                Handles.DotHandleCap(-1, m_BB_Origin, Quaternion.identity, HandleUtility.GetHandleSize(m_BB_Origin) * 0.05f, EventType.Repaint);
                Handles.DotHandleCap(-1, m_BB_OppositeCorner, Quaternion.identity, HandleUtility.GetHandleSize(m_BB_OppositeCorner) * 0.05f, EventType.Repaint);
            }
            using (new Handles.DrawingScope(EditorHandleDrawing.vertexSelectedColor))
            {
                Handles.DotHandleCap(-1, m_BB_HeightCorner, Quaternion.identity, HandleUtility.GetHandleSize(m_BB_HeightCorner) * 0.05f, EventType.Repaint);
            }
        }

        void OnOverlayGUI(UObject overlayTarget, SceneView view)
        {
            EditorGUIUtility.AddCursorRect(new Rect(0, 0, Screen.width, Screen.height), MouseCursor.ArrowPlus);
            EditorGUILayout.HelpBox(L10n.Tr("Click and drag to place and scale the shape, or SHIFT+click once to duplicate last size settings."), MessageType.Info);

            DrawShapeGUI();

            var snapEnabled = Tools.pivotRotation != PivotRotation.Global;
            using(new EditorGUI.DisabledScope(snapEnabled))
            {
                if(snapEnabled)
                    EditorSnapSettings.gridSnapEnabled = EditorGUILayout.Toggle("Snapping", EditorSnapSettings.gridSnapEnabled);
                else
                    EditorGUILayout.Toggle("Snapping", false);
            }

            string foldoutName = "New Shape Settings";
            if(currentShapeInOverlay == m_LastShapeCreated)
                foldoutName = "Settings (" + m_LastShapeCreated.name + ")";

            Editor.CreateCachedEditor(currentShapeInOverlay, typeof(ShapeComponentEditor), ref m_ShapeEditor);

            using(new EditorGUILayout.VerticalScope(new GUIStyle(EditorStyles.frameBox)))
            {
                ( (ShapeComponentEditor) m_ShapeEditor ).m_ShapePropertyLabel.text = foldoutName;
                ( (ShapeComponentEditor) m_ShapeEditor ).DrawShapeParametersGUI(this);
            }
        }

        void DrawShapeGUI()
        {
            if(m_BoldCenteredStyle == null)
                m_BoldCenteredStyle = new GUIStyle("BoldLabel") { alignment = TextAnchor.MiddleCenter };

            EditorGUILayout.LabelField(EditorShapeUtility.shapeTypes[s_ActiveShapeIndex.value], m_BoldCenteredStyle, GUILayout.ExpandWidth(true));

            var shape = currentShapeInOverlay.shape;

            int groupCount = EditorShapeUtility.shapeTypesGUI.Count;
            for(int i = 0; i < groupCount; i++)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                int index = GUILayout.Toolbar(s_ActiveShapeIndex.value - + i * EditorShapeUtility.MaxContentPerGroup, EditorShapeUtility.shapeTypesGUI[i], Styles.command);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    s_ActiveShapeIndex.value = index + i * EditorShapeUtility.MaxContentPerGroup;

                    var type = EditorShapeUtility.availableShapeTypes[s_ActiveShapeIndex];
                    if(shape.GetType() != type)
                    {
                        if(currentShapeInOverlay == m_LastShapeCreated)
                            m_LastShapeCreated = null;

                        UndoUtility.RegisterCompleteObjectUndo(currentShapeInOverlay, "Change Shape");
                        currentShapeInOverlay.SetShape(EditorShapeUtility.CreateShape(type), currentShapeInOverlay.pivotLocation);
                        SetBounds(currentShapeInOverlay.size);

                        ProBuilderEditor.Refresh();
                    }
                }
            }
        }
    }
}