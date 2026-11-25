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
    /// How to play: on the first run a comic card walks you through the controls one at a time, starting at the
    /// mirror (first you make your duck), each card waiting until you have really done the thing it asks. The
    /// controller picture on the card presses and lets go like a flip-book. Hold the left menu button to skip. HELP in the toy bag shows the whole
    /// control sheet again; Y closes it. The card floats low in front of you and lazily follows your view.
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public Camera cam;
        public Font font;
        const string DoneKey = "microduck_tutorial_done_v2";

        class Step
        {
            public string pic = "pads"; public (int frame, float secs)[] seq;   // the controller flip-book and its timing
            public string title, body; public Func<bool> done; public Action enter;
            public float max = float.PositiveInfinity;                           // only the talking cards time out
            public Func<bool> lost; public int back = 1;                         // precondition gone: step back
            public Vector2 offset;                                               // card shift (right, up) so it never covers what you work on
        }
        static readonly (int, float)[] Press = { (0, 0.55f), (1, 0.45f) }, Hold = { (0, 0.4f), (1, 1.3f) }, LetGo = { (1, 1.0f), (0, 0.8f) },
                                       LeftRight = { (0, 0.35f), (1, 0.5f), (0, 0.35f), (2, 0.5f) };
        Step[] steps;
        int index = -1; float stepT, holdT, animT, heldT; int animI; bool help;
        Transform picture; Material picMat; readonly System.Collections.Generic.Dictionary<string, Texture2D> texCache = new System.Collections.Generic.Dictionary<string, Texture2D>();
        Transform root, card; TextMeshPro title, body, counter; TMP_FontAsset fa;
        PlayerDuck pd; PlayerSkates sk; BagMenu bag;
        int snap0, chirp0, grab0, drop0, view0, save0, placed0; float walk0, turn0, skipT;
        InputAction skipA, skipHoldA;

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
        public static string DebugTitle => inst != null && inst.card.gameObject.activeSelf ? inst.title.text + " [" + inst.counter.text + "]" : "(none)";
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
            skipHoldA = new InputAction("TutorialSkipHold", InputActionType.Button);
            skipHoldA.AddBinding("<XRController>{LeftHand}/menu"); skipHoldA.AddBinding("<XRController>{LeftHand}/menuButton"); skipHoldA.Enable();
            BuildCard();
            // Every step waits until you have really done the thing; nothing moves on by itself except the talking cards.
            steps = new[]
            {
                new Step { title = "HI, LITTLE DUCK!", body = "YOU ARE THE DUCK.\nFIRST, LET'S MAKE YOU LOOK\nTHE WAY YOU LIKE!", max = 4.5f, done = () => stepT > 4f, enter = ToMirror, offset = new Vector2(-0.12f, -0.02f) },
                new Step { pic = "trigger_r", seq = Press, title = "MAKE YOUR DUCK", offset = new Vector2(-0.12f, -0.02f),
                           body = "THAT'S YOU IN THE MIRROR!\nPOINT YOUR RIGHT HAND AT THE BOARD,\nPULL THE TRIGGER TO PICK COLOURS.\nPRESS DONE! WHEN YOU LIKE IT.",
                           done = () => MicroduckVR.Custom.MirrorStation.SaveCount > save0 },
                new Step { pic = "stick_l", seq = Hold, title = "WADDLE", body = "PUSH THE LEFT STICK TO WALK\nTOWARDS WHERE YOU LOOK.\nGO FOR A LITTLE WALK!", done = () => pd.Walked - walk0 > 0.4f },
                new Step { pic = "stick_r", seq = LeftRight, title = "TURN", body = "FLICK THE RIGHT STICK\nLEFT OR RIGHT TO TURN ROUND.", done = () => pd.SnapCount > snap0 || pd.Turned - turn0 > 40f },
                new Step { pic = "trigger_r", seq = Hold, title = "OPEN YOUR BEAK", body = "HOLD THE RIGHT TRIGGER:\nYOUR BEAK OPENS. KEEP IT OPEN!", enter = SpawnPracticeToy,
                           done = () => { heldT = pd.Body.JawTarget > 0.3f ? heldT + Time.deltaTime : 0f; return heldT > 0.7f; } },
                new Step { pic = "trigger_r", seq = LetGo, title = "GRAB IT", body = "WALK UP TO THE RUBBER DUCK.\nHOLD THE TRIGGER, PUT YOUR BEAK ON IT,\nTHEN LET GO TO BITE!", done = () => pd.Held != null },
                new Step { pic = "trigger_r", seq = Press, title = "DROP IT", body = "CARRY IT WHEREVER YOU LIKE.\nSQUEEZE THE TRIGGER AGAIN TO DROP IT.", done = () => pd.DropCount > drop0, lost = () => pd.Held == null && pd.DropCount == drop0 },
                new Step { pic = "a", seq = Press, title = "QUACK!", body = "PRESS A TO QUACK.", done = () => pd.ChirpCount > chirp0 },
                new Step { pic = "y", seq = Press, title = "TOY BAG", body = "PRESS Y TO OPEN YOUR TOY BAG.", done = () => bag != null && bag.State == BagMenu.Mode.Panel },
                new Step { pic = "trigger_r", seq = Press, title = "PICK A TOY", body = "POINT YOUR RIGHT HAND AT A TOY\nAND PULL THE TRIGGER.", offset = new Vector2(-0.31f, 0.04f),
                           done = () => bag.State == BagMenu.Mode.Place, lost = () => bag.State == BagMenu.Mode.Closed },
                new Step { pic = "trigger_r", seq = Press, title = "PUT IT DOWN", body = "POINT AT THE FLOOR NEAR YOU.\nPULL THE TRIGGER TO DROP THE TOY THERE.",
                           done = () => bag.PlacedCount > placed0, lost = () => bag.State != BagMenu.Mode.Place, back = 2 },
                new Step { pic = "y", seq = Press, title = "CLOSE THE BAG", body = "PRESS Y TO PUT THE BAG AWAY.", done = () => !BagMenu.Busy },
                new Step { pic = "b", seq = Press, title = "ROLLER SKATES", body = "PRESS B TO PUT ON YOUR SKATES.", done = () => sk != null && sk.On },
                new Step { pic = "stick_l", seq = Hold, title = "ROLL!", body = "PUSH THE LEFT STICK TO SKATE.\nTAKE A LAP!", done = () => pd.Walked - walk0 > 1.5f, lost = () => !sk.On },
                new Step { pic = "trigger_l", seq = Press, title = "BOOST", body = "PULL THE LEFT TRIGGER\nFOR A BURST OF SPEED.", done = () => sk.Boosting, lost = () => !sk.On, back = 2 },
                new Step { pic = "drift", seq = Hold, title = "DRIFT", body = "GET SOME SPEED, THEN HOLD THE LEFT GRIP\nAND STEER TO SLIDE ROUND A CORNER.", done = () => sk.Drifting, lost = () => !sk.On, back = 3, max = 75f },
                new Step { pic = "b", seq = Press, title = "SKATES OFF", body = "PRESS B AGAIN TO TAKE THEM OFF.", done = () => !sk.On },
                new Step { pic = "grip_r", seq = Hold, title = "LASER DOT", body = "HOLD THE RIGHT GRIP AND POINT AT THE FLOOR.\nTHE OTHER DUCKS LOVE CHASING THE DOT!",
                           done = () => { heldT = LaserPointer.Active ? heldT + Time.deltaTime : heldT; return heldT > 1.5f; } },
                new Step { pic = "click_l", seq = Press, title = "SEE YOURSELF", body = "CLICK THE LEFT STICK IN\nTO STEP OUT AND SEE YOUR DUCK.", done = () => pd.thirdPerson },
                new Step { pic = "stick_r", seq = LeftRight, title = "LOOK AROUND", body = "OUT HERE THE RIGHT STICK\nMOVES YOUR DUCK'S HEAD. TRY IT!", done = () => pd.HeadAimAmount > 0.6f, lost = () => !pd.thirdPerson },
                new Step { pic = "click_l", seq = Press, title = "BACK INSIDE", body = "CLICK THE LEFT STICK AGAIN\nTO SEE THROUGH YOUR DUCK'S EYES.", done = () => !pd.thirdPerson },
                new Step { title = "YOU'RE READY!", body = "FOOTBALL, RACES, DANCES, MORE DUCKS\nAND HELP ARE ALL IN THE TOY BAG (Y).", max = 7f, done = () => stepT > 6.5f },
            };
            bool done = false; try { done = PlayerPrefs.GetInt(DoneKey, 0) == 1; } catch { }
            if (!done) Invoke(nameof(Begin), 1.5f);
        }

        /// <summary>The tutorial opens at the mirror, so the first thing you do is choose your duck's look.</summary>
        void ToMirror()
        {
            pd.SetThirdPerson(false);
            pd.Teleport(MicroduckVR.Custom.MirrorStation.StandPos, Mathf.PI);
            pd.FaceView(Mathf.PI);
        }

        void OnDestroy() { skipA?.Disable(); skipHoldA?.Disable(); if (inst == this) inst = null; }

        public void Begin() { help = false; Go(0); }

        void Go(int i)
        {
            index = Mathf.Max(0, i); stepT = 0f; heldT = 0f;
            if (index >= steps.Length) { Finish(); return; }
            snap0 = pd.SnapCount; chirp0 = pd.ChirpCount; grab0 = pd.GrabCount; drop0 = pd.DropCount; view0 = pd.ViewToggles; walk0 = pd.Walked; turn0 = pd.Turned;
            save0 = MicroduckVR.Custom.MirrorStation.SaveCount; placed0 = bag != null ? bag.PlacedCount : 0;
            steps[index].enter?.Invoke();
            Show(steps[index].title, steps[index].body, $"{index + 1}/{steps.Length}");
            animI = 0; animT = 0f; SetFrame();
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

        /// <summary>Shows the current frame of the step's controller flip-book (rest, pressed, rest...).</summary>
        void SetFrame()
        {
            var st = steps[index];
            string name = st.seq == null ? "pads_" + st.pic + "_0" : "pads_" + st.pic + "_" + st.seq[animI % st.seq.Length].frame;
            if (!texCache.TryGetValue(name, out var tex)) { tex = Resources.Load<Texture2D>("Tutorial/" + name); texCache[name] = tex; }
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
            // some steps keep the card out of the way of what you are doing (the mirror board, the open toy bag)
            if (!help && index >= 0 && index < steps.Length) { var o = steps[index].offset; want += Vector3.Cross(Vector3.up, f) * o.x + Vector3.up * o.y; }
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
            // holding the left menu button for a moment skips the whole tutorial
            skipT = skipHoldA.IsPressed() ? skipT + Time.deltaTime : 0f;
            if (skipT > 1.2f) { skipT = 0f; Finish(); return; }
            stepT += Time.deltaTime;
            var s = steps[index];
            // the picture presses and lets go
            if (s.seq != null)
            {
                animT += Time.deltaTime;
                if (animT >= s.seq[animI % s.seq.Length].secs) { animT = 0f; animI = (animI + 1) % s.seq.Length; SetFrame(); }
            }
            if (s.done() || stepT > s.max) { Go(index + 1); return; }
            if (s.lost != null && stepT > 0.5f && s.lost()) Go(index - s.back);
        }

        /// <summary>The help sheet closes when the bag is opened (Y) again.</summary>
        void Update()
        {
            if (help && BagMenu.Busy && holdT > 0.3f) { help = false; card.gameObject.SetActive(false); var b = GetComponent<BagMenu>(); if (b != null) b.Close(); }
        }
    }
}
