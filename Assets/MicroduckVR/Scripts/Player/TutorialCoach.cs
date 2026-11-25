using System;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Player
{
    /// <summary>
    /// How to play: on the first run a comic card walks you through the controls one at a time, moving on as soon
    /// as you have done each thing (or after a while, so nobody gets stuck). HELP in the toy bag shows the whole
    /// control sheet again; Y closes it. The card floats low in front of you and lazily follows your view.
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public Camera cam;
        public Font font;
        const string DoneKey = "microduck_tutorial_done_v1";

        class Step { public string pic = "pads"; public string title, body; public Func<bool> done; public float max = 60f; public Action enter; }
        Step[] steps;
        int index = -1; float stepT, holdT; bool help;
        Transform picture; Material picMat;
        Transform root, card; TextMeshPro title, body, counter; TMP_FontAsset fa;
        PlayerDuck pd; PlayerSkates sk; BagMenu bag;
        int snap0, chirp0, grab0, view0; float walk0; bool bagWasOpen, sawBag, laserSeen;
        InputAction skipA;

        static TutorialCoach inst;
        public static void ShowHelp() { if (inst != null) inst.OpenHelp(); }
        public static void ShowSheet(string title, string text, string footer) { if (inst != null) { inst.index = -1; inst.help = true; inst.Show(title, text, footer); } }
        public const string CreditsText =
            "MICRODUCK LAB VR\nMADE BY YUTONG JIN\n\n" +
            "ROBOT: POLLEN ROBOTICS\n(CC BY-NC-SA 4.0)\n" +
            "FONT: BANGERS, VERNON ADAMS (OFL)\n" +
            "BUILT WITH UNITY, URP, OPENXR\n" +
            "HOUSE, TOYS AND SOUNDS: MADE IN CODE\n" +
            "THANKS FOR PLAYING!";
        public static void Restart() { if (inst != null) inst.Begin(); }
        public static void HideAll() { if (inst != null) { inst.help = false; inst.index = -1; inst.card.gameObject.SetActive(false); } }

        void Start()
        {
            inst = this;
            pd = GetComponent<PlayerDuck>(); bag = GetComponent<BagMenu>();
            if (cam == null) cam = Camera.main;
            if (font == null && bag != null) font = bag.font;
            if (font != null) fa = TMP_FontAsset.CreateFontAsset(font);
            skipA = new InputAction("TutorialSkip", InputActionType.Button); skipA.AddBinding("<Keyboard>/escape"); skipA.Enable();
            BuildCard();
            steps = new[]
            {
                new Step { title = "HI, LITTLE DUCK!", body = "YOU ARE THE DUCK.\nLOOK AROUND THE GIANT HOUSE!", max = 6f, done = () => stepT > 5f },
                new Step { pic = "walk", title = "WADDLE", body = "PUSH THE LEFT STICK TO WALK\nTOWARDS WHERE YOU LOOK.", done = () => pd.Walked - walk0 > 0.35f },
                new Step { pic = "turn", title = "TURN", body = "FLICK THE RIGHT STICK\nLEFT OR RIGHT TO TURN.", done = () => pd.SnapCount > snap0 },
                new Step { pic = "beak", title = "BEAK", body = "HOLD THE RIGHT TRIGGER TO OPEN YOUR BEAK.\nLET GO NEXT TO A TOY TO GRAB IT.\nSQUEEZE AGAIN TO DROP IT.", max = 90f,
                           done = () => pd.GrabCount > grab0, enter = SpawnPracticeToy },
                new Step { pic = "chirp", title = "QUACK!", body = "PRESS A TO CHIRP.", done = () => pd.ChirpCount > chirp0 },
                new Step { pic = "bag", title = "TOY BAG", body = "PRESS Y TO OPEN YOUR TOY BAG.\nPOINT WITH THE RIGHT HAND, PULL THE TRIGGER\nTO PICK A TOY, AGAIN TO DROP IT. Y TO CLOSE.", max = 90f,
                           done = () => sawBag && !BagMenu.Busy },
                new Step { pic = "skates", title = "ROLLER SKATES", body = "PRESS B TO PUT ON SKATES.\nLEFT GRIP: DRIFT.  LEFT TRIGGER: BOOST.\nB AGAIN TO TAKE THEM OFF.", done = () => sk != null && sk.On && stepT > 4f },
                new Step { pic = "laser", title = "LASER DOT", body = "HOLD THE RIGHT GRIP FOR A LASER DOT.\nTHE OTHER DUCKS LOVE CHASING IT!", done = () => laserSeen && stepT > 2f },
                new Step { pic = "view", title = "THIRD PERSON", body = "CLICK THE LEFT STICK TO SEE YOURSELF.\nTHERE THE RIGHT STICK MOVES YOUR HEAD,\nRIGHT STICK CLICK PUTS THE VIEW BEHIND YOU.", done = () => pd.ViewToggles > view0 && stepT > 3f },
                new Step { title = "YOU'RE READY!", body = "FOOTBALL, RACES, DANCES AND MORE\nARE IN THE TOY BAG. HELP IS THERE TOO.", max = 7f, done = () => stepT > 6.5f },
            };
            bool done = false; try { done = PlayerPrefs.GetInt(DoneKey, 0) == 1; } catch { }
            if (!done) Invoke(nameof(Begin), 1.5f);
        }

        void OnDestroy() { skipA?.Disable(); if (inst == this) inst = null; }

        public void Begin() { help = false; Go(0); }

        void Go(int i)
        {
            index = i; stepT = 0f;
            if (index >= steps.Length) { Finish(); return; }
            snap0 = pd.SnapCount; chirp0 = pd.ChirpCount; grab0 = pd.GrabCount; view0 = pd.ViewToggles; walk0 = pd.Walked; sawBag = false; laserSeen = false;
            steps[index].enter?.Invoke();
            Show(steps[index].title, steps[index].body, $"{index + 1}/{steps.Length}");
            SetPicture(steps[index].pic);
        }

        void Finish()
        {
            index = -1; card.gameObject.SetActive(false);
            try { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); } catch { }
        }

        void OpenHelp()
        {
            index = -1; help = true;
            Show("HOW TO PLAY",
                 "LEFT STICK: WALK      RIGHT STICK: TURN\n" +
                 "RIGHT TRIGGER: BEAK (GRAB / DROP)      A: CHIRP\n" +
                 "Y: TOY BAG      B: ROLLER SKATES      X: DANCE\n" +
                 "SKATES: LEFT GRIP DRIFT, LEFT TRIGGER BOOST\n" +
                 "RIGHT GRIP: LASER DOT\n" +
                 "LEFT STICK CLICK: FIRST / THIRD PERSON\n" +
                 "THIRD PERSON: RIGHT STICK MOVES YOUR HEAD,\nRIGHT STICK CLICK PUTS THE VIEW BEHIND YOU",
                 "Y TO CLOSE");
        }

        void SpawnPracticeToy()
        {
            // a rubber duck right in front, so there is something to grab
            var f = cam.transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            var p = pd.Body.Pos + f * 0.14f;
            p.y = WorldQueries.GroundNear(p.x, p.z, pd.Body.Pos.y);
            ToyCatalog.Spawn("toy_duck", p, Mathf.Atan2(-f.x, -f.z) * Mathf.Rad2Deg);
        }

        // ------------------------------------------------------------------ the card
        void BuildCard()
        {
            root = new GameObject("Tutorial").transform;
            card = new GameObject("card").transform; card.SetParent(root, false);
            var slab = new GameObject("slab"); slab.transform.SetParent(card, false); slab.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            slab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(0.3f, 0.115f, 0.004f), new ComicMesh.Style { radius = 0.01f, segments = 3 });
            var m = ComicLook.ToonMaterial(ComicLook.Hex("#FFFDF5"), "tutorial_card"); m.SetFloat("_Unlit", 1f);
            var mr = slab.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tab = new GameObject("tab"); tab.transform.SetParent(card, false); tab.transform.localPosition = new Vector3(0f, 0.047f, 0.002f);
            tab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(0.3f, 0.02f, 0.004f), new ComicMesh.Style { radius = 0.006f, segments = 3 });
            var tm = ComicLook.ToonMaterial(ComicLook.Hex("#FFC43D"), "tutorial_tab"); tm.SetFloat("_Unlit", 1f);
            var tr = tab.AddComponent<MeshRenderer>(); tr.sharedMaterial = tm; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            title = Text(0.2f, new Vector3(0f, 0.047f, -0.001f), ComicLook.Hex("#FF5D73"));
            body = Text(0.055f, new Vector3(0.055f, -0.008f, -0.001f), ComicLook.Ink);
            body.textWrappingMode = TextWrappingModes.Normal; body.enableAutoSizing = true; body.fontSizeMin = 0.04f; body.fontSizeMax = 0.11f; body.rectTransform.sizeDelta = new Vector2(0.18f, 0.08f); body.lineSpacing = -10f;
            counter = Text(0.08f, new Vector3(0.125f, -0.05f, -0.001f), ComicLook.Hex("#97A0AE"));
            counter.alignment = TextAlignmentOptions.Right; counter.rectTransform.sizeDelta = new Vector2(0.05f, 0.01f);
            // the comic drawing of the two controllers, the buttons to press painted yellow and ringed in red
            var pq = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(pq.GetComponent<Collider>());
            pq.name = "controllers"; picture = pq.transform; picture.SetParent(card, false);
            picture.localPosition = new Vector3(-0.072f, -0.008f, -0.002f); picture.localScale = new Vector3(0.135f, 0.0886f, 1f);
            picMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            picMat.SetFloat("_AlphaClip", 1f); picMat.SetFloat("_Cutoff", 0.4f); picMat.EnableKeyword("_ALPHATEST_ON");
            var pmr = pq.GetComponent<MeshRenderer>(); pmr.sharedMaterial = picMat; pmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            card.gameObject.SetActive(false);
        }

        TextMeshPro Text(float size, Vector3 at, Color c)
        {
            var go = new GameObject("text"); go.transform.SetParent(card, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); if (fa != null) t.font = fa;
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(0.19f, 0.02f);
            return t;
        }

        void SetPicture(string name)
        {
            var tex = Resources.Load<Texture2D>("Tutorial/pads_" + name);
            if (tex != null) { picMat.SetTexture("_BaseMap", tex); picMat.mainTexture = tex; }
        }

        void Show(string t, string b, string c)
        {
            title.text = t; body.text = b; counter.text = c;
            body.fontSizeMax = b.Split('\n').Length > 4 ? 0.075f : 0.11f;
            // the help sheet uses the whole card for text; steps show the controllers on the left
            bool sheet = help;
            picture.gameObject.SetActive(!sheet);
            body.transform.localPosition = sheet ? new Vector3(0f, -0.006f, -0.001f) : new Vector3(0.073f, -0.008f, -0.001f);
            body.rectTransform.sizeDelta = sheet ? new Vector2(0.28f, 0.085f) : new Vector2(0.145f, 0.08f);
            card.gameObject.SetActive(true);
            card.localScale = Vector3.one * 0.3f; holdT = 0f;
            Place(true);
        }

        void Place(bool snap)
        {
            if (cam == null) return;
            var f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            var want = cam.transform.position + f * 0.3f + Vector3.down * 0.07f;
            float angle = Vector3.Angle(root.position - cam.transform.position, f);
            if (snap || Vector3.Distance(root.position, want) > 0.5f) root.position = want;
            else if (angle > 30f || Vector3.Distance(root.position, want) > 0.1f) root.position = Vector3.Lerp(root.position, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
            root.rotation = Quaternion.LookRotation(root.position - cam.transform.position, Vector3.up);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (sk == null && pd != null) sk = pd.Skates;
            if (!card.gameObject.activeSelf) return;
            Place(false);
            holdT += Time.deltaTime;
            card.localScale = Vector3.one * (holdT < 0.2f ? Mathf.SmoothStep(0.3f, 1f, holdT / 0.2f) : 1f);
            if (skipA.WasPressedThisFrame()) { if (help) { help = false; card.gameObject.SetActive(false); } else Finish(); return; }
            if (help) return;
            if (index < 0) return;
            stepT += Time.deltaTime;
            if (BagMenu.Busy) sawBag = true;
            if (LaserPointer.Active) laserSeen = true;
            var s = steps[index];
            if (s.done() || stepT > s.max) Go(index + 1);
        }

        /// <summary>The help sheet closes when the bag is opened (Y) again.</summary>
        void Update()
        {
            if (help && BagMenu.Busy) { help = false; card.gameObject.SetActive(false); var b = GetComponent<BagMenu>(); if (b != null) b.Close(); }
        }
    }
}
