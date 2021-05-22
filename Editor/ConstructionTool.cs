using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using System.Linq;

namespace DI_ConstructionSystem.Editor
{
    using CTool = ConstructionTool;
    using CSnap = ConstructionSnap;
    using CSegment = ConstructionSegment;

    [EditorTool("Construction Tool")]
    public class ConstructionTool : EditorTool
    {
        //Tool
        public static CTool Instance;
        public static Mode mode = Mode.ViewSelected;

        [SerializeField]
        Texture2D m_ToolIcon;
        GUIContent m_IconContent;

        public override GUIContent toolbarIcon
        {
            get { return m_IconContent; }
        }

        public enum Mode
        {
            ViewSelected,
            ViewOpen,
            ViewAll,
            None
        }
        //Main
        public List<CSegment> segments = new List<CSegment>();
        public List<CSnap> _snaps = new List<CSnap>();

        bool mouseDown;

        //Movement
        Vector3 startPos, endPos;
        Vector3 Delta
        {
            get
            {
                return (endPos - startPos);
            }
        }
        public static int increment = 45;
        const float minMoveDist = 0.35f;

        //Snapping
        public List<CSnap> snapIterate = new List<CSnap>(), movingSnaps = new List<CSnap>();
        Dictionary<CSnap, CSnap> possibleSnaps = new Dictionary<CSnap, CSnap>();
        const float snapDistance = 0.1f;
        float bSize = 0.1f;
        float pSize = 0.1f;

        bool snapDragging = true;
        CSnap snapDragOrigin;
        Plane snapPlane;

        //Toolbar
        string[] toolbarTabs = new string[] { "Snap", "API", "Character", "Nav" };
        GUIContent[] toolbarContent;
        int toolbarSelection = 0;
        GUISkin toolbarSkin;
        GUIStyle toolbarStyle, borderBoxStyle;
        delegate void OnToolbarChange(int oldValue, int newValue);
        OnToolbarChange onToolbarChange;
        bool showToolbar;

        private void OnEnable()
        {
            m_IconContent = new GUIContent()
            {
                image = m_ToolIcon,
                text = "Construction Tool",
                tooltip = "Dracon LDA"
            };

            Instance = this;

            ToolManager.activeToolChanged += EditorTools_activeToolChanged;
            Selection.selectionChanged += Instance.UpdateIterate;
            Selection.selectionChanged += RefreshSegments;
            Selection.selectionChanged += UpdateMoveObjects;

            CreateToolBar();
        }

