using System.Collections.Generic;
using System.Text.RegularExpressions;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Player;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Custom
{
    /// <summary>
    /// The garage corner (port of garage.js): step onto the turntable and the view goes to third person, with a
    /// workbench board listing your duck's replaceable parts in three tabs (shell, legs, servos). Each part wears
    /// with the distance you walk; pick one to see it flash on your duck, REPLACE pops the old one off and the new
    /// one flashes in like new.
    /// </summary>
    public class Garage : MonoBehaviour
    {
        public static Garage Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        static readonly Vector3 Turntable = new Vector3(-2.2f, 0f, 1.78f);

        class PartDef { public string id, label, tab; public Regex re; public float rate; }
        static readonly List<PartDef> Parts = new List<PartDef>();
        static void Def(string id, string label, string tab, string re, float rate) => Parts.Add(new PartDef { id = id, label = label, tab = tab, re = new Regex(re, RegexOptions.IgnoreCase), rate = rate });
        static Garage()
        {
            Def("head", "HEAD SHELL", "SHELL", "^shell_head_(top|face)", 2f); Def("bodyL", "BODY SHELL L", "SHELL", "^shell_body_left", 3f);
            Def("bodyR", "BODY SHELL R", "SHELL", "^shell_body_right", 3f); Def("eye", "EYE / CAMERA", "SHELL", "^(eye_camera_ring|electronics_camera_lens)", 1f);
            Def("beak", "BEAK", "SHELL", "^(shell_beak|shell_head_bottom)", 4f);
            Def("legL", "LEFT LEG", "LEGS", "^(leg_left|foot_left)", 8f); Def("legR", "RIGHT LEG", "LEGS", "^(leg_right|foot_right)", 8f);
            string[] sv = { "left_hip_yaw", "left_hip_roll", "left_hip_pitch", "left_knee", "left_ankle", "right_hip_yaw", "right_hip_roll", "right_hip_pitch", "right_knee", "right_ankle", "neck_pitch", "head_pitch", "head_yaw", "head_roll", "jaw" };
            foreach (var n in sv) Def("servo_" + n, n.Replace('_', ' ').ToUpperInvariant(), "SERVOS", "^servo_" + n + "$", n.Contains("knee") || n.Contains("ankle") ? 12f : n == "jaw" ? 6f : 5f);
        }

        public WorldPanel Board => board;
        PlayerDuck pd; WorldPanel board; string tab = "SHELL"; PartDef selected;
        readonly List<(WorldPanel.Button row, Transform fill, PartDef part)> rows = new List<(WorldPanel.Button, Transform, PartDef)>();
        bool inside, wasThird; float flashT = -1f, odoSaved; Material flash;
        const string OdoKey = PrefKeys.Odometer;

        public static void Ensure(PlayerDuck player, TMP_FontAsset font)
        {
            if (Instance != null) return;
            Instance = new GameObject("Garage").AddComponent<Garage>();
            Instance.pd = player; Instance.Build(font);
        }

        static float Odo { get { try { return PlayerPrefs.GetFloat(OdoKey, 0f); } catch { return 0f; } } }
        static float ReplacedAt(string id) { try { return PlayerPrefs.GetFloat(PrefKeys.PartPrefix + id, 0f); } catch { return 0f; } }
        float Condition(PartDef p) => Mathf.Clamp(100f - (Odo + pd.Walked - odoSaved - ReplacedAt(p.id)) * p.rate, 12f, 100f);

        void Build(TMP_FontAsset font)
        {
            flash = ComicLook.ToonMaterial(ComicLook.Hex("#FFF1D6"), "garage_flash"); flash.SetFloat("_Unlit", 1f);
            var go = new GameObject("Garage board"); go.transform.SetParent(transform, false);
            board = go.AddComponent<WorldPanel>(); board.font = font; board.cam = pd.cam; board.reach = 1.2f;
            MirrorStation.Sign(font, "DUCK GARAGE", new Vector3(-2.96f, 0.5f, Turntable.z), Quaternion.LookRotation(Vector3.left, Vector3.up), 1.5f, "#FF8A3D");     // on the wall behind the turntable, facing the room
            var bag = pd.GetComponent<BagMenu>(); if (bag != null) board.rightHand = bag.rightHand;
            // sized to read from where it floats beside your duck on the turntable (see WorldPanel.follow)
            const float bw = 0.44f, bh = 0.4f;
            board.follow = true; board.focus = Turntable; board.readDist = 0.42f; board.sideDeg = 42f; board.minY = bh / 2f + 0.03f;
            board.panelRect = new Rect(-bw / 2f, -bh / 2f, bw, bh);
            board.Slab(new Vector2(bw, bh), "#3D3B4F", new Vector3(0f, 0f, 0.003f), 0.1f);
            board.Slab(new Vector2(bw, 0.04f), "#FFC43D", new Vector3(0f, bh / 2f - 0.02f, 0f), 0.4f);
            board.Label("GARAGE", new Vector3(0f, bh / 2f - 0.021f, -0.004f), 0.22f, "#1B1430", bw);
            int k = 0;
            foreach (var t in new[] { "SHELL", "LEGS", "SERVOS" }) { var tt = t; board.Add("tab_" + t, t, new Vector2(-0.135f + k++ * 0.135f, bh / 2f - 0.064f), new Vector2(0.125f, 0.034f), "#E6E9F0", () => { tab = tt; selected = null; Layout(); }, 0.17f); }
            // up to 16 rows in two columns; each row: name + condition bar
            for (int i = 0; i < 16; i++)
            {
                int col = i / 8, r = i % 8;
                var c = new Vector2(-0.108f + col * 0.216f, bh / 2f - 0.108f + -r * 0.031f);
                var row = board.Add("row_" + i, "", c, new Vector2(0.208f, 0.027f), "#FFFDF5", null, 0.05f);
                var idx = i; row.click = () => Pick(idx);
                row.text = board.Label("", new Vector3(c.x - 0.03f, c.y + 0.001f, -0.0135f), 0.155f, "#1B1430", 0.138f, TextAlignmentOptions.Left);
                board.Slab(new Vector2(0.05f, 0.009f), "#B9C3CF", new Vector3(c.x + 0.072f, c.y, -0.0135f), 0.5f, 0.002f);
                var fill = board.Slab(new Vector2(0.05f, 0.009f), "#8FD35A", new Vector3(c.x + 0.072f, c.y, -0.0155f), 0.5f, 0.002f);
                rows.Add((row, fill, null));
            }
            board.Add("replace", "REPLACE PART", new Vector2(0f, -bh / 2f + 0.03f), new Vector2(0.24f, 0.044f), "#FF8A3D", Replace, 0.22f);
            Layout();
            go.SetActive(false);
        }

        void Layout()
        {
            var list = Parts.FindAll(p => p.tab == tab);
            for (int i = 0; i < rows.Count; i++)
            {
                var (row, fill, _) = rows[i];
                bool on = i < list.Count;
                row.t.gameObject.SetActive(on); row.text.gameObject.SetActive(on); fill.gameObject.SetActive(on);
                fill.parent.GetChild(fill.GetSiblingIndex() - 1).gameObject.SetActive(on);
                rows[i] = (row, fill, on ? list[i] : null);
                if (!on) continue;
                row.text.text = list[i].label;
                float c = Condition(list[i]) / 100f;
                fill.localScale = new Vector3(c, 1f, 1f); fill.localPosition = new Vector3(row.rect.center.x + 0.072f - 0.025f * (1f - c), row.rect.center.y, -0.0155f);
                fill.GetComponent<MeshRenderer>().sharedMaterial = WorldPanel.Unlit(c > 0.6f ? "#8FD35A" : c > 0.3f ? "#FFC43D" : "#FF5D73");
                board.Select(row, list[i] == selected);
            }
            foreach (var b in board.buttons) if (b.id.StartsWith("tab_")) board.Select(b, b.id == "tab_" + tab);
        }

        void Pick(int i) { selected = rows[i].part; flashT = 0f; Layout(); }

        void Replace()
        {
            if (selected == null) return;
            try { PlayerPrefs.SetFloat(PrefKeys.PartPrefix + selected.id, Odo + pd.Walked - odoSaved); PlayerPrefs.Save(); } catch { }
            // the old part pops off: copies of its meshes fly up and away, the real ones flash in as new
            foreach (var r in Renderers(selected))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null) continue;
                var c = new GameObject("old part"); c.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); c.transform.localScale = r.transform.lossyScale;
                c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; c.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                c.AddComponent<PopOff>().v = (Random.insideUnitSphere * 0.15f + Vector3.up * 0.35f);
            }
            flashT = 0f;
            MicroduckVR.Games.GameHud.Get().Big("GOOD AS NEW!", "#FFC43D", 1.1f);
            Layout();
        }

        IEnumerable<Renderer> Renderers(PartDef p)
        {
            var rig = pd.Body.Model.GetChild(0);
            foreach (var r in rig.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (p.re.IsMatch(ComicLook.MeshName(r))) yield return r;
            }
        }

        void Update()
        {
            if (pd == null) return;
            var flat = pd.Body.Pos - Turntable; flat.y = 0f;
            bool on = flat.magnitude < (inside ? 0.28f : 0.2f);        // a little slack, so standing on the edge does not flicker the board
            board.focus = pd.Body.Pos;
            if (on != inside)
            {
                inside = on; board.gameObject.SetActive(on);
                if (on) { wasThird = pd.thirdPerson; if (!wasThird) pd.SetThirdPerson(true); Layout(); }
                else { if (!wasThird && pd.thirdPerson) pd.SetThirdPerson(false); selected = null; RestoreLook(); }
            }
            // a pulsing glow on the selected part
            if (inside && selected != null && flashT >= 0f)
            {
                flashT += Time.deltaTime;
                bool lit = Mathf.Repeat(flashT, 0.5f) < 0.25f;
                foreach (var r in Renderers(selected)) if (lit) r.sharedMaterial = flash;
                if (!lit) RestoreLook();
                if (flashT > 2f) { flashT = -1f; RestoreLook(); }
            }
            // keep the odometer for wear between sessions
            if (pd.Walked - odoSaved > 1f) { try { PlayerPrefs.SetFloat(OdoKey, Odo + (pd.Walked - odoSaved)); } catch { } odoSaved = pd.Walked; }
        }

        void RestoreLook() { DuckCustom.Load().ApplyTo(pd.gameObject, false); }

        class PopOff : MonoBehaviour
        {
            public Vector3 v; float t; Vector3 s0; Vector3 spin;
            void Start() { s0 = transform.localScale; spin = Random.insideUnitSphere * 540f; }
            void Update()
            {
                t += Time.deltaTime; v += Vector3.down * 1.2f * Time.deltaTime;
                transform.position += v * Time.deltaTime; transform.Rotate(spin * Time.deltaTime, Space.World);
                transform.localScale = s0 * Mathf.Clamp01(1f - (t - 0.4f) / 0.4f);
                if (t > 0.8f) Destroy(gameObject);
            }
        }
    }
}
