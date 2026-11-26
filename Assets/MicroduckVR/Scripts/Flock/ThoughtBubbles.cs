using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Flock
{
    /// <summary>
    /// Comic speech bubbles over the AI ducks (port of thoughts.js): when a duck's intent changes, or on an
    /// event, it pops a short first-person line. At most a few are shown (nearest, in view, not behind walls).
    /// Bubbles are world-space slabs (they get ink outlines from the comic pass) with Bangers text, facing you.
    /// </summary>
    public class ThoughtBubbles : MonoBehaviour
    {
        public Font font;
        public Camera viewer;
        public bool showThoughts = true;

        const float SHOW = 3.0f, FADE = 0.35f, REPEAT = 8f, RANGE = 4.0f;
        const int MAX_ON = 4, POOL = 6;

        static readonly Dictionary<AiState, string[]> StateLines = new Dictionary<AiState, string[]>
        {
            { AiState.Wander,   new[] { "Just exploring...", "What's over here?", "La la, wandering." } },
            { AiState.Roam,     new[] { "Off to the {room}!", "Heading to the {room}!", "Visiting the {room}." } },
            { AiState.Follow,   new[] { "Wait for me!", "Right behind you!", "Coming too!" } },
            { AiState.Toy,      new[] { "Ooh, a {prop}!", "That {prop} is mine!", "Mmm, {prop}!" } },
            { AiState.Sit,      new[] { "Taking a break.", "Comfy.", "Sitting pretty." } },
            { AiState.Idle,     new[] { "~ la la ~", "Hmm...", "Nice day." } },
            { AiState.Race,     new[] { "Zoom zoom!", "Catch me!", "Gotta go fast!" } },
            { AiState.Football, new[] { "Pass it!", "My ball!", "Go team!" } },
            { AiState.Laser,    new[] { "Red dot!", "I'll get it!", "Pounce!" } },
        };

        static readonly Dictionary<DuckEventKind, string[]> EventLines = new Dictionary<DuckEventKind, string[]>
        {
            { DuckEventKind.Got,   new[] { "Got it!", "Gotcha!", "Mine now!" } },
            { DuckEventKind.Drop,  new[] { "Here you go!", "For you!", "Ta-da!" } },
            { DuckEventKind.Bed,   new[] { "Boing boing!", "Bouncy!", "Wheee, bed!" } },
            { DuckEventKind.Water, new[] { "Splashy!", "Puddle!", "Splish splash!" } },
            { DuckEventKind.Tile,  new[] { "Wheee, slippery!", "Sliiide!", "Whoa, tiles!" } },
        };

        /// <summary>States and events share the repeat timer per duck: a state is its own number, an event this plus its number.</summary>
        const int EventTopic = 100;

        class Bubble
        {
            public GameObject go; public Transform slab, tail; public TextMeshPro text;
            public DuckBrain duck; public float age; public bool live; public float width;
        }

        readonly List<Bubble> pool = new List<Bubble>();
        readonly Dictionary<DuckBrain, (AiState, string)> lastIntent = new Dictionary<DuckBrain, (AiState, string)>();
        readonly Dictionary<(DuckBrain, int), float> lastSaid = new Dictionary<(DuckBrain, int), float>();
        readonly System.Random rng = new System.Random(5);
        TMP_FontAsset fontAsset;
        Material slabMat, inkMat;

        void Start()
        {
            if (viewer == null) viewer = Camera.main;
            if (font != null) fontAsset = TMP_FontAsset.CreateFontAsset(font);
            slabMat = ComicLook.ToonMaterial(Color.white, "bubble_white");
            slabMat.SetFloat(ShaderIds.Unlit, 1f);
            inkMat = ComicLook.ToonMaterial(ComicLook.Ink, "bubble_ink");
            inkMat.SetFloat(ShaderIds.Unlit, 1f);
            for (int i = 0; i < POOL; i++) pool.Add(MakeBubble(i));
        }

        Bubble MakeBubble(int i)
        {
            var b = new Bubble { go = new GameObject("Thought " + i) };
            b.go.transform.SetParent(transform, false);
            var slab = new GameObject("slab"); slab.transform.SetParent(b.go.transform, false);
            slab.AddComponent<MeshFilter>(); slab.AddComponent<MeshRenderer>().sharedMaterial = slabMat;
            b.slab = slab.transform;
            var tail = new GameObject("tail"); tail.transform.SetParent(b.go.transform, false);
            tail.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(0.022f, 0.022f, 0.004f), new ComicMesh.Style { radius = 0.002f, segments = 1 });
            tail.AddComponent<MeshRenderer>().sharedMaterial = slabMat;
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            b.tail = tail.transform;
            var txt = new GameObject("text"); txt.transform.SetParent(b.go.transform, false);
            b.text = txt.AddComponent<TextMeshPro>();
            if (fontAsset != null) b.text.font = fontAsset;
            b.text.fontSize = 0.2f; b.text.color = ComicLook.Ink; b.text.alignment = TextAlignmentOptions.Center;
            b.text.textWrappingMode = TextWrappingModes.NoWrap; b.text.rectTransform.sizeDelta = new Vector2(0.4f, 0.06f);
            txt.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            b.go.SetActive(false);
            return b;
        }

        public void Say(DuckBrain d, AiState state, string context)
        {
            if (!showThoughts || !StateLines.TryGetValue(state, out var lines)) return;
            Say(d, (int)state, lines, context);
        }

        public void Say(DuckBrain d, DuckEventKind kind, string context)
        {
            if (!showThoughts || !EventLines.TryGetValue(kind, out var lines)) return;
            Say(d, EventTopic + (int)kind, lines, context);
        }

        void Say(DuckBrain d, int topic, string[] lines, string context)
        {
            if (lastSaid.TryGetValue((d, topic), out var when) && Time.time - when < REPEAT) return;
            lastSaid[(d, topic)] = Time.time;
            string line = lines[rng.Next(lines.Length)].Replace("{room}", context).Replace("{prop}", context);
            Bubble b = null;
            foreach (var x in pool) if (x.live && x.duck == d) { b = x; break; }
            if (b == null) foreach (var x in pool) if (!x.live) { b = x; break; }
            if (b == null) { b = pool[0]; foreach (var x in pool) if (x.age > b.age) b = x; }
            b.duck = d; b.age = 0f; b.live = true;
            b.text.text = line.ToUpperInvariant();
            b.width = 0f;
            Fit(b);
        }

        void OnDuckEvent(DuckBrain d, DuckEventKind kind, string ctx) => Say(d, kind, ctx);

        /// <summary>Size the bubble to its text. Dynamic font glyphs can be generated a frame late, so this runs
        /// again while the bubble is young and grows the bubble whenever the measured text got wider.</summary>
        void Fit(Bubble b)
        {
            b.text.ForceMeshUpdate();
            float textW = Mathf.Max(b.text.textBounds.size.x, b.text.GetPreferredValues(b.text.text).x);
            float w = Mathf.Max(0.07f, textW + 0.035f), h = 0.042f;
            if (w <= b.width + 0.001f) return;
            b.width = w;
            b.slab.GetComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(Mathf.Round(w * 200f) / 200f, h, 0.004f), new ComicMesh.Style { radius = 0.016f, segments = 3, wobble = 0.002f, seed = 7 });
            b.tail.localPosition = new Vector3(w * 0.22f, -h / 2f, 0.001f);
        }
        readonly HashSet<DuckBrain> subscribed = new HashSet<DuckBrain>();

        void LateUpdate()
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            foreach (var d in DuckBrain.All)
            {
                if (subscribed.Add(d)) d.OnEvent += OnDuckEvent;
                var intent = (d.State, d.Context ?? "");
                if (!lastIntent.TryGetValue(d, out var prev) || prev != intent)
                {
                    lastIntent[d] = intent;
                    if (d.State == AiState.Toy && string.IsNullOrEmpty(d.Context)) continue;
                    Say(d, d.State, d.Context);
                }
            }
            // which bubbles may show: nearest MAX_ON, in front, in range, not behind walls
            Vector3 eye = viewer.transform.position;
            var live = new List<Bubble>();
            foreach (var b in pool) { if (b.live && b.duck == null) { b.live = false; b.go.SetActive(false); } if (b.live) live.Add(b); }   // a duck may have been sent home
            live.Sort((a, c) => Vector3.Distance(eye, a.duck.Pos).CompareTo(Vector3.Distance(eye, c.duck.Pos)));
            int shown = 0;
            foreach (var b in pool)
            {
                if (!b.live) { b.go.SetActive(false); continue; }
                b.age += Time.deltaTime;
                if (b.age < 0.5f) Fit(b);
                if (b.age > SHOW + FADE || b.duck == null) { b.live = false; b.go.SetActive(false); continue; }
                Vector3 p = b.duck.Pos + Vector3.up * 0.33f;
                Vector3 toB = p - eye; float dist = toB.magnitude;
                bool ok = live.IndexOf(b) < MAX_ON && dist < RANGE && Vector3.Dot(toB, viewer.transform.forward) > 0f
                          && !Physics.Raycast(eye, toB / dist, dist - 0.1f, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore);
                b.go.SetActive(ok);
                if (!ok) continue;
                shown++;
                // pop in, hold, shrink out; always face the viewer (upright, no roll)
                float s = b.age < 0.18f ? Mathf.SmoothStep(0.2f, 1.12f, b.age / 0.18f) : b.age < 0.3f ? Mathf.Lerp(1.12f, 1f, (b.age - 0.18f) / 0.12f) : b.age > SHOW ? Mathf.Lerp(1f, 0f, (b.age - SHOW) / FADE) : 1f;
                float distScale = Mathf.Clamp(dist / 0.55f, 0.6f, 6f);    // the same size in your eye near or far: small beside you, readable across the room
                b.go.transform.position = p + Vector3.up * 0.037f * (distScale - 1f);
                Vector3 look = p - eye; look.y = 0f;
                if (look.sqrMagnitude > 1e-6f) b.go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                b.go.transform.localScale = Vector3.one * s * distScale;
            }
        }
    }
}
