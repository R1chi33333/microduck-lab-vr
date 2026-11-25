using System.Collections.Generic;
using MicroduckVR.Comic;
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

        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "wander",   new[] { "Just exploring...", "What's over here?", "La la, wandering." } },
            { "roam",     new[] { "Off to the {room}!", "Heading to the {room}!", "Visiting the {room}." } },
            { "follow",   new[] { "Wait for me!", "Right behind you!", "Coming too!" } },
            { "toy",      new[] { "Ooh, a {prop}!", "That {prop} is mine!", "Mmm, {prop}!" } },
            { "got",      new[] { "Got it!", "Gotcha!", "Mine now!" } },
            { "drop",     new[] { "Here you go!", "For you!", "Ta-da!" } },
            { "sit",      new[] { "Taking a break.", "Comfy.", "Sitting pretty." } },
            { "idle",     new[] { "~ la la ~", "Hmm...", "Nice day." } },
            { "bed",      new[] { "Boing boing!", "Bouncy!", "Wheee, bed!" } },
            { "water",    new[] { "Splashy!", "Puddle!", "Splish splash!" } },
            { "tile",     new[] { "Wheee, slippery!", "Sliiide!", "Whoa, tiles!" } },
        };

        class Bubble
        {
            public GameObject go; public Transform slab, tail; public TextMeshPro text;
            public DuckBrain duck; public float age; public bool live;
        }

        readonly List<Bubble> pool = new List<Bubble>();
        readonly Dictionary<DuckBrain, string> lastIntent = new Dictionary<DuckBrain, string>();
        readonly Dictionary<(DuckBrain, string), float> lastSaid = new Dictionary<(DuckBrain, string), float>();
        readonly System.Random rng = new System.Random(5);
        TMP_FontAsset fontAsset;
        Material slabMat, inkMat;

        void Start()
        {
            if (viewer == null) viewer = Camera.main;
            if (font != null) fontAsset = TMP_FontAsset.CreateFontAsset(font);
            slabMat = ComicLook.ToonMaterial(Color.white, "bubble_white");
            slabMat.SetFloat("_Unlit", 1f);
            inkMat = ComicLook.ToonMaterial(ComicLook.Ink, "bubble_ink");
            inkMat.SetFloat("_Unlit", 1f);
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

        public void Say(DuckBrain d, string kind, string context)
        {
            if (!showThoughts || !Lines.TryGetValue(kind, out var lines)) return;
            if (lastSaid.TryGetValue((d, kind), out var when) && Time.time - when < REPEAT) return;
            lastSaid[(d, kind)] = Time.time;
            string line = lines[rng.Next(lines.Length)].Replace("{room}", context).Replace("{prop}", context);
            Bubble b = null;
            foreach (var x in pool) if (x.live && x.duck == d) { b = x; break; }
            if (b == null) foreach (var x in pool) if (!x.live) { b = x; break; }
            if (b == null) { b = pool[0]; foreach (var x in pool) if (x.age > b.age) b = x; }
            b.duck = d; b.age = 0f; b.live = true;
            b.text.text = line.ToUpperInvariant();
            b.text.ForceMeshUpdate();
            float w = Mathf.Max(0.07f, b.text.textBounds.size.x + 0.03f), h = 0.042f;
            b.slab.GetComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(w, h, 0.004f), new ComicMesh.Style { radius = 0.016f, segments = 3, wobble = 0.002f, seed = rng.Next(100) });
            b.tail.localPosition = new Vector3(w * 0.22f, -h / 2f, 0.001f);
        }

        void OnDuckEvent(DuckBrain d, string kind, string ctx) => Say(d, kind, ctx);
        readonly HashSet<DuckBrain> subscribed = new HashSet<DuckBrain>();

        void LateUpdate()
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            foreach (var d in DuckBrain.All)
            {
                if (subscribed.Add(d)) d.OnEvent += OnDuckEvent;
                string intent = d.State + ":" + d.Context;
                if (!lastIntent.TryGetValue(d, out var prev) || prev != intent)
                {
                    lastIntent[d] = intent;
                    if (d.State == "toy" && string.IsNullOrEmpty(d.Context)) continue;
                    Say(d, d.State, d.Context);
                }
            }
            // which bubbles may show: nearest MAX_ON, in front, in range, not behind walls
            Vector3 eye = viewer.transform.position;
            var live = new List<Bubble>();
            foreach (var b in pool) if (b.live) live.Add(b);
            live.Sort((a, c) => Vector3.Distance(eye, a.duck.Pos).CompareTo(Vector3.Distance(eye, c.duck.Pos)));
            int shown = 0;
            foreach (var b in pool)
            {
                if (!b.live) { b.go.SetActive(false); continue; }
                b.age += Time.deltaTime;
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
                float distScale = Mathf.Clamp(dist / 0.9f, 1f, 2.6f);      // far bubbles stay readable
                b.go.transform.position = p + Vector3.up * 0.02f * (distScale - 1f);
                Vector3 look = p - eye; look.y = 0f;
                if (look.sqrMagnitude > 1e-6f) b.go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                b.go.transform.localScale = Vector3.one * s * distScale;
            }
        }
    }
}