        private void EditorTools_activeToolChanged()
        {
            if (ToolManager.IsActiveTool(this))
            {
                RefreshSegments();
                Instance.UpdateIterate();
                UpdateMoveObjects();
                CreateToolBar();
                showToolbar = false;
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            var view = window as SceneView;
            if (view == null)
                return;

            var evt = Event.current;
            var t_evt = evt.type;

            if (t_evt == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
            else if (t_evt == EventType.Repaint)
            {
                foreach (var seg in segments.Where(x => x.zeroCheck))
                {
                    seg.ZeroCheck();
                }
            }

            SelectUI(view, evt);

            SnapUI(view, evt);

            DragUI(view, evt);

            OGUI(view, evt);

            MoveObjects(view, evt);
        }

        public static void RefreshSegments()
        {
            Debug.Log("Refreshing Segments");
            if (Instance == null)
            {
                return;
            }
            Instance.segments.Clear();
            Instance._snaps.Clear();
            foreach (CSegment seg in Resources.FindObjectsOfTypeAll(typeof(CSegment)) as CSegment[])
            {
                if (!EditorUtility.IsPersistent(seg.transform.root.gameObject) && !(seg.hideFlags == HideFlags.NotEditable || seg.hideFlags == HideFlags.HideAndDontSave))
                {
                    Instance.segments.Add(seg);
                    foreach (var snap in seg.snaps)
                    {
                        Instance._snaps.Add(snap);
                    }
                }
            }
        }

        public void CreateToolBar()
        {
            toolbarSkin = Resources.Load<GUISkin>("Toolbar/ToolbarSkin");
            toolbarStyle = new GUIStyle(toolbarSkin.GetStyle("Tab"));
            borderBoxStyle = new GUIStyle(toolbarSkin.GetStyle("BorderBox"));
        }

        #region shortcuts
        [Shortcut("Construction - Change Mode", KeyCode.G, ShortcutModifiers.Alt)]
        public static void ChangeMode()
        {
            RefreshSegments();
            if (Instance == null || !ToolManager.IsActiveTool(Instance))
            {
                return;
            }
            if (mode == Mode.ViewSelected)
            {
                mode = Mode.ViewOpen;
            }
            else if (mode == Mode.ViewOpen)
            {
                mode = Mode.ViewAll;
            }
            else if (mode == Mode.ViewAll)
            {
                mode = Mode.ViewSelected;
            }

            Instance.UpdateIterate();
        }

        [Shortcut("Construction - Refresh", KeyCode.G, ShortcutModifiers.Shift)]
        public static void Refresh()
        {
            RefreshSegments();
            Instance.UpdateIterate();
            Instance.UpdateMoveObjects();
            Instance.CreateToolBar();
        }

        [Shortcut("Construction Tool", KeyCode.G)]
        public static void SwitchToThis()
        {
            ToolManager.SetActiveTool(typeof(ConstructionTool));
        }

        [Shortcut("Construction - Rotate Left", KeyCode.D, ShortcutModifiers.Alt)]
        public static void RotateLeft()
        {
            if (Instance == null || !ToolManager.IsActiveTool(Instance))
                return;

            Undo.RecordObjects(Selection.transforms, "Rotate Object Left");

            foreach (var t in Selection.transforms)
            {
                t.Rotate(Vector3.up, -increment);
            }
        }

        [Shortcut("Construction - Rotate Right", KeyCode.A, ShortcutModifiers.Alt)]
        public static void RotateRight()
        {
            if (Instance == null || !ToolManager.IsActiveTool(Instance))
                return;

            Undo.RecordObjects(Selection.transforms, "Rotate Object Right");

            foreach (var t in Selection.transforms)
            {
                t.Rotate(Vector3.up, increment);
            }
        }

        [Shortcut("Construction - Add Increment", KeyCode.E, ShortcutModifiers.Alt)]
        public static void AddIncrement()
        {
            switch (increment)
            {
                case 0:
                    increment = 15;
                    break;
                case 15:
                    increment = 45;
                    break;
                case 45:
                    increment = 90;
                    break;
                case 90:
                    increment = 135;
                    break;
                case 135:
                    increment = 180;
                    break;
                default:
                    break;
            }
        }
        [Shortcut("Construction - Subtract Increment", KeyCode.Q, ShortcutModifiers.Alt)]
        public static void SubtractIncrement()
        {
            switch (increment)
            {
                case 15:
                    increment = 0;
                    break;
                case 45:
                    increment = 15;
                    break;
                case 90:
                    increment = 45;
                    break;
                case 135:
                    increment = 90;
                    break;
                case 180:
                    increment = 135;
                    break;
                default:
                    break;
            }
        }
        #endregion

        void SelectUI(SceneView view, Event evt)
        {
            using (new Handles.DrawingScope(Color.yellow))
            {
                foreach (var segment in segments)
                {
                    if (segment == null || Selection.activeGameObject == segment.gameObject)
                    {
                        continue;
                    }
                    if (Handles.Button(segment.transform.position, segment.transform.rotation, 0.1f, 0.1f, Handles.CubeHandleCap))
                    {
                        Selection.activeGameObject = segment.gameObject;
                    }
                }
            }
        }

        void SnapUI(SceneView view, Event evt)
        {
            segments.RemoveAll(x => x == null);
            _snaps.RemoveAll(x => x == null);
            List<CSnap> skipSnaps = new List<CSnap>();


            using (var scope = new Handles.DrawingScope(Color.yellow))
            {
                foreach (var snap in snapIterate)
                {
                    if (skipSnaps.Contains(snap)) continue;

                    if (snap.connected)
                    {
                        if (!skipSnaps.Contains(snap.ConnectedTo)) { skipSnaps.Add(snap.ConnectedTo); }
                        DetachUI(snap);
                    }
                }
            }
        }

        void DragUI(SceneView view, Event evt)
        {
            float dragSizeMod = 0.1f;
            Dictionary<int, CSnap> handles = new Dictionary<int, CSnap>();
            foreach (var snap in _snaps)
            {
                if (!snap.connected)
                {
                    Vector3 pos = snap.transform.position;
                    int id = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);
                    if (!handles.ContainsKey(id))
                    {
                        handles.Add(id, snap);
                    }
                    //Repaint sets up draw. Layout sets up click detect. 
                    using (new Handles.DrawingScope(Color.yellow))
                    {
                        if (evt.type == EventType.Repaint)
                        {
                            Handles.CubeHandleCap(id, pos, Quaternion.identity, dragSizeMod, EventType.Repaint);
                        }
                        else if (evt.type == EventType.Layout)
                        {
                            Handles.CubeHandleCap(id, pos, Quaternion.identity, dragSizeMod, EventType.Layout);
                        }
                    }
                }
            }
            switch (evt.type)
            {
                case EventType.MouseDown:
                    int md_i = HandleUtility.nearestControl;
                    if (handles.ContainsKey(md_i))
                    {
                        Debug.Log($"Handle {md_i} clicked, attached to snap {handles[md_i].name}");
                        snapDragging = true;
                        snapDragOrigin = handles[md_i];
                        snapPlane = new Plane(Vector3.down, handles[md_i].transform.position);
                    }
                    break;
                case EventType.MouseUp:
                    if (snapDragging)
                    {
                        int mu_i = HandleUtility.nearestControl;
                        if (handles.ContainsKey(mu_i))
                        {
                            snapDragOrigin.Attach(handles[mu_i]);
                            Debug.Log("Attached points by drag - " + snapDragOrigin + " - " + handles[mu_i]);
                        }
                    }
                    snapDragging = false;
                    break;
                default:
                    break;
            }

            if (snapDragging)
            {
                Vector3 mousePosition = Event.current.mousePosition;
                Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
                mousePosition = ray.origin;
                using (new Handles.DrawingScope(Color.yellow))
                {
                    Vector3 snapPos = snapDragOrigin.transform.position;
                    Handles.DrawWireDisc(snapPos, Vector3.up, 0.3f);
                    Handles.DrawWireDisc(snapPos, Vector3.right, 0.3f);

                    Handles.DrawAAPolyLine(10, snapPos, mousePosition);
                    HandleUtility.Repaint();
                }
            }
        }

        void OGUI(SceneView view, Event evt)
        {
            Handles.BeginGUI();

            //Toolbar
            GUILayout.BeginArea(new Rect(3, 1, Screen.width, 200));
            if (showToolbar)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    int currentSelection = toolbarSelection;
                    toolbarSelection = GUILayout.Toolbar(toolbarSelection, toolbarTabs, toolbarStyle);
                    if (check.changed)
                    {
                        onToolbarChange?.Invoke(currentSelection, toolbarSelection);
                    }
                }
            }
            showToolbar = EditorGUILayout.Toggle(showToolbar, toolbarSkin.toggle);
            GUILayout.EndArea();

