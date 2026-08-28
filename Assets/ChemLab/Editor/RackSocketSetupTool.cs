using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ChemLab.Editor
{
    /// <summary>
    /// ChemLab → Rack Socket Setup Tool
    ///
    /// HOW TO USE:
    ///   1. Open via Unity menu: ChemLab → Rack Socket Setup Tool
    ///   2. Drag your Rack root object into "Rack Root"
    ///   3. Drag all your Flask/Tube objects into the "Flasks / Tubes" list
    ///   4. Adjust the name filter if your peg objects have a different name
    ///   5. Click "Setup Sockets on Pegs"  — adds XRSocketInteractable + trigger collider to every peg
    ///   6. Click "Setup Attach Points on Flasks" — adds AttachPoint child at neck + wires XRGrabInteractable
    ///   7. Click "Auto-Wire Nearest Flask → Socket" — pairs each flask to the closest peg socket
    /// </summary>
    public class RackSocketSetupTool : EditorWindow
    {
        // ── Inspector-like fields ─────────────────────────────────────────────

        private GameObject  rackRoot;
        private string      pegNameFilter     = "Peg";      // substring match — case-insensitive
        private float       socketRadius      = 0.06f;      // trigger sphere size on peg
        private Vector3     attachOffset      = new Vector3(0f, 0.05f, 0f); // neck offset from flask pivot
        private bool        showFlasks        = true;

        private SerializedObject   serializedWindow;
        private Vector2            scrollPos;

        // Manually listed flasks (drag into list)
        private GameObject[] flasks = new GameObject[0];
        private SerializedProperty flasksProp;

        // ── Menu entry ────────────────────────────────────────────────────────

        [MenuItem("Tools/Rack Socket Setup Tool")]
        public static void ShowWindow()
        {
            var w = GetWindow<RackSocketSetupTool>("Rack Socket Setup");
            w.minSize = new Vector2(380, 520);
        }

        private void OnEnable()
        {
            serializedWindow = new SerializedObject(this);
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            GUILayout.Label("ChemLab — Rack Socket Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Rack ──
            EditorGUILayout.LabelField("Step 1 — Rack", EditorStyles.boldLabel);
            rackRoot      = (GameObject)EditorGUILayout.ObjectField("Rack Root", rackRoot, typeof(GameObject), true);
            pegNameFilter = EditorGUILayout.TextField("Peg Name Filter (substring)", pegNameFilter);
            socketRadius  = EditorGUILayout.FloatField("Socket Trigger Radius", socketRadius);

            EditorGUI.BeginDisabledGroup(rackRoot == null);
            if (GUILayout.Button("1 · Setup Sockets on All Pegs"))
                SetupSocketsOnPegs();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // ── Flasks ──
            EditorGUILayout.LabelField("Step 2 — Flasks / Tubes", EditorStyles.boldLabel);
            attachOffset = EditorGUILayout.Vector3Field("Attach Point Offset (from pivot)", attachOffset);

            // Draw a manual array field for flasks
            serializedWindow.Update();
            var fProp = serializedWindow.FindProperty("flasks");
            EditorGUILayout.PropertyField(fProp, new GUIContent("Flasks / Tubes"), true);
            serializedWindow.ApplyModifiedProperties();

            EditorGUI.BeginDisabledGroup(flasks == null || flasks.Length == 0);
            if (GUILayout.Button("2 · Setup Attach Points on Flasks"))
                SetupAttachPoints();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // ── Auto-wire ──
            EditorGUILayout.LabelField("Step 3 — Auto-Wire", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Pairs each flask to the nearest peg socket and sets it as the " +
                "Starting Selected Interactable so it begins snapped at runtime.",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(rackRoot == null || flasks == null || flasks.Length == 0);
            if (GUILayout.Button("3 · Auto-Wire Nearest Flask → Socket"))
                AutoWire();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // ── Helpers ──
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            if (GUILayout.Button("Select All Pegs in Scene"))
                SelectAllPegs();

            EditorGUILayout.EndScrollView();
        }

        // ── Step 1 — Sockets on pegs ─────────────────────────────────────────

        private void SetupSocketsOnPegs()
        {
            int count = 0;
            foreach (Transform t in rackRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject == rackRoot) continue;
                if (!t.name.ToLower().Contains(pegNameFilter.ToLower())) continue;

                // ── Create or reuse SocketPoint child ──
                Transform socketPoint = t.Find("SocketPoint");
                if (socketPoint == null)
                {
                    var sp = new GameObject("SocketPoint");
                    Undo.RegisterCreatedObjectUndo(sp, "Create SocketPoint");
                    sp.transform.SetParent(t, false);
                    // Position the socket at the tip of the peg along its local Z axis
                    sp.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                    sp.transform.localRotation = Quaternion.identity;
                    socketPoint = sp.transform;
                }

                // ── Trigger collider on the child ──
                var sphere = socketPoint.GetComponent<SphereCollider>();
                if (sphere == null)
                    sphere = Undo.AddComponent<SphereCollider>(socketPoint.gameObject);

                sphere.isTrigger = true;
                sphere.radius    = socketRadius;
                sphere.center    = Vector3.zero;

                // ── XR Socket Interactable on the child ──
                var socket = socketPoint.GetComponent<XRSocketInteractor>();
                if (socket == null)
                    socket = Undo.AddComponent<XRSocketInteractor>(socketPoint.gameObject);

                socket.enabled = true;

                count++;
                EditorUtility.SetDirty(socketPoint.gameObject);
            }

            Debug.Log($"[RackSocketSetup] Created SocketPoint children with sockets on {count} peg(s) under '{rackRoot.name}'.");
        }

        // ── Step 2 — Attach Points on flasks ─────────────────────────────────

        private void SetupAttachPoints()
        {
            int count = 0;
            foreach (var flask in flasks)
            {
                if (flask == null) continue;

                var grab = flask.GetComponent<XRGrabInteractable>();
                if (grab == null)
                {
                    Debug.LogWarning($"[RackSocketSetup] '{flask.name}' has no XRGrabInteractable — skipped.");
                    continue;
                }

                // Only create AttachPoint if one doesn't already exist
                Transform existing = flask.transform.Find("AttachPoint");
                if (existing == null)
                {
                    var ap = new GameObject("AttachPoint");
                    Undo.RegisterCreatedObjectUndo(ap, "Create AttachPoint");
                    ap.transform.SetParent(flask.transform, false);
                    ap.transform.localPosition = attachOffset;
                    ap.transform.localRotation = Quaternion.identity;
                    existing = ap.transform;
                }

                Undo.RecordObject(grab, "Set Attach Transform");
                grab.attachTransform = existing;
                EditorUtility.SetDirty(grab);
                count++;
            }

            Debug.Log($"[RackSocketSetup] Set up AttachPoints on {count} flask(s).");
        }

        // ── Step 3 — Auto-wire nearest flask to each socket ──────────────────

        private void AutoWire()
        {
            // Collect all sockets under rack — now on SocketPoint children
            var sockets = rackRoot.GetComponentsInChildren<XRSocketInteractor>(true);
            if (sockets.Length == 0)
            {
                Debug.LogWarning("[RackSocketSetup] No XRSocketInteractable found — run Step 1 first.");
                return;
            }

            int wired = 0;

            foreach (var socket in sockets)
            {
                // Find the flask whose current world position is closest to this SocketPoint
                GameObject nearest     = null;
                float      nearestDist = float.MaxValue;

                foreach (var flask in flasks)
                {
                    if (flask == null) continue;
                    float d = Vector3.Distance(flask.transform.position, socket.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest     = flask;
                    }
                }

                if (nearest == null) continue;

                var grab = nearest.GetComponent<XRGrabInteractable>();
                if (grab == null) continue;

                Undo.RecordObject(socket, "Wire Starting Interactable");
                socket.startingSelectedInteractable = grab;
                EditorUtility.SetDirty(socket);

                Debug.Log($"[RackSocketSetup] '{nearest.name}' → '{socket.transform.parent.name}/SocketPoint' (dist {nearestDist:F3}m)");
                wired++;
            }

            Debug.Log($"[RackSocketSetup] Auto-wired {wired} flask(s) to socket(s).");
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private void SelectAllPegs()
        {
            if (rackRoot == null) return;
            var found = new System.Collections.Generic.List<Object>();
            foreach (Transform t in rackRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject == rackRoot) continue;
                if (t.name.ToLower().Contains(pegNameFilter.ToLower()))
                    found.Add(t.gameObject);
            }
            Selection.objects = found.ToArray();
            Debug.Log($"[RackSocketSetup] Selected {found.Count} peg(s).");
        }
    }
}
