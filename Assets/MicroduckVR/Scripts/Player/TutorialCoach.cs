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
            public bool silent;                                                  // no card: the step is on a board of its own (the mirror)
            public float max = float.PositiveInfinity;                           // only the talking cards time out
            public Func<bool> lost; public int back = 1;                         // precondition gone: step back
            public Vector2 offset;                                               // card shift (right, up) so it never covers what you work on
            public Func<Vector3?> focus;                                         // ... measured from the direction of this thing, not of your nose:
                                                                                 // a card beside the mirror must not run away when you turn to read it
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
        public static string DebugTitle => inst == null ? "(none)" : inst.card.gameObject.activeSelf ? inst.title.text + " [" + inst.counter.text + "]"
            : inst.index >= 0 && inst.index < inst.steps.Length && inst.steps[inst.index].silent ? inst.steps[inst.index].title + " [board]" : "(none)";
        public static void Restart() { if (inst != null) inst.Begin(); }
        /// <summary>Editor checks: jump straight to a step.</summary>
        public static void DebugGo(int i) { if (inst != null) { inst.help = false; inst.Go(i); } }
        /// <summary>The card (kept out of the mirror's reflection).</summary>
        public static Transform Root => inst != null ? inst.root : null;
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
                // first your look, at the mirror, with only the mirror's own board in view; the lessons start after DONE!
                new Step { title = "MAKE YOUR DUCK", silent = true, enter = ToMirror, done = () => MicroduckVR.Custom.MirrorStation.SaveCount > save0 },
                new Step { title = "LOOKING GOOD!", body = "YOU ARE THE DUCK.\nNOW LET'S LEARN TO GET AROUND!", max = 4.5f, done = () => stepT > 4f },
                new Step { pic = "stick_l", seq = Hold, title = "WADDLE", body = "PUSH THE LEFT STICK TO WALK\nTOWARDS WHERE YOU LOOK.\nGO FOR A LITTLE WALK!", done = () => pd.Walked - walk0 > 0.4f },
                new Step { pic = "stick_r", seq = LeftRight, title = "TURN", body = "FLICK THE RIGHT STICK\nLEFT OR RIGHT TO TURN ROUND.", done = () => pd.SnapCount > snap0 || pd.Turned - turn0 > 40f },
                new Step { pic = "trigger_r", seq = Hold, title = "OPEN YOUR BEAK", body = "HOLD THE RIGHT TRIGGER:\nYOUR BEAK OPENS. KEEP IT OPEN!", enter = SpawnPracticeToy,
                           done = () => { heldT = pd.Body.JawTarget > 0.3f ? heldT + Time.deltaTime : 0f; return heldT > 0.7f; } },
                new Step { pic = "trigger_r", seq = LetGo, title = "GRAB IT", body = "WALK UP TO THE RUBBER DUCK.\nHOLD THE TRIGGER, PUT YOUR BEAK ON IT,\nTHEN LET GO TO BITE!", done = () => pd.Held != null },
                new Step { pic = "trigger_r", seq = Press, title = "DROP IT", body = "CARRY IT WHEREVER YOU LIKE.\nSQUEEZE THE TRIGGER AGAIN TO DROP IT.", done = () => pd.DropCount > drop0, lost = () => pd.Held == null && pd.DropCount == drop0 },
                new Step { pic = "a", seq = Press, title = "QUACK!", body = "PRESS A TO QUACK.", done = () => pd.ChirpCount > chirp0 },
                new Step { pic = "y", seq = Press, title = "TOY BAG", body = "PRESS Y TO OPEN YOUR TOY BAG.", done = () => bag != null && bag.State == BagMenu.Mode.Panel },
                new Step { pic = "trigger_r", seq = Press, title = "PICK A TOY", body = "POINT YOUR RIGHT HAND AT A TOY\nAND PULL THE TRIGGER.", offset = new Vector2(-0.32f, 0.03f), focus = () => bag != null && bag.State == BagMenu.Mode.Panel ? bag.PanelPos : (Vector3?)null,
                           done = () => bag.State == BagMenu.Mode.Place, lost = () => bag.State == BagMenu.Mode.Closed },
                new Step { pic = "trigger_r", seq = Press, title = "PUT IT DOWN", offset = new Vector2(0f, 0.15f), body = "POINT AT THE FLOOR NEAR YOU.\nPULL THE TRIGGER TO DROP THE TOY THERE.",
                           done = () => bag.PlacedCount > placed0, lost = () => bag.State != BagMenu.Mode.Place, back = 2 },
                new Step { pic = "y", seq = Press, title = "CLOSE THE BAG", body = "PRESS Y TO PUT THE BAG AWAY.", done = () => !BagMenu.Busy },
                new Step { pic = "b", seq = Press, title = "ROLLER SKATES", body = "PRESS B TO PUT ON YOUR SKATES.", done = () => sk != null && sk.On },
                new Step { pic = "stick_l", seq = Hold, title = "ROLL!", offset = new Vector2(0f, 0.15f), body = "PUSH THE LEFT STICK TO SKATE.\nTAKE A LAP!", done = () => pd.Walked - walk0 > 1.5f, lost = () => !sk.On },
                new Step { pic = "trigger_l", seq = Press, title = "BOOST", offset = new Vector2(0f, 0.15f), body = "PULL THE LEFT TRIGGER\nFOR A BURST OF SPEED.", done = () => sk.Boosting, lost = () => !sk.On, back = 2 },
                new Step { pic = "drift", seq = Hold, title = "DRIFT", offset = new Vector2(0f, 0.15f), body = "GET SOME SPEED, THEN HOLD THE LEFT GRIP\nAND STEER TO SLIDE ROUND A CORNER.", done = () => sk.Drifting, lost = () => !sk.On, back = 3, max = 75f },
                new Step { pic = "b", seq = Press, title = "SKATES OFF", body = "PRESS B AGAIN TO TAKE THEM OFF.", done = () => !sk.On },
                new Step { pic = "grip_r", seq = Hold, title = "LASER DOT", offset = new Vector2(0f, 0.15f), body = "HOLD THE RIGHT GRIP AND POINT AT THE FLOOR.\nTHE OTHER DUCKS LOVE CHASING THE DOT!",
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
            if (MicroduckVR.Custom.MirrorStation.Instance != null) MicroduckVR.Custom.MirrorStation.Instance.OpenBoard(true);
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
            if (steps[index].silent) { card.gameObject.SetActive(false); return; }
            Show(steps[index].title, steps[index].body.Replace('\n', ' '), $"{index + 1}/{steps.Length}");
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

        static Vector3? AtMirror() => MicroduckVR.Custom.MirrorStation.Centre;

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
            // two backs: the step card (picture + a few words) and the taller sheet (help, credits). Everything on them is
            // sized to read in the headset from where the card floats: body text about 1.5 degrees of letter height or more
            slabStep = Back(new Vector2(CW, CH)); slabSheet = Back(new Vector2(SW, SH));
            title = Text(0.24f, Vector3.zero, ComicLook.Ink);
            body = Text(12f, Vector3.zero, ComicLook.Ink);
            body.textWrappingMode = TextWrappingModes.Normal; body.enableAutoSizing = true; body.fontSizeMin = 8f; body.fontSizeMax = 17f; body.lineSpacing = -8f;
            counter = Text(0.1f, Vector3.zero, ComicLook.Hex("#5B6472"));
            counter.alignment = TextAlignmentOptions.Right; counter.rectTransform.sizeDelta = new Vector2(0.14f, 0.014f);
            // the comic drawing of the two controllers, the buttons to press painted yellow and ringed in red
            var pq = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(pq.GetComponent<Collider>());
            pq.name = "controllers"; picture = pq.transform; picture.SetParent(card, false);
            picture.localPosition = new Vector3(-0.09f, -0.016f, -0.002f); picture.localScale = new Vector3(0.15f, 0.0984f, 1f);
            picMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            picMat.SetFloat("_AlphaClip", 1f); picMat.SetFloat("_Cutoff", 0.4f); picMat.EnableKeyword("_ALPHATEST_ON");
            var pmr = pq.GetComponent<MeshRenderer>(); pmr.sharedMaterial = picMat; pmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            card.gameObject.SetActive(false);
        }

        const float CW = 0.34f, CH = 0.15f, SW = 0.37f, SH = 0.27f, TabH = 0.032f;
        Transform slabStep, slabSheet;
        /// <summary>A cream card with a yellow title strip across its top.</summary>
        Transform Back(Vector2 size)
        {
            var g = new GameObject("back").transform; g.SetParent(card, false);
            var slab = new GameObject("slab"); slab.transform.SetParent(g, false); slab.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            slab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(size.x, size.y, 0.004f), new ComicMesh.Style { radius = 0.01f, segments = 3 });
            var m = ComicLook.ToonMaterial(ComicLook.Hex("#FFFDF5"), "tutorial_card"); m.SetFloat("_Unlit", 1f);
            var mr = slab.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tab = new GameObject("tab"); tab.transform.SetParent(g, false); tab.transform.localPosition = new Vector3(0f, size.y / 2f - TabH / 2f, 0.002f);
            tab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(size.x, TabH, 0.004f), new ComicMesh.Style { radius = 0.006f, segments = 3 });
            var tm = ComicLook.ToonMaterial(ComicLook.Hex("#FFC43D"), "tutorial_tab"); tm.SetFloat("_Unlit", 1f);
            var tr = tab.AddComponent<MeshRenderer>(); tr.sharedMaterial = tm; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
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
            // the help sheet uses a taller card, all of it for text; steps show the controllers on the left
            bool sheet = help;
            float w = sheet ? SW : CW, h = sheet ? SH : CH;
            slabStep.gameObject.SetActive(!sheet); slabSheet.gameObject.SetActive(sheet);
            picture.gameObject.SetActive(!sheet);
            title.transform.localPosition = new Vector3(0f, h / 2f - TabH / 2f - 0.001f, -0.001f); title.rectTransform.sizeDelta = new Vector2(w - 0.02f, TabH);
            body.transform.localPosition = sheet ? new Vector3(0f, -TabH / 2f + 0.006f, -0.001f) : new Vector3(0.082f, -0.014f, -0.001f);
            // TMP's auto-size searches in steps of 0.05 pt, hopeless at these sizes: the body is set at 100x and scaled down
            body.transform.localScale = Vector3.one * 0.01f;
            body.rectTransform.sizeDelta = (sheet ? new Vector2(w - 0.03f, h - TabH - 0.04f) : new Vector2(0.16f, h - TabH - 0.022f)) * 100f;
            body.fontSizeMin = 8f; body.fontSizeMax = sheet ? 20f : 17f;
            counter.fontSize = sheet ? 0.15f : 0.1f;
            counter.transform.localPosition = new Vector3(w / 2f - 0.08f, -h / 2f + (sheet ? 0.014f : 0.011f), -0.001f);
            card.gameObject.SetActive(true);
            card.localScale = Vector3.one * 0.3f; holdT = 0f;
            Place(true);
        }

        float cardYaw, fit = 1f; bool yawGlide; int viewSeen = -1;
        void Place(bool snap)
        {
            if (cam == null) return;
            var eye = cam.transform.position;
            var f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            Step st = !help && index >= 0 && index < steps.Length ? steps[index] : null;
            bool anchored = false;
            if (st != null && st.focus != null)
            {
                var fp = st.focus();
                if (fp.HasValue) { var d = fp.Value - eye; d.y = 0f; if (d.sqrMagnitude > 1e-4f) { f = d.normalized; anchored = true; } }
            }
            // The card rides with your eyes, so it is always the same distance away however fast you skate, but it only
            // turns after you: it stays put while you glance about and swings round once you have turned well away.
            float yawWant = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            if (snap || anchored || viewSeen != pd.ViewToggles) { cardYaw = yawWant; yawGlide = false; viewSeen = pd.ViewToggles; }
            else
            {
                float d = Mathf.DeltaAngle(cardYaw, yawWant);
                if (Mathf.Abs(d) > 32f) yawGlide = true;
                if (yawGlide) { cardYaw = Mathf.LerpAngle(cardYaw, yawWant, 1f - Mathf.Exp(-5f * Time.deltaTime)); if (Mathf.Abs(d) < 2f) yawGlide = false; }
            }
            var dir = Quaternion.Euler(0f, cardYaw, 0f) * Vector3.forward;
            // in third person the duck stands low in the middle of the view: the card sits above it
            var want = eye + dir * (help ? 0.36f : 0.34f) + Vector3.down * (help ? 0.03f : pd.thirdPerson ? 0f : 0.075f);
            // some steps keep the card out of the way of what you are doing (the mirror, the open toy bag, the floor ahead)
            if (st != null) { var o = st.offset; want += Vector3.Cross(Vector3.up, dir) * o.x + Vector3.up * o.y; }
            // never inside a wall or a piece of furniture: come closer to the eyes instead, and shrink to look the same size
            var to = want - eye; float dist = to.magnitude; fit = 1f;
            if (Physics.SphereCast(eye, 0.03f, to / dist, out var hit, dist, MicroduckVR.Duck.WorldQueries.WorldMask, QueryTriggerInteraction.Ignore))
            { float near = Mathf.Max(0.14f, hit.distance - 0.02f); want = eye + to / dist * near; fit = near / dist; }
            root.position = want;
            root.rotation = Quaternion.LookRotation(want - eye, Vector3.up);
            MicroduckVR.Flock.DuckBrain.ClearView(eye, want);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (sk == null && pd != null) sk = pd.Skates;
            bool silent = !help && index >= 0 && index < steps.Length && steps[index].silent;
            if (silent)
            {
                // a step with no card still has to notice when it is done
                stepT += Time.deltaTime;
                if (steps[index].done() || stepT > steps[index].max) Go(index + 1);
                return;
            }
            if (!card.gameObject.activeSelf) return;
            Place(false);
            holdT += Time.deltaTime;
            card.localScale = Vector3.one * (holdT < 0.2f ? Mathf.SmoothStep(0.3f, 1f, holdT / 0.2f) : 1f) * fit;
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