            //Debug
            GUILayout.BeginArea(new Rect(Screen.width - 230, Screen.height - 190, 225, 150), borderBoxStyle);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string UI = $"Increment: {increment}\n" +
                    $"Mode: {mode}\n" +
                    $"Drag Magnitude: {(endPos - startPos).magnitude}\n" +
                    $"Segments | Snaps: {segments.Count} | {_snaps.Count}\n" +
                    $"Connected Snaps: {_snaps.Where(x => x.connected).Count()}\n" +
                    $"Snap Iterate: {snapIterate.Count()}";
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(UI);
                    GUILayout.FlexibleSpace();
                }
            }

            GUILayout.EndArea();


            Handles.EndGUI();
        }

        void MoveObjects(SceneView view, Event evt)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                UpdateMoveObjects();
                mouseDown = true;
            }
            else if (e.type == EventType.MouseUp)
            {
                if (mouseDown && startPos != endPos && Delta.magnitude > minMoveDist)
                {
                    Undo.RecordObjects(Selection.transforms, "Simple Transform Tool");
                    foreach (var trs in Selection.transforms)
                    {
                        trs.position += Delta;
                    }

                    if (possibleSnaps.Count > 0)
                    {
                        var pSnaps = possibleSnaps.Keys.ToArray();
                        foreach (var snap in pSnaps)
                        {
                            snap.Attach(possibleSnaps[snap]);
                        }
                        pSnaps.MultiSnapPosition();
                    }
                }

                Instance.UpdateIterate();
                UpdateMoveObjects();

                mouseDown = false;
                return;
            }

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var rot = Quaternion.identity;
                Vector3 _pos = Handles.PositionHandle(endPos, rot);
                if (check.changed)
                {
                    endPos = _pos;
                    UpdateSnapDetection();
                }
            }

            using (var scope = new Handles.DrawingScope(Color.yellow))
            {
                if (startPos != endPos)
                {
                    Handles.DrawAAPolyLine(5f, startPos, endPos);

                    if (Delta.magnitude > minMoveDist)
                    {
                        foreach (var s in movingSnaps)
                        {
                            Vector3 cPos = s.transform.position + Delta;
                            if (possibleSnaps.ContainsKey(s))
                            {
                                Handles.color = Color.cyan;
                                Vector3 oPos = possibleSnaps[s].transform.position;
                                Handles.DrawWireCube(oPos, Vector3.one * 0.1f);
                                Handles.DrawAAPolyLine(oPos, cPos);
                                Handles.color = Color.yellow;
                            }
                            Handles.DrawWireDisc(cPos, Vector3.up, 0.05f);
                        }

                        foreach (var s in _snaps)
                        {
                            if (!possibleSnaps.ContainsKey(s) && !movingSnaps.Contains(s) && !s.connected)
                            {
                                Handles.DrawWireCube(s.transform.position, Vector3.one * 0.1f);
                            }
                        }

                        Handles.DrawWireDisc(endPos, Vector3.up, 0.15f);

                    }
                }
            }
        }

        void UpdateMoveObjects()
        {
            startPos = Tools.handlePosition;
            endPos = Tools.handlePosition;

            movingSnaps.Clear();
            foreach (var trs in Selection.transforms)
            {
                foreach (var s in trs.GetComponentsInChildren<ConstructionSnap>())
                {
                    movingSnaps.Add(s);
                }
            }
        }

        void UpdateSnapDetection()
        {
            possibleSnaps.Clear();
            foreach (var snap in movingSnaps)
            {
                if (snap.connected)
                {
                    continue;
                }

                var other = snap.ClosestInRange(snapDistance, Delta);
                if (other != null)
                {
                    possibleSnaps.Add(snap, other);
                }
            }
        }

        void DetachUI(CSnap snap)
        {
            Transform snapT = snap.transform;
            Vector3 midPos = (snapT.position + snap.ConnectedTo.transform.position) * 0.5f;
            Vector3 raisedMid = midPos + Vector3.up * 0.1f;

            Handles.DrawAAPolyLine(snapT.position, snap.ConnectedTo.transform.position);
            Handles.DrawAAPolyLine(midPos, raisedMid);

            if (Handles.Button(raisedMid, Quaternion.identity, bSize, pSize, Handles.SphereHandleCap))
            {
                snap?.DetachAll();
            }

            if (Handles.Button(raisedMid + Vector3.up * 0.15f, Quaternion.identity, bSize * 0.5f, pSize * 0.5f, Handles.SphereHandleCap))
            {
                snap?.SnapPosition(true);
            }

        }
    }

    public static class ConstructionExtension
    {
        public static void UpdateIterate(this CTool tool)
        {
            tool.segments.RemoveAll(x => x == null);
            tool.snapIterate.Clear();
            switch (CTool.mode)
            {
                case CTool.Mode.ViewSelected:
                    foreach (var segment in tool.segments.Where(x => Selection.transforms.Contains(x.transform)))
                    {
                        tool.snapIterate.AddRange(segment.snaps);
                    }
                    break;
                case CTool.Mode.ViewOpen:
                    tool.snapIterate.AddRange(tool._snaps.Where(x => x.connected == false));
                    break;
                case CTool.Mode.ViewAll:
                    tool.snapIterate.AddRange(tool._snaps);
                    break;
                case CTool.Mode.None:
                    break;
            }
        }

        public static void ZeroCheck(this CSegment segment)
        {
            //Im doing some HELLA hacky stuff here to deal with object duplication. 
            Vector3 segPos = segment.transform.position;
            foreach (var snap in segment.snaps)
            {
                var other = snap.ClosestInRange(0.01f, Vector3.zero);
                if (other != null && !other.connected)
                {
                    if (Vector3.Distance(other.segment.transform.position, segPos) < 0.01f)
                    {
                        continue;
                    }
                    Debug.Log("Attaching From Zero Check");
                    snap.Attach(other);
                }
            }
            segment.zeroCheck = false;
        }

        public static Vector3 SnapDelta(this CSnap snap)
        {
            if (snap == null || !snap.connected || snap.ConnectedTo == null)
            {
                return Vector3.zero;
            }

            Vector3 A = snap.transform.position;
            Vector3 B = snap.ConnectedTo.transform.position;

            Vector3 C = B - A;
            return C;
        }

        public static CSnap ClosestInRange(this CSnap snap, float radius, Vector3 offset)
        {
            ConstructionTool ct = ConstructionTool.Instance;
            float max = radius;
            ConstructionSnap result = null;
            Vector3 pos = snap.transform.position + offset;

            foreach (var other in ct._snaps)
            {
                if (other.segment == snap.segment)
                {
                    continue;
                }

                float dist = (other.transform.position - pos).sqrMagnitude;
                if (dist < max)
                {
                    max = dist;
                    result = other;
                }
            }
            return result;
        }

        public static void Attach(this CSnap snap, CSnap other)
        {
            if (other == null || snap == null)
                return;
            Undo.RecordObjects(new Object[] { snap, other }, "Attach");
            snap.ConnectedTo = other;
            other.ConnectedTo = snap;
        }

        public static void DetachAll(this CSnap snap)
        {
            if (!snap.connected || snap.ConnectedTo == null)
            {
                return;
            }
            Undo.RecordObjects(new Object[] { snap.ConnectedTo, snap }, "Detach");
            snap.ConnectedTo.ConnectedTo = null;
            snap.ConnectedTo = null;
        }

        public static void SnapPosition(this CSnap snap, bool moveSelf)
        {
            if (!snap.connected)
            {
                return;
            }
            CSnap other = snap.ConnectedTo;
            var snapDelta = moveSelf ? snap.SnapDelta() : other.SnapDelta();
            if (snapDelta != Vector3.zero)
            {
                Undo.RecordObject(moveSelf ? snap.segment.transform : other.segment.transform, "Snap To");
                if (moveSelf)
                {
                    snap.segment.transform.position += snapDelta;
                }
                else
                {
                    other.segment.transform.position += snapDelta;
                }
            }
        }

        public static void MultiSnapPosition(this CSnap[] snaps)
        {
            Dictionary<CSegment, List<CSnap>> movements = new Dictionary<CSegment, List<CSnap>>();

            foreach (var snap in snaps)
            {
                if (!snap.connected)
                {
                    continue;
                }
                else
                {
                    if (!movements.ContainsKey(snap.segment))
                    {
                        movements.Add(snap.segment, new List<CSnap>() { snap });
                    }
                    else
                    {
                        movements[snap.segment].Add(snap);
                    }
                }
            }

            foreach (var key in movements.Keys.ToArray())
            {
                Vector3 mid = Vector3.zero;
                foreach (var val in movements[key])
                {
                    mid += val.SnapDelta();
                }
                mid /= movements[key].Count();
                Undo.RecordObject(key.transform, "Snap To");
                key.transform.position += mid;
            }

        }
    }
}
