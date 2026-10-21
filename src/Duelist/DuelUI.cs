using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;

namespace DuelistMod
{
    /// <summary>
    /// Duel interface that lives on the board. The 3D stands, zones and piles are clicked directly
    /// (raycast from the tabletop camera); small labels are pinned to them; a tooltip follows the
    /// mouse with exact numbers. Only the hand, life, a log ticker and the turn buttons are flat.
    /// Keys: 1-8 play a hand card, F1-F5 attack with that zone, Tab/E/Q change target,
    /// Space next phase (or don't respond to a chain), R end turn, X Extra Deck, L full log,
    /// Backspace surrender.
    /// </summary>
    public class DuelUI : MonoBehaviour
    {
        public DuelSession Session;

        private const float RefH = 1080f;
        private float _scale = 1f, _w = 1920f;
        private bool _cursorOpened;
        private object _eventSystem;
        private bool _extraOpen, _fullLog, _helpOpen;

        // Context menu (hand monster summon choices, field monster actions).
        private class MenuItem { public string Label; public string Disabled; public Action Act; }
        private List<MenuItem> _menu;
        private Vector2 _menuAt;
        private Rect _menuRect;
        private readonly List<Rect> _menuRects = new List<Rect>();
        private string _menuTitle;

        // Graveyard / Banished viewer, also used to pick a Revive target.
        private enum PileView { None, Grave, Banished, Revive }
        private PileView _pile = PileView.None;
        private Card _reviveSpell;
        private Rect _pileRect, _pileCloseRect;
        private readonly List<Rect> _pileRects = new List<Rect>();
        private readonly List<int> _pileIndex = new List<int>();

        // Material selection bar.
        private Rect _selRect, _selOkRect, _selCancelRect;

        private bool AnyPanel => _extraOpen || _pile != PileView.None || _menu != null;

        // Gamepad: a cursor driven by the left stick (D-pad snaps between buttons/cards), A clicks.
        private readonly DuelPad _pad = new DuelPad();
        private bool _padMode;
        private Vector2 _cursor = new Vector2(960f, 700f);
        private int _pendingClick = -1;
        private bool _padClick;
        private Vector3 _lastMouse;
        private readonly List<Vector2> _snaps = new List<Vector2>();
        private List<MenuItem> _snappedMenu;
        private DuelClickable[] _clickables;
        private float _clickablesAt = -10f;
        private bool PadOn => _padMode && DuelConfig.Controller.Value;

        private static Texture2D _white;
        private GUIStyle _txt, _small, _tiny, _title, _num, _numSub, _banner, _btn, _pill, _pillBold, _tip;
        private bool _stylesReady;

        // What the mouse is over.
        private DuelClickable _hoverWorld;
        private int _hoverHand = -1;
        private int _hoverExtraCard = -1;
        private bool _overUi;

        // Rects from the last repaint, for click handling.
        private readonly List<Rect> _handRects = new List<Rect>();
        private readonly List<Rect> _extraRects = new List<Rect>();
        private Rect _endRect, _nextRect, _retreatRect, _extraPanelRect, _extraCloseRect, _lifeRect, _logRect, _helpBtnRect, _helpRect;

        // Phase bar (clickable chips) and the chain prompt.
        private readonly List<Rect> _phaseRects = new List<Rect>();
        private readonly List<DuelSession.Phase> _phaseOf = new List<DuelSession.Phase>();
        private Rect _phaseBarRect, _promptRect, _passRect, _chainRect;
        private readonly List<Rect> _respRects = new List<Rect>();
        private readonly List<int> _respSlots = new List<int>();

        // Animated numbers and cards.
        private float _shownMyLp = -1f, _shownEnemyLp = -1f;
        private float _myLpHit = -10f, _enemyLpHit = -10f;
        private readonly Dictionary<Card, float> _cardBorn = new Dictionary<Card, float>();

        private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        private static readonly Color Panel = new Color(0.03f, 0.02f, 0.06f, 0.72f);

        // ------------------------------------------------------------ cursor

        private void OnEnable()
        {
            SetCursor(true);
            DuelistPlugin.Log.LogInfo("Duel UI attached.");
        }
        private void OnDisable()
        {
            SetCursor(false);
            if (Session != null && Session.Board)
            {
                Session.Board.PlaceMarker(Session.Board.TargetMarker, Vector3.zero, false);
                Session.Board.PlaceMarker(Session.Board.HoverMarker, Vector3.zero, false);
            }
        }

        private void SetCursor(bool open)
        {
            if (open == _cursorOpened) return;
            try
            {
                BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                if (_eventSystem == null)
                {
                    LocalUser lu = LocalUserManager.GetFirstLocalUser();
                    if (lu != null)
                        _eventSystem = lu.GetType().GetProperty("eventSystem", bf)?.GetValue(lu) ?? lu.GetType().GetField("eventSystem", bf)?.GetValue(lu);
                }
                if (_eventSystem == null) return;

                Type t = _eventSystem.GetType();
                PropertyInfo p = t.GetProperty("cursorOpenerCount", bf);
                FieldInfo fi = p == null ? t.GetField("cursorOpenerCount", bf) : null;
                int delta = open ? 1 : -1;
                if (p != null && p.CanWrite) p.SetValue(_eventSystem, Math.Max(0, (int)p.GetValue(_eventSystem) + delta));
                else if (fi != null) fi.SetValue(_eventSystem, Math.Max(0, (int)fi.GetValue(_eventSystem) + delta));
                else { DuelistPlugin.Log.LogWarning("cursorOpenerCount not found; use the keyboard in the duel."); return; }
                _cursorOpened = open;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("Cursor toggle failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------ world hover + markers

        private void Update()
        {
            try { UpdatePad(); }
            catch (Exception e)
            {
                if (_errorSections.Add("controller")) DuelistPlugin.Log.LogError("Duel UI controller failed:\n" + e);
            }
            try { UpdateWorld(); }
            catch (Exception e)
            {
                if (_errorSections.Add("world hover")) DuelistPlugin.Log.LogError("Duel UI world hover failed:\n" + e);
            }
        }

        private void UpdateWorld()
        {
            if (Session == null || Session.Finished || !Session.Board) return;

            _hoverWorld = null;
            Camera cam = Session.Camera.SceneCamera;
            if (cam && !_overUi && !AnyPanel)
            {
                Ray ray = cam.ScreenPointToRay(PointerScreen());
                float best = float.MaxValue;
                foreach (RaycastHit hit in Physics.RaycastAll(ray, 600f, ~0, QueryTriggerInteraction.Collide))
                {
                    DuelClickable c = hit.collider ? hit.collider.GetComponent<DuelClickable>() : null;
                    if (c && hit.distance < best && c.transform.IsChildOf(Session.Board.transform)) { best = hit.distance; _hoverWorld = c; }
                }
            }

            DuelBoard b = Session.Board;
            Representative target = Session.CurrentTarget();
            int ti = target != null ? Session.Reps.IndexOf(target) : -1;
            b.PlaceMarker(b.TargetMarker, target != null ? target.BoardPosition : Vector3.zero, target != null);
            if (b.TargetMarker && target != null) b.TargetMarker.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);

            bool hoverVisible = _hoverWorld && !(_hoverWorld.Kind == ClickKind.Rep && _hoverWorld.Index == ti);
            b.PlaceMarker(b.HoverMarker, hoverVisible ? HoverPos(_hoverWorld) : Vector3.zero, hoverVisible);
        }

        // ------------------------------------------------------------ gamepad

        /// <summary>Where the pointer is on screen (pixels, bottom-left origin): the pad cursor or the mouse.</summary>
        private Vector3 PointerScreen() => PadOn ? new Vector3(_cursor.x * _scale, Screen.height - _cursor.y * _scale, 0f) : Input.mousePosition;

        private void UpdatePad()
        {
            if (Session == null || Session.Finished || !DuelConfig.Controller.Value) { _padMode = false; return; }
            _pad.Poll();
            Vector3 mouse = Input.mousePosition;
            bool mouseUsed = (mouse - _lastMouse).sqrMagnitude > 4f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            _lastMouse = mouse;
            if (_pad.AnyInput && !_padMode)
            {
                _padMode = true;
                _cursor = new Vector2(mouse.x / _scale, (Screen.height - mouse.y) / _scale);   // start where the mouse was
            }
            else if (mouseUsed && !_pad.AnyInput) _padMode = false;
            if (!_padMode) return;

            // left stick: smooth cursor, faster the further you push
            Vector2 st = _pad.Stick;
            if (st != Vector2.zero)
            {
                float speed = Mathf.Lerp(250f, 1500f, Mathf.Clamp01((st.magnitude - 0.2f) / 0.8f));
                _cursor += new Vector2(st.x, -st.y).normalized * Mathf.Min(1f, st.magnitude) * speed * Time.unscaledDeltaTime;
            }
            // D-pad: hop to the next button / card / zone in that direction
            if (_pad.Left) Snap(new Vector2(-1f, 0f));
            if (_pad.Right) Snap(new Vector2(1f, 0f));
            if (_pad.Up) Snap(new Vector2(0f, -1f));
            if (_pad.Down) Snap(new Vector2(0f, 1f));
            _cursor.x = Mathf.Clamp(_cursor.x, 2f, _w - 2f);
            _cursor.y = Mathf.Clamp(_cursor.y, 2f, RefH - 2f);

            // a menu just opened: put the cursor on its first choice
            if (_menu != null && _menu != _snappedMenu && _menuRects.Count > 0) { _cursor = _menuRects[0].center; _snappedMenu = _menu; }
            if (_menu == null) _snappedMenu = null;

            if (_pad.A) _pendingClick = 0;
            else if (_pad.X) _pendingClick = 1;
            else if (_pad.B)
            {
                if (Session.AwaitingResponse) Session.PassChain();
                else if (AnyPanel || Session.Selecting != null) { _menu = null; _pile = PileView.None; _extraOpen = false; Session.CancelSelection(); }
                else if (_helpOpen) _helpOpen = false;
            }
            else if (_pad.Y)
            {
                if (Session.AwaitingResponse) Session.PassChain();
                else if (Session.Selecting != null) Session.ConfirmSelection();
                else if (!AnyPanel) Session.NextPhase();
            }
            if (_pad.LB) Session.CycleTarget(-1);
            if (_pad.RB) Session.CycleTarget(1);
            if (_pad.LT) _extraOpen = !_extraOpen;
            if (_pad.RT && Session.Selecting == null && !Session.AwaitingResponse) Session.EndTurn();
            if (_pad.Back) _helpOpen = !_helpOpen;
        }

        private void Snap(Vector2 dir)
        {
            float best = float.MaxValue;
            Vector2 pick = _cursor;
            foreach (Vector2 p in _snaps)
            {
                Vector2 v = p - _cursor;
                float along = Vector2.Dot(v, dir);
                if (along < 12f) continue;
                float side = Mathf.Abs(v.x * dir.y - v.y * dir.x);
                float score = along + side * 2.2f;
                if (score < best) { best = score; pick = p; }
            }
            _cursor = pick;
        }

        /// <summary>Everything the D-pad can hop to, gathered from the last repaint (only the open panel when one is up).</summary>
        private void BuildSnaps()
        {
            if (!PadOn) return;
            _snaps.Clear();
            void AddAll(List<Rect> rs) { foreach (Rect r in rs) if (r.width > 0f) _snaps.Add(r.center); }
            void Add(Rect r) { if (r.width > 0f) _snaps.Add(r.center); }

            if (_menu != null) { AddAll(_menuRects); return; }
            if (_pile != PileView.None) { AddAll(_pileRects); Add(_pileCloseRect); return; }
            if (_extraOpen) { AddAll(_extraRects); Add(_extraCloseRect); return; }
            if (Session.AwaitingResponse) { AddAll(_respRects); Add(_passRect); }
            if (Session.Selecting != null) { Add(_selOkRect); Add(_selCancelRect); }
            AddAll(_handRects);
            AddAll(_phaseRects);
            Add(_nextRect); Add(_endRect); Add(_helpBtnRect);

            // the board: monsters, zones, piles, the enemy commander
            if (Session.Board && Time.unscaledTime - _clickablesAt > 1f)
            {
                _clickables = Session.Board.GetComponentsInChildren<DuelClickable>();
                _clickablesAt = Time.unscaledTime;
            }
            if (_clickables != null)
                foreach (DuelClickable c in _clickables)
                    if (c && c.isActiveAndEnabled && ToGui(HoverPos(c), out Vector2 g)) _snaps.Add(g);
        }

        private void DrawPadCursor()
        {
            if (Event.current.type != EventType.Repaint) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float r = 14f + pulse * 3f;
            Color ring = new Color(1f, 0.85f, 0.35f, 0.95f);
            // a gold reticle: ring segments + a centre dot
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector2 p = _cursor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Box(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), ring);
            }
            Box(new Rect(_cursor.x - 3f, _cursor.y - 3f, 6f, 6f), Color.white);

            string hint = "<b>A</b> select   <b>X</b> options   <b>B</b> back   <b>Y</b> " + (Session.AwaitingResponse ? "don't respond" : Session.Selecting != null ? "confirm" : "next phase") +
                          "   <b>LB/RB</b> target   <b>LT</b> Extra Deck   <b>RT</b> end turn   <b>D-pad</b> jump   <b>View</b> help";
            Rect hr = new Rect(_w * 0.5f - 470f, RefH - 30f, 940f, 26f);
            Box(hr, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(hr, hint, new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter, fontSize = 14 });
        }

        private Vector3 HoverPos(DuelClickable c)
        {
            if (c.Kind == ClickKind.Rep && c.Index >= 0 && c.Index < Session.Reps.Count) return Session.Reps[c.Index].BoardPosition;
            if (c.Kind == ClickKind.Commander && Session.Board) return Session.Board.CommanderPos;
            return c.transform.position;
        }

        // ------------------------------------------------------------ styles & helpers

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            if (!_white)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            _txt = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, wordWrap = true };
            _txt.normal.textColor = Color.white;
            _small = new GUIStyle(_txt) { fontSize = 16 };
            _tiny = new GUIStyle(_txt) { fontSize = 13 };
            _title = new GUIStyle(_txt) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _num = new GUIStyle(_txt) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _numSub = new GUIStyle(_txt) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _banner = new GUIStyle(_txt) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 20, richText = true };
            _pill = new GUIStyle(_txt) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _pillBold = new GUIStyle(_pill) { fontStyle = FontStyle.Bold, fontSize = 16 };
            _tip = new GUIStyle(_txt) { fontSize = 16 };
        }

        private static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private static void Outline(Rect r, Color c, float w)
        {
            Box(new Rect(r.x, r.y, r.width, w), c);
            Box(new Rect(r.x, r.yMax - w, r.width, w), c);
            Box(new Rect(r.x, r.y, w, r.height), c);
            Box(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        private static void Bar(Rect r, float frac, Color fill, float ghost = -1f)
        {
            Box(r, new Color(0.15f, 0.03f, 0.03f, 0.9f));
            if (ghost > frac) Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ghost), r.height), new Color(1f, 0.9f, 0.6f, 0.9f));
            Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
        }

        private static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.9f);
            for (int dx = -2; dx <= 2; dx += 2)
                for (int dy = -2; dy <= 2; dy += 2)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        private static string Short(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        /// <summary>World point → GUI coordinates (scaled to 1080p). False if behind the camera.</summary>
        private bool ToGui(Vector3 world, out Vector2 gui)
        {
            gui = default(Vector2);
            Camera cam = Session.Camera.SceneCamera;
            if (!cam) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            gui = new Vector2(sp.x / _scale, (Screen.height - sp.y) / _scale);
            return true;
        }

        /// <summary>A small rounded label under a board object: one bold line, optional detail line, optional bar.</summary>
        private readonly List<Rect> _placedPills = new List<Rect>();

        /// <summary>The game's elite type in its own colour, e.g. "Blazing", or nothing for a normal monster.</summary>
        private static string AttrTag(Representative r)
        {
            if (r.EliteName == null) return "";
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(r.EliteColor, Color.white, 0.25f));
            return $"<color=#{hex}>◆{r.EliteName.ToUpper()}</color> ";
        }

        private Rect Pill(Vector2 at, string line1, string line2, Color border, float barFrac = -1f, Color barColor = default(Color), float ghost = -1f)
        {
            float w1 = _pillBold.CalcSize(new GUIContent(line1)).x;
            float w2 = string.IsNullOrEmpty(line2) ? 0f : _pill.CalcSize(new GUIContent(line2)).x;
            float width = Mathf.Max(w1, w2) + 18f;
            float height = 24f + (w2 > 0f ? 20f : 0f) + (barFrac >= 0f ? 10f : 0f);
            Rect r = new Rect(at.x - width * 0.5f, at.y, width, height);
            // never sit on top of a label that's already drawn: slide down below it
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                foreach (Rect p in _placedPills)
                {
                    if (!r.Overlaps(p)) continue;
                    r.y = p.yMax + 3f;
                    moved = true;
                }
                if (!moved) break;
            }
            _placedPills.Add(r);
            Box(r, Panel);
            Outline(r, border, 1.5f);
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, 22f), line1, _pillBold);
            float y = r.y + 22f;
            if (w2 > 0f) { GUI.Label(new Rect(r.x, y, r.width, 20f), line2, _pill); y += 20f; }
            if (barFrac >= 0f) Bar(new Rect(r.x + 8f, y + 1f, r.width - 16f, 6f), barFrac, barColor, ghost);
            return r;
        }

        // ------------------------------------------------------------ main

        private void OnGUI()
        {
            if (Session == null || Session.Finished) return;
            EnsureStyles();

            _scale = Screen.height / RefH;
            _w = Screen.width / _scale;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));

            if (PadOn)
            {
                // the gamepad cursor stands in for the mouse; A/X become left/right clicks
                if (_pendingClick >= 0 && Event.current.type == EventType.Layout)
                {
                    int btn = _pendingClick;
                    _pendingClick = -1;
                    Event saved = Event.current;
                    Event.current = new Event { type = EventType.MouseDown, mousePosition = _cursor, button = btn, clickCount = 1 };
                    _padClick = true;
                    try { DrawAll(); }
                    finally { _padClick = false; Event.current = saved; }
                }
                Event.current.mousePosition = _cursor;
            }
            DrawAll();
            if (PadOn) Safe("pad cursor", DrawPadCursor);
            if (_errorSections.Count > 0)
            {
                Rect r = new Rect(_w * 0.5f - 360f, RefH * 0.5f - 20f, 720f, 40f);
                Box(r, new Color(0.4f, 0f, 0f, 0.85f));
                GUI.Label(r, "Duel UI error in: " + string.Join(", ", _errorSections) + "  ·  Backspace to leave  ·  send LogOutput.log", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, richText = true });
            }
            GUI.matrix = oldMatrix;
        }

        private void DrawAll()
        {
            Safe("keys", HandleKeys);
            Safe("click", HandleClick);

            Safe("board labels", DrawBoardLabels);
            Safe("phase sweep", DrawPhaseSweep);
            Safe("header", DrawHeader);
            Safe("log", DrawLog);
            Safe("help", DrawHelp);
            Safe("life", DrawLife);
            Safe("hand", DrawHand);
            Safe("buttons", DrawButtons);
            if (_extraOpen) Safe("extra deck", DrawExtraPanel);
            Safe("pile", DrawPilePanel);
            Safe("selection", DrawSelection);
            Safe("chain", DrawChain);
            Safe("menu", DrawMenu);
            Safe("banner", DrawBanner);
            Safe("popups", DrawPopups);
            Safe("tooltip", DrawTooltip);
            if (Session.Result != DuelSession.Outcome.None) Safe("result", DrawResult);
            if (Event.current.type == EventType.Repaint) Safe("hover", UpdateOverUi);

            if (Event.current.type == EventType.Repaint) Safe("snaps", BuildSnaps);
        }

        private readonly HashSet<string> _errorSections = new HashSet<string>();

        /// <summary>Draws one UI section; an exception is logged once and the rest of the UI keeps working.</summary>
        private void Safe(string name, Action draw)
        {
            try { draw(); }
            catch (Exception e)
            {
                if (_errorSections.Add(name)) DuelistPlugin.Log.LogError($"Duel UI section '{name}' failed:\n{e}");
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));
                GUI.enabled = true;
                GUI.color = Color.white;
            }
        }

        private void UpdateOverUi()
        {
            Vector2 m = Event.current.mousePosition;
            _overUi = _hoverHand >= 0 || _endRect.Contains(m) || _nextRect.Contains(m) || _retreatRect.Contains(m) || _lifeRect.Contains(m) || _logRect.Contains(m) || _helpBtnRect.Contains(m) || (_helpOpen && _helpRect.Contains(m))
                      || _phaseBarRect.Contains(m)
                      || (Session.AwaitingResponse && _promptRect.Contains(m))
                      || (_extraOpen && _extraPanelRect.Contains(m))
                      || (_menu != null && _menuRect.Contains(m))
                      || (_pile != PileView.None && _pileRect.Contains(m))
                      || (Session.Selecting != null && _selRect.Contains(m));
        }

        private void HandleClick()
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || (e.button != 0 && e.button != 1)) return;
            Vector2 m = e.mousePosition;
            bool right = e.button == 1;

            // 0. A chain is waiting for your answer: the prompt (or a glowing set card on the board) takes the click.
            if (Session.AwaitingResponse)
            {
                for (int i = 0; i < _respRects.Count && i < _respSlots.Count; i++)
                    if (_respRects[i].Contains(m)) { Session.RespondWith(_respSlots[i]); e.Use(); return; }
                if (_passRect.Contains(m)) { Session.PassChain(); e.Use(); return; }
                if (_hoverWorld && _hoverWorld.Kind == ClickKind.BackZone) { Session.RespondWith(_hoverWorld.Index); e.Use(); return; }
                if (_promptRect.Contains(m)) { e.Use(); return; }
            }

            // Phase bar chips.
            for (int i = 0; i < _phaseRects.Count && i < _phaseOf.Count; i++)
            {
                if (!_phaseRects[i].Contains(m)) continue;
                ClickPhase(_phaseOf[i]);
                e.Use();
                return;
            }

            // 1. Open context menu takes the click.
            if (_menu != null)
            {
                for (int i = 0; i < _menuRects.Count && i < _menu.Count; i++)
                {
                    if (!_menuRects[i].Contains(m)) continue;
                    MenuItem it = _menu[i];
                    _menu = null;
                    if (it.Disabled == null) it.Act?.Invoke();
                    else Session.PopScreen(it.Disabled, new Color(1f, 0.6f, 0.6f));
                    e.Use();
                    return;
                }
                _menu = null;
                e.Use();
                return;
            }

            // 2. Graveyard / Banished viewer (and Revive target picking).
            if (_pile != PileView.None)
            {
                if (_pileCloseRect.Contains(m) || !_pileRect.Contains(m)) { _pile = PileView.None; e.Use(); return; }
                for (int i = 0; i < _pileRects.Count; i++)
                {
                    if (!_pileRects[i].Contains(m)) continue;
                    if (_pile == PileView.Revive)
                    {
                        Session.Revive(_reviveSpell, _pileIndex[i]);
                        _pile = PileView.None;
                    }
                    break;
                }
                e.Use();
                return;
            }

            // 3. Extra Deck panel: pick a monster, then pick its materials on the board.
            if (_extraOpen)
            {
                if (_extraCloseRect.Contains(m) || !_extraPanelRect.Contains(m)) { _extraOpen = false; e.Use(); return; }
                for (int i = 0; i < _extraRects.Count; i++)
                {
                    if (!_extraRects[i].Contains(m)) continue;
                    Session.StartExtra(i);
                    if (Session.Selecting != null) _extraOpen = false;
                    break;
                }
                e.Use();
                return;
            }

            // 4. Material selection: clicking your monsters picks them.
            if (Session.Selecting != null)
            {
                if (_selOkRect.Contains(m)) { Session.ConfirmSelection(); e.Use(); return; }
                if (_selCancelRect.Contains(m)) { Session.CancelSelection(); e.Use(); return; }
                if (_selRect.Contains(m)) { e.Use(); return; }
                if (_hoverWorld && _hoverWorld.Kind == ClickKind.MonsterZone) { Session.ToggleMaterial(_hoverWorld.Index); e.Use(); return; }
                if (right) { Session.CancelSelection(); e.Use(); return; }
            }

            if (_padClick)
            {
                // a controller "click" is a single event, so press the turn buttons here rather than through GUI.Button
                if (_nextRect.Contains(m)) { Session.NextPhase(); e.Use(); return; }
                if (_endRect.Contains(m)) { Session.EndTurn(); e.Use(); return; }
                if (_retreatRect.Contains(m)) { Session.Retreat(); e.Use(); return; }
            }
            if (_endRect.Contains(m) || _nextRect.Contains(m) || _retreatRect.Contains(m)) return; // let the buttons handle it
            if (_helpBtnRect.Contains(m) || _logRect.Contains(m))
            {
                if (_helpBtnRect.Contains(m)) _helpOpen = !_helpOpen; else _fullLog = !_fullLog;
                e.Use();
                return;
            }
            if (_helpOpen && _helpRect.Contains(m)) { e.Use(); return; }

            if (_hoverHand >= 0 && Session.Selecting == null) { ClickHandCard(_hoverHand, m); _hoverHand = -1; e.Use(); return; }

            if (_hoverWorld)
            {
                switch (_hoverWorld.Kind)
                {
                    case ClickKind.Rep: Session.SelectRep(_hoverWorld.Index); break;
                    case ClickKind.Commander: if (_hoverWorld.Index >= 0) Session.SelectRep(_hoverWorld.Index); else if (!Session.DirectorExposed) Session.PopScreen("Clear the Director's monster zones first, then attack it directly", new Color(0.85f, 0.7f, 1f)); break;
                    case ClickKind.MonsterZone:
                    {
                        int slot = _hoverWorld.Index;
                        FieldMonster fm = Session.Monsters[slot];
                        if (fm == null) break;
                        if (right || Session.CanAttack(fm) != null) OpenFieldMenu(slot, m);
                        else Session.AttackWith(slot);
                        break;
                    }
                    case ClickKind.BackZone:
                        if (Session.Backrow[_hoverWorld.Index] != null) Session.ActivateSetSpell(_hoverWorld.Index);
                        break;
                    case ClickKind.ExtraDeck: _extraOpen = true; break;
                    case ClickKind.Graveyard: _pile = PileView.Grave; break;
                    case ClickKind.Banished: _pile = PileView.Banished; break;
                }
                e.Use();
            }
        }

        private void ClickHandCard(int index, Vector2 at)
        {
            if (index < 0 || index >= Session.Hand.Count) return;
            Card c = Session.Hand[index];
            if (c.Type == CardType.Monster || c.IsQuickPlay) { OpenHandMenu(index, at); return; }
            if (c.Effect == CardEffect.Ritual) { Session.StartRitual(index); return; }
            if (c.Effect == CardEffect.Revive)
            {
                string why = Session.BlockReason(c);
                if (why != null) { Session.PopScreen(why, new Color(1f, 0.6f, 0.6f)); return; }
                _reviveSpell = c;
                _pile = PileView.Revive;
                return;
            }
            Session.PlayFromHand(index);
        }

        private void ClickPhase(DuelSession.Phase p)
        {
            if (!Session.IsPlayerTurn) return;
            DuelSession.Phase cur = Session.CurrentPhase;
            if (p == DuelSession.Phase.End) { Session.EndTurn(); return; }
            if (p == DuelSession.Phase.Battle && cur == DuelSession.Phase.Main1) { Session.NextPhase(); return; }
            if (p == DuelSession.Phase.Main2 && cur == DuelSession.Phase.Battle) { Session.NextPhase(); return; }
            if (p == DuelSession.Phase.Main2 && cur == DuelSession.Phase.Main1) { Session.NextPhase(); if (Session.CurrentPhase == DuelSession.Phase.Battle) Session.NextPhase(); return; }
        }

        private void OpenHandMenu(int index, Vector2 at)
        {
            Card c = Session.Hand[index];
            List<MenuItem> items = new List<MenuItem>();
            if (c.Type == CardType.Spell)
            {
                items.Add(new MenuItem { Label = "Activate (Quick-Play)", Disabled = Session.BlockReason(c), Act = () => Session.ActivateSpell(index) });
                items.Add(new MenuItem { Label = "Set (use it on the enemy's turn)", Disabled = Session.IsMainPhase ? null : "Only in your Main Phase", Act = () => Session.SetSpellTrap(index) });
                OpenMenu(c.Name, items, at);
                return;
            }
            if (c.IsRitual)
            {
                int spell = Session.Hand.FindIndex(h => h.Effect == CardEffect.Ritual);
                items.Add(new MenuItem { Label = "Ritual Summon", Disabled = Session.BlockReason(c), Act = () => Session.StartRitual(spell) });
            }
            else
            {
                if (c.TributesNeeded > 0)
                    items.Add(new MenuItem { Label = $"Tribute Summon ({c.TributesNeeded} tribute{(c.TributesNeeded > 1 ? "s" : "")})", Disabled = Session.CanTributeSummon(c), Act = () => Session.StartTribute(index) });
                else
                {
                    items.Add(new MenuItem { Label = $"Normal Summon (ATK {c.AtkPts})", Disabled = Session.CanNormalSummon(c), Act = () => Session.NormalSummon(index) });
                    items.Add(new MenuItem { Label = $"Set (face-down DEF {c.DefPts})", Disabled = Session.CanSet(c), Act = () => Session.SetMonster(index) });
                }
                if (c.Level <= 4)
                    items.Add(new MenuItem { Label = "Special Summon (no monsters on field)", Disabled = Session.CanSpecialFromHand(c), Act = () => Session.SpecialFromHand(index) });
            }
            OpenMenu(c.Name, items, at);
        }

        private void OpenFieldMenu(int slot, Vector2 at)
        {
            FieldMonster fm = Session.Monsters[slot];
            if (fm == null) return;
            List<MenuItem> items = new List<MenuItem>();
            Representative t = Session.CurrentTarget();
            string atkLabel = !Session.EnemyMonstersOnField ? "Attack directly" : t != null ? $"Attack {t.Label}" : "Attack";
            items.Add(new MenuItem { Label = atkLabel, Disabled = Session.CanAttack(fm), Act = () => Session.AttackWith(slot) });
            if (fm.FaceDown)
                items.Add(new MenuItem { Label = "Flip Summon", Disabled = Session.CanFlip(fm), Act = () => Session.FlipSummon(slot) });
            else if (!fm.IsAlly)
                items.Add(new MenuItem { Label = fm.Defense ? $"Change to attack position (ATK {Session.MonsterAtk(fm)})" : $"Change to defense position (DEF {Session.MonsterDef(fm)})", Disabled = Session.CanChangePosition(fm), Act = () => Session.ChangePosition(slot) });
            if (fm.Materials != null)
                items.Add(new MenuItem { Label = $"Detach a material: attack every monster ({fm.Materials.Count} left)", Disabled = Session.CanDetach(fm), Act = () => Session.DetachMaterial(slot) });
            OpenMenu(fm.FaceDown ? fm.Card.Name + " (set)" : fm.Title, items, at);
        }

        private void OpenMenu(string title, List<MenuItem> items, Vector2 at)
        {
            _menu = items;
            _menuTitle = title;
            _menuAt = at;
        }

        private void DrawMenu()
        {
            if (_menu == null) return;
            const float w = 360f, rowH = 34f;
            float h = 32f + _menu.Count * rowH + 8f;
            float x = Mathf.Clamp(_menuAt.x, 8f, _w - w - 8f);
            float y = Mathf.Clamp(_menuAt.y - h - 10f, 8f, RefH - h - 8f);
            _menuRect = new Rect(x, y, w, h);
            Box(_menuRect, new Color(0.03f, 0.02f, 0.07f, 0.96f));
            Outline(_menuRect, Gold, 2f);
            GUI.Label(new Rect(x + 12f, y + 4f, w - 24f, 26f), $"<b>{_menuTitle}</b>", _small);

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) _menuRects.Clear();
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < _menu.Count; i++)
            {
                MenuItem it = _menu[i];
                Rect r = new Rect(x + 6f, y + 32f + i * rowH, w - 12f, rowH - 4f);
                if (repaint) _menuRects.Add(r);
                bool hover = r.Contains(mouse);
                Box(r, it.Disabled == null ? (hover ? new Color(0.4f, 0.3f, 0.08f, 0.95f) : new Color(0.15f, 0.12f, 0.2f, 0.95f)) : new Color(0.1f, 0.1f, 0.1f, 0.8f));
                string label = it.Disabled == null ? $"<b>{it.Label}</b>" : $"<color=#888888>{it.Label}</color>  <size=12><color=#cc7777>{it.Disabled}</color></size>";
                GUI.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), label, new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false });
            }
        }

        private void DrawSelection()
        {
            if (Session.Selecting == null) { _selRect = default(Rect); return; }
            string status = Session.SelectionStatus(out bool ok);
            const float w = 820f;
            _selRect = new Rect(_w * 0.5f - w * 0.5f, 132f, w, 86f);
            Box(_selRect, new Color(0.05f, 0.03f, 0.1f, 0.95f));
            Outline(_selRect, ok ? new Color(0.4f, 1f, 0.5f) : Gold, 2f);
            GUI.Label(new Rect(_selRect.x + 14f, _selRect.y + 6f, w - 28f, 44f), status + "\n<color=#ffffffaa>Click your monsters on the board to pick them · right-click or Esc to cancel</color>", _small);
            _selOkRect = new Rect(_selRect.xMax - 250f, _selRect.yMax - 34f, 116f, 28f);
            _selCancelRect = new Rect(_selRect.xMax - 126f, _selRect.yMax - 34f, 112f, 28f);
            Box(_selOkRect, ok ? new Color(0.15f, 0.5f, 0.2f) : new Color(0.2f, 0.2f, 0.2f));
            GUI.Label(_selOkRect, ok ? "<b>CONFIRM</b> [C]" : "<color=#888888>CONFIRM</color>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            Box(_selCancelRect, new Color(0.4f, 0.12f, 0.12f));
            GUI.Label(_selCancelRect, "Cancel", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>The chain stack (newest on top) and, when it's your move, the respond prompt.</summary>
        private void DrawChain()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) { _respRects.Clear(); _respSlots.Clear(); }
            if (!Session.AwaitingResponse) { _promptRect = default(Rect); _passRect = default(Rect); }

            List<ChainLink> chain = Session.Chain;
            if (chain.Count > 0)
            {
                const float w = 330f, rowH = 30f;
                float h = 30f + chain.Count * rowH + 8f;
                _chainRect = new Rect(_w - w - 12f, 330f, w, h);
                Box(_chainRect, new Color(0.03f, 0.02f, 0.06f, 0.9f));
                Outline(_chainRect, new Color(1f, 0.85f, 0.35f, 0.6f), 1.5f);
                GUI.Label(new Rect(_chainRect.x + 10f, _chainRect.y + 4f, w - 20f, 24f), "<b>CHAIN</b> <color=#ffffff77>resolves top to bottom</color>", _tiny);
                for (int i = chain.Count - 1, row = 0; i >= 0; i--, row++)
                {
                    ChainLink l = chain[i];
                    Rect r = new Rect(_chainRect.x + 6f, _chainRect.y + 28f + row * rowH, w - 12f, rowH - 3f);
                    Box(r, l.Mine ? new Color(0.1f, 0.14f, 0.25f, 0.95f) : new Color(0.25f, 0.07f, 0.08f, 0.95f));
                    Box(new Rect(r.x, r.y, 4f, r.height), l.Color);
                    string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(l.Color, Color.white, 0.35f));
                    string text = $"<b>{l.Number}</b>  <color=#{hex}>{Short(l.Name, 20)}</color>  <color=#ffffff77>SS{l.Speed} · {(l.Mine ? "you" : "enemy")}</color>" + (l.Negated ? "  <color=#ff8080>NEGATED</color>" : "");
                    GUI.Label(new Rect(r.x + 10f, r.y, r.width - 14f, r.height), text, new GUIStyle(_tiny) { alignment = TextAnchor.MiddleLeft, wordWrap = false });
                }
            }

            if (!Session.AwaitingResponse) return;
            List<int> opts = Session.PlayerResponses();
            const float pw = 760f, optH = 40f;
            float ph = 96f + opts.Count * (optH + 6f) + 50f;
            _promptRect = new Rect(_w * 0.5f - pw * 0.5f, RefH * 0.5f - ph * 0.5f - 60f, pw, ph);
            Box(_promptRect, new Color(0.03f, 0.02f, 0.07f, 0.96f));
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            Outline(_promptRect, new Color(1f, 0.85f, 0.35f, pulse), 3f);
            GUI.Label(new Rect(_promptRect.x + 16f, _promptRect.y + 8f, pw - 32f, 30f), "<b><color=#f2c640>CHAIN?</color></b>  Respond with a set card", _small);
            GUI.Label(new Rect(_promptRect.x + 16f, _promptRect.y + 36f, pw - 32f, 40f), "<color=#ffffffcc>" + Session.ChainContext + "</color>", _tiny);
            float frac = Mathf.Clamp01(Session.ChainTimeLeft / Mathf.Max(1f, DuelConfig.ChainWindow.Value));
            Bar(new Rect(_promptRect.x + 16f, _promptRect.y + 78f, pw - 32f, 6f), frac, new Color(1f, 0.8f, 0.3f));

            Vector2 mouse = Event.current.mousePosition;
            for (int k = 0; k < opts.Count; k++)
            {
                SetCard sc = Session.Backrow[opts[k]];
                if (sc == null) continue;
                Rect r = new Rect(_promptRect.x + 16f, _promptRect.y + 92f + k * (optH + 6f), pw - 32f, optH);
                if (repaint) { _respRects.Add(r); _respSlots.Add(opts[k]); }
                bool hover = r.Contains(mouse);
                Box(r, hover ? new Color(0.4f, 0.3f, 0.08f, 0.95f) : new Color(0.15f, 0.12f, 0.2f, 0.95f));
                string kind = sc.Card.IsCounterTrap ? "COUNTER TRAP · SS3" : sc.Card.Type == CardType.Spell ? "QUICK-PLAY · SS2" : "TRAP · SS2";
                GUI.Label(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), $"<b>[{k + 1}] {sc.Card.Name}</b>  <color=#d0a0ff>{kind}</color>  <color=#ffffffaa>{Short(sc.Card.Describe(Session.Player), 70)}</color>", new GUIStyle(_tiny) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = 15 });
            }
            _passRect = new Rect(_promptRect.xMax - 260f, _promptRect.yMax - 42f, 244f, 32f);
            Box(_passRect, _passRect.Contains(mouse) ? new Color(0.35f, 0.15f, 0.15f) : new Color(0.22f, 0.12f, 0.14f));
            GUI.Label(_passRect, "Don't respond <color=#ffffff88>[Space]</color>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawPilePanel()
        {
            if (_pile == PileView.None) return;
            List<Card> src = _pile == PileView.Banished ? Session.Banished : Session.Graveyard;
            bool revive = _pile == PileView.Revive;
            string title = _pile == PileView.Banished ? "BANISHED · gone for the rest of the duel"
                         : revive ? $"{(_reviveSpell != null ? _reviveSpell.Name : "Revive")}: pick a monster to Special Summon from your graveyard"
                         : "GRAVEYARD";

            const float cw = 112f, ch = 158f, gap = 10f;
            const int perRow = 8;
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) { _pileRects.Clear(); _pileIndex.Clear(); }

            List<int> shown = new List<int>();
            for (int i = src.Count - 1; i >= 0 && shown.Count < 24; i--)
                if (!revive || src[i].Type == CardType.Monster || src[i].Type == CardType.Extra) shown.Add(i);
            int rows = Mathf.Max(1, Mathf.CeilToInt(shown.Count / (float)perRow));
            float pw = perRow * (cw + gap) + 30f;
            _pileRect = new Rect(_w * 0.5f - pw * 0.5f, 140f, pw, 70f + rows * (ch + gap) + 50f);
            Box(_pileRect, new Color(0.05f, 0.04f, 0.08f, 0.96f));
            Outline(_pileRect, revive ? Gold : new Color(0.6f, 0.6f, 0.75f), 3f);
            GUI.Label(new Rect(_pileRect.x + 16f, _pileRect.y + 8f, pw - 32f, 30f), $"<b>{title}</b>  ({src.Count})", _small);
            if (shown.Count == 0)
                GUI.Label(new Rect(_pileRect.x, _pileRect.y + 60f, pw, 40f), "<color=#aaaaaa>Empty.</color>", new GUIStyle(_txt) { alignment = TextAnchor.MiddleCenter });

            for (int k = 0; k < shown.Count; k++)
            {
                Rect r = new Rect(_pileRect.x + 15f + (k % perRow) * (cw + gap), _pileRect.y + 46f + (k / perRow) * (ch + gap), cw, ch);
                if (repaint) { _pileRects.Add(r); _pileIndex.Add(shown[k]); }
                DrawCardFace(r, src[shown[k]], -1);
                if (revive && r.Contains(Event.current.mousePosition)) Outline(r, Gold, 4f);
            }
            _pileCloseRect = new Rect(_pileRect.xMax - 130f, _pileRect.yMax - 44f, 110f, 34f);
            Box(_pileCloseRect, new Color(0.25f, 0.2f, 0.3f));
            GUI.Label(_pileCloseRect, "<b>Close</b>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        // ------------------------------------------------------------ labels pinned to the board

        private static string StatLine(int atk, int def, bool defense, int atkMod = 0, int defMod = 0, bool link = false)
        {
            string a = $"ATK {atk}" + (atkMod < 0 ? $"<size=12>({atkMod})</size>" : atkMod > 0 ? $"<size=12>(+{atkMod})</size>" : "");
            string d = link ? "LINK" : $"DEF {def}" + (defMod < 0 ? $"<size=12>({defMod})</size>" : "");
            return defense
                ? $"<color=#ffe0a088>{a}</color> / <b><color=#9cc8ff>{d}</color></b>"
                : $"<b><color=#ffd27a>{a}</color></b> / <color=#9cc8ff88>{d}</color>";
        }

        private void DrawBoardLabels()
        {
            _placedPills.Clear();
            if (!Session.Board) return;
            DuelBoard b = Session.Board;
            Vector3 toward = -b.transform.forward; // labels sit on the near edge of each object
            Representative target = Session.CurrentTarget();

            for (int i = 0; i < Session.Reps.Count; i++)
            {
                Representative rep = Session.Reps[i];
                if (rep.ZoneIndex < 0 || rep.Defeated || rep.IsCommander) continue; // in the deck, gone, or drawn below
                if (!ToGui(rep.BoardPosition + toward * 1.9f, out Vector2 at)) continue;

                bool isTarget = rep == target;
                string name = (rep.IsBoss ? "<color=#ff7777>★</color> " : "") + AttrTag(rep) + Short(rep.Name, 20);
                string pos = rep.DefensePos ? "<color=#9cc8ff>DEF POS</color>" : "<color=#ffb070>ATK POS</color>";
                string stats = StatLine(rep.Atk, rep.Def, rep.DefensePos, rep.AtkMod, rep.DefMod) + "  " + pos;
                Pill(at, isTarget ? "<color=#f2c640>◆</color> " + name : name, stats, isTarget ? Gold : rep.DefensePos ? new Color(0.6f, 0.75f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.3f));
            }

            DrawEnemyCommanderAndPiles(b, toward, target);

            for (int i = 0; i < DuelSession.Zones; i++)
            {
                FieldMonster fm = Session.Monsters[i];
                if (fm == null) continue;
                if (!ToGui(b.MonsterPos(i) + toward * 1.9f, out Vector2 at)) continue;
                bool ready = Session.CanAttack(fm) == null;
                string tag = fm.IsAlly ? "<color=#8fc8ff>ALLY</color> " : "";
                Card c = fm.Card;
                if (c.Extra == ExtraKind.Xyz) tag += $"<color=#c0c0ff>R{c.Rank}</color> ";
                else if (c.Extra == ExtraKind.Link) tag += $"<color=#80b0ff>LINK-{c.LinkRating}</color> ";
                else if (c.Extra == ExtraKind.Synchro) tag += $"<color=#ffffff>SYN Lv{c.Level}</color> ";
                else if (c.Extra == ExtraKind.Fusion) tag += "<color=#d0a0ff>FUS</color> ";
                else if (c.IsRitual) tag += "<color=#80a0ff>RIT</color> ";
                else if (!fm.IsAlly) tag += $"<color=#ffffff99>Lv{c.Level}</color>" + (c.IsTuner ? "<color=#a0ffa0>T</color> " : " ");
                if (c.HitsAll || fm.AllThisTurn) tag += "<color=#d0a0ff>ALL</color> ";

                string l1 = fm.FaceDown ? "<color=#e0c080>SET</color> " + Short(c.Name, 16) : tag + Short(fm.Title, 16);
                string mats = fm.Materials != null ? $"  <color=#c0c0ff>{fm.Materials.Count} mat</color>" : "";
                string frozen = fm.FrozenRound == Session.Round ? "  <color=#9fe8ff>FROZEN</color>" : "";
                string l2 = (fm.FaceDown ? $"<color=#e0c080>face-down</color> <color=#9cc8ff>DEF {Session.MonsterDef(fm)}</color>"
                                         : StatLine(Session.MonsterAtk(fm), Session.MonsterDef(fm), fm.Defense, fm.TempAtk, 0, c.Extra == ExtraKind.Link) + (fm.Defense ? "  <color=#9cc8ff>DEF POS</color>" : ""))
                            + mats + frozen + (ready ? "  <color=#f2c640>READY</color>" : "");

                Color border = new Color(1f, 0.85f, 0.35f, ready ? 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f) : 0.25f);
                if (Session.Selecting != null)
                {
                    if (Session.Selecting.Chosen.Contains(i)) { border = new Color(0.4f, 1f, 0.5f); l1 = "<color=#80ff90>✓ MATERIAL</color>  " + l1; }
                    else if (Session.IsCandidate(i)) { border = new Color(1f, 1f, 1f, 0.7f); l2 += "  <color=#ffffffaa>click to pick</color>"; }
                    else border = new Color(1f, 1f, 1f, 0.1f);
                }
                Pill(at, l1, l2, border);
            }

            List<int> responses = Session.AwaitingResponse ? Session.PlayerResponses() : null;
            for (int i = 0; i < DuelSession.Zones; i++)
            {
                SetCard sc = Session.Backrow[i];
                if (sc == null || !ToGui(b.BackPos(i) + toward * 1.9f, out Vector2 at)) continue;
                bool can = responses != null ? responses.Contains(i) : Session.SetCardReason(i) == null;
                string kind = sc.Card.IsCounterTrap ? "COUNTER" : sc.Card.Type == CardType.Spell ? "QUICK-PLAY" : "TRAP";
                string l2 = can ? "<color=#f2c640>click to activate!</color>" : $"<color=#ffffff77>{kind} · SS{sc.Card.SpellSpeed}</color>" + (sc.Charges > 1 ? $" x{sc.Charges}" : "");
                Color border = can ? new Color(1f, 0.85f, 0.35f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(0.85f, 0.5f, 1f, 0.5f);
                Pill(at, "<color=#e0a0ff>SET</color> " + Short(sc.Card.Name, 16), l2, border);
            }

            if (ToGui(b.World(DuelBoard.ExtraLocal) + toward * 1.9f, out Vector2 ex))
                Pill(ex, $"EXTRA DECK  {Session.ExtraDeck.Count}", "<color=#ffffff99>click or [X]</color>", new Color(0.7f, 0.5f, 1f, 0.6f));
            if (ToGui(b.World(DuelBoard.DeckLocal) + toward * 1.9f, out Vector2 dk))
                Pill(dk, $"DECK  {Session.DrawPile.Count}", $"<color=#ffffff88>{Session.DeckTotal}/{DuelConfig.MaxDeckSize.Value} · {DuelConfig.MetaDeckSize.Value} ideal</color>", new Color(1f, 1f, 1f, 0.25f));
            if (ToGui(b.World(DuelBoard.GraveLocal) + toward * 1.9f, out Vector2 gy))
                Pill(gy, $"GRAVEYARD  {Session.Graveyard.Count}", "<color=#ffffff88>click to view</color>", new Color(1f, 1f, 1f, 0.25f));
            if (ToGui(b.World(DuelBoard.BanishLocal) + toward * 1.3f, out Vector2 bn))
                Pill(bn, $"BANISHED  {Session.Banished.Count}", null, new Color(0.7f, 0.7f, 1f, 0.3f));
        }

        // ------------------------------------------------------------ flat HUD

        private void DrawEnemyCommanderAndPiles(DuelBoard b, Vector3 toward, Representative target)
        {
            // Commander: the boss (its health is their LP), or The Director when there's nobody.
            if (ToGui(b.CommanderPos + Vector3.up * 0.2f + toward * 2.6f, out Vector2 cat))
            {
                Representative c = Session.Commander;
                bool exposed = !Session.EnemyMonstersOnField;
                string expose = exposed ? "<color=#f2c640>OPEN · attacks hit their LP</color>" : "<color=#ffffff88>behind their monsters</color>";
                if (c == null)
                    Pill(cat, "<color=#d0a0ff>COMMANDER</color>  The Director", $"LP <b>{Session.EnemyLP}</b>  " + expose, exposed ? Gold : new Color(0.8f, 0.5f, 1f, 0.6f));
                else if (c.Defeated)
                    Pill(cat, "<color=#888888>COMMANDER " + Short(c.Name, 18) + "</color>", "<color=#888888>DEFEATED</color>", new Color(1f, 1f, 1f, 0.2f));
                else
                {
                    string l1 = "<color=#ff7777>★ COMMANDER</color>  " + AttrTag(c) + Short(c.Name, 18);
                    string l2 = $"LP <b>{Session.EnemyLP}</b> · <color=#ffd27a>ATK {c.Atk}</color>" + (c.Actions > 1 ? $" ×{c.Actions}" : "") + "  " + expose;
                    Pill(cat, l1, l2, exposed ? Gold : new Color(1f, 0.4f, 0.4f, 0.6f));
                }
            }

            // Their face-up cards: elite aspects on the field, each able to chain once per round.
            for (int i = 0; i < Session.EnemyAuras.Count && i < DuelSession.Zones; i++)
            {
                EnemyAura a = Session.EnemyAuras[i];
                if (!ToGui(b.EnemyBackPos(i) + toward * 1.9f, out Vector2 at)) continue;
                string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(a.Color, Color.white, 0.3f));
                string state = Session.AuraDestroyed(a) ? "<color=#888888>sealed</color>" : Session.AuraReady(a) ? "<color=#ffb0a0>can chain</color>" : "<color=#ffffff66>used this round</color>";
                Pill(at, $"<color=#{hex}>{Short(a.Name, 14)}</color> x{a.Count}", state, new Color(a.Color.r, a.Color.g, a.Color.b, 0.6f));
            }

            if (ToGui(b.World(DuelBoard.EnemyDeckLocal) + toward * 1.9f, out Vector2 ed))
                Pill(ed, $"ENEMY DECK  {Session.EnemyDeckCount}", "<color=#ffffff88>new spawns are drawn</color>", new Color(1f, 0.5f, 0.5f, 0.4f));
            if (ToGui(b.World(DuelBoard.EnemyGraveLocal) + toward * 1.9f, out Vector2 eg))
                Pill(eg, $"ENEMY GRAVEYARD  {Session.EnemyGraveCount}", null, new Color(1f, 1f, 1f, 0.25f));
            if (ToGui(b.World(DuelBoard.EnemyExtraLocal) + toward * 1.9f, out Vector2 ex))
                Pill(ex, $"ENEMY EXTRA  {Session.EnemyExtraCount}", Session.EnemyExtraCount > 0 ? "<color=#ffffff88>teleporter boss</color>" : null, new Color(0.7f, 0.5f, 1f, 0.4f));
        }

        private static readonly DuelSession.Phase[] PlayerPhases =
        {
            DuelSession.Phase.Draw, DuelSession.Phase.Standby, DuelSession.Phase.Main1,
            DuelSession.Phase.Battle, DuelSession.Phase.Main2, DuelSession.Phase.End
        };
        private static readonly string[] PhaseShort = { "DRAW", "STANDBY", "MAIN 1", "BATTLE", "MAIN 2", "END" };

        /// <summary>The phase bar: your six phases and the enemy's turn, the current one lit. Click ahead to move on.</summary>
        private void DrawHeader()
        {
            const float chip = 96f, enemyW = 150f, turnW = 108f, h = 40f;
            float total = turnW + PlayerPhases.Length * chip + enemyW + 8f;
            float x = _w * 0.5f - total * 0.5f, y = 8f;
            _phaseBarRect = new Rect(x, y, total, h);
            Box(_phaseBarRect, Panel);
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) { _phaseRects.Clear(); _phaseOf.Clear(); }

            GUI.Label(new Rect(x, y, turnW, h), $"TURN {Session.Round}<size=12>/{DuelConfig.MaxRounds.Value}</size>", new GUIStyle(_pillBold) { fontSize = 17 });
            float cx = x + turnW;
            DuelSession.Phase cur = Session.CurrentPhase;
            Vector2 mouse = Event.current.mousePosition;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < PlayerPhases.Length; i++)
            {
                DuelSession.Phase p = PlayerPhases[i];
                Rect r = new Rect(cx + 2f, y + 4f, chip - 4f, h - 8f);
                bool on = cur == p;
                bool past = Session.IsPlayerPhase && cur > p;
                bool clickable = Session.IsPlayerTurn && ((p == DuelSession.Phase.Battle && cur == DuelSession.Phase.Main1 && !(Session.Round == 1 && DuelConfig.FirstTurnNoBattle.Value))
                                 || (p == DuelSession.Phase.Main2 && (cur == DuelSession.Phase.Battle || cur == DuelSession.Phase.Main1))
                                 || (p == DuelSession.Phase.End && (cur == DuelSession.Phase.Main1 || cur == DuelSession.Phase.Battle || cur == DuelSession.Phase.Main2)));
                bool noBattle = p == DuelSession.Phase.Battle && Session.Round == 1 && DuelConfig.FirstTurnNoBattle.Value;
                Color bg = on ? new Color(0.85f * pulse, 0.62f * pulse, 0.12f, 0.95f)
                         : clickable && r.Contains(mouse) ? new Color(0.35f, 0.27f, 0.08f, 0.95f)
                         : past ? new Color(0.12f, 0.1f, 0.06f, 0.8f) : new Color(0.08f, 0.06f, 0.12f, 0.85f);
                Box(r, bg);
                if (clickable) Outline(r, new Color(1f, 0.85f, 0.35f, 0.7f), 1.5f);
                string label = noBattle ? $"<color=#ffffff33>{PhaseShort[i]}</color>" : on ? $"<color=#1a1206><b>{PhaseShort[i]}</b></color>" : past ? $"<color=#ffffff55>{PhaseShort[i]}</color>" : PhaseShort[i];
                GUI.Label(r, label, new GUIStyle(_pill) { fontSize = 14, richText = true });
                if (repaint) { _phaseRects.Add(r); _phaseOf.Add(p); }
                cx += chip;
            }
            Rect er = new Rect(cx + 6f, y + 4f, enemyW - 6f, h - 8f);
            bool enemy = Session.IsEnemyPhase;
            Box(er, enemy ? new Color(0.75f * pulse, 0.15f, 0.12f, 0.95f) : new Color(0.12f, 0.04f, 0.05f, 0.85f));
            string el = enemy ? $"<b>{DuelSession.PhaseName(cur).Replace("ENEMY ", "").Replace(" PHASE", "")}</b>" : "ENEMY TURN";
            GUI.Label(er, enemy ? "<color=#ffffff>ENEMY · " + el + "</color>" : "<color=#ff9a9a88>ENEMY TURN</color>", new GUIStyle(_pill) { fontSize = 14 });

            // what you can do right now, under the bar
            string hint = Session.AwaitingResponse ? "<color=#f2c640>RESPOND?</color> pick a set card or press Space"
                        : Session.ChainBusy ? "chain resolving…"
                        : cur == DuelSession.Phase.Main1 || cur == DuelSession.Phase.Main2 ? $"Summon, set, activate · Normal Summons left: {Session.SummonsLeft}"
                        : cur == DuelSession.Phase.Battle ? "Click a READY monster to attack your target"
                        : Session.IsEnemyPhase ? "Watch for chances to chain your set cards" : "";
            if (hint.Length > 0) Shadowed(new Rect(x, y + h + 2f, total, 22f), hint, new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter, fontSize = 14 }, new Color(1f, 1f, 1f, 0.85f));
        }

        /// <summary>The big sweep across the screen whenever the phase changes.</summary>
        private void DrawPhaseSweep()
        {
            float age = Time.unscaledTime - Session.PhaseChangedAt;
            if (age < 0f || age > 1.15f || Session.Result != DuelSession.Outcome.None) return;
            string name = DuelSession.PhaseName(Session.CurrentPhase);
            if (string.IsNullOrEmpty(name)) return;
            bool enemy = Session.IsEnemyPhase;
            float slideIn = Mathf.Clamp01(age / 0.18f);
            float slideOut = Mathf.Clamp01((age - 0.85f) / 0.3f);
            float ease = 1f - (1f - slideIn) * (1f - slideIn);
            float offset = (1f - ease) * -_w + slideOut * slideOut * _w;
            float a = 1f - slideOut;
            Rect band = new Rect(offset, RefH * 0.36f, _w, 86f);
            Color c = enemy ? new Color(0.55f, 0.06f, 0.08f, 0.82f * a) : new Color(0.2f, 0.12f, 0.02f, 0.82f * a);
            Box(band, c);
            Color edge = enemy ? new Color(1f, 0.35f, 0.3f, a) : new Color(1f, 0.85f, 0.35f, a);
            Box(new Rect(band.x, band.y, band.width, 3f), edge);
            Box(new Rect(band.x, band.yMax - 3f, band.width, 3f), edge);
            Shadowed(new Rect(band.x, band.y + 4f, band.width, 78f), name, new GUIStyle(_num) { fontSize = 54 }, new Color(1f, 0.95f, 0.8f, a));
        }

        /// <summary>End-of-duel screen: win or lose, why, kills and gold.</summary>
        private void DrawResult()
        {
            bool win = Session.Result == DuelSession.Outcome.Victory;
            float h = win ? 500f : 330f;
            Rect r = new Rect(_w * 0.5f - 340f, RefH * 0.5f - h * 0.5f, 680f, h);
            Box(new Rect(0f, 0f, _w, RefH), new Color(0f, 0f, 0f, 0.45f));
            Box(r, new Color(0.04f, 0.02f, 0.08f, 0.95f));
            Outline(r, win ? Gold : new Color(1f, 0.35f, 0.35f), 4f);
            if (win)
            {
                // a pulsing golden frame and rays: this is the big moment
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                Outline(new Rect(r.x - 8f, r.y - 8f, r.width + 16f, r.height + 16f), new Color(1f, 0.85f, 0.35f, 0.25f + 0.35f * pulse), 3f);
                Outline(new Rect(r.x - 16f, r.y - 16f, r.width + 32f, r.height + 32f), new Color(0.75f, 0.4f, 1f, 0.15f + 0.25f * (1f - pulse)), 2f);
            }

            Shadowed(new Rect(r.x, r.y + 18f, r.width, 70f), win ? "VICTORY!" : "DEFEAT", new GUIStyle(_num) { fontSize = 64 }, win ? Gold : new Color(1f, 0.4f, 0.4f));
            GUI.Label(new Rect(r.x + 20f, r.y + 96f, r.width - 40f, 30f), Session.ResultReason, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontSize = 19 });

            uint total = Session.BaseGold + Session.BonusGold + Session.VictoryGold + Session.JackpotGold + Session.BountyGold;
            int bountyItems = Session.BountyWhite + Session.BountyGreen + Session.BountyRed;
            string lines =
                $"Enemies destroyed: <b>{Session.KillCount}</b>\n" +
                $"Gold from kills: <b>${Session.BaseGold}</b>\n" +
                $"Duel kill bonus: <b><color=#f2c640>+${Session.BonusGold}</color></b>\n" +
                $"Monster bounties: <b><color=#f2c640>+${Session.BountyGold}</color></b>" + (bountyItems > 0 ? $" + <b>{(Session.BountyRed > 0 ? $"<color=#ff5b5b>{Session.BountyRed} red</color> " : "")}{(Session.BountyGreen > 0 ? $"<color=#7dff7d>{Session.BountyGreen} green</color> " : "")}{(Session.BountyWhite > 0 ? $"{Session.BountyWhite} white " : "")}</b>item{(bountyItems > 1 ? "s" : "")}" : "") + "\n" +
                (win ? $"Victory bonus: <b><color=#f2c640>+${Session.VictoryGold}</color></b>\n" +
                       $"Victory JACKPOT: <b><color=#f2c640>+${Session.JackpotGold}</color></b>\n" : "<color=#ffffff77>No victory bonus</color>\n") +
                $"<size=26><b>Total earned: <color=#f2c640>${total}</color></b></size>";
            GUI.Label(new Rect(r.x + 60f, r.y + 136f, r.width - 120f, 200f), lines, new GUIStyle(_txt) { alignment = TextAnchor.UpperCenter, fontSize = 20 });

            if (win)
            {
                string spoils =
                    $"<b><color=#ff5b5b>{Session.LootLegendary} LEGENDARY</color> + <color=#7dff7d>{Session.LootUncommon} uncommon</color> item{(Session.LootLegendary + Session.LootUncommon == 1 ? "" : "s")} drop at your feet</b>" +
                    (Session.BossBeaten ? "  <color=#c77dff>(boss bonus!)</color>" : "") + "\n" +
                    (DuelConfig.VictoryLevelUp.Value ? "<b><color=#7de0ff>LEVEL UP</color></b> · " : "") + "Full heal" +
                    (DuelConfig.VictoryBuffSeconds.Value > 0f ? $" · <b><color=#f2c640>Victor's Glory</color></b> ({DuelConfig.VictoryBuffSeconds.Value:0}s of speed)" : "");
                Box(new Rect(r.x + 30f, r.y + 346f, r.width - 60f, 2f), new Color(1f, 0.85f, 0.35f, 0.5f));
                GUI.Label(new Rect(r.x + 30f, r.y + 358f, r.width - 60f, 120f), "<size=24><b>VICTORY SPOILS</b></size>\n" + spoils,
                          new GUIStyle(_txt) { alignment = TextAnchor.UpperCenter, fontSize = 19 });
            }
        }

        private void DrawBanner()
        {
            float age = Time.unscaledTime - Session.BannerTime;
            if (age > 2.4f || string.IsNullOrEmpty(Session.Banner)) return;
            float a = age < 1.8f ? 1f : 1f - (age - 1.8f) / 0.6f;
            Rect r = new Rect(_w * 0.5f - 500f, 80f, 1000f, 46f);
            Shadowed(r, Session.Banner, _banner, new Color(1f, 0.92f, 0.6f, a));
        }

        private void DrawLog()
        {
            int lines = _fullLog ? 14 : 3;
            float width = _fullLog ? 520f : 380f;
            float y = 12f;
            List<string> shown = new List<string>();
            for (int i = Mathf.Max(0, Session.Log.Count - lines); i < Session.Log.Count; i++) shown.Add(Session.Log[i]);
            float h = 28f;
            foreach (string s in shown) h += _tiny.CalcHeight(new GUIContent(s), width - 20f) + 2f;
            _logRect = new Rect(12f, y, width, h);
            Box(_logRect, new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(22f, y + 4f, width - 20f, 20f), "<b>LOG</b> <color=#ffffff77>[L]</color>", _tiny);
            float ly = y + 24f;
            for (int i = 0; i < shown.Count; i++)
            {
                bool newest = i == shown.Count - 1;
                float lh = _tiny.CalcHeight(new GUIContent(shown[i]), width - 20f);
                GUI.Label(new Rect(22f, ly, width - 20f, lh), newest ? shown[i] : "<color=#ffffffaa>" + shown[i] + "</color>", _tiny);
                ly += lh + 2f;
            }
        }

        private void DrawHelp()
        {
            _helpBtnRect = new Rect(12f, _logRect.yMax + 6f, 110f, 28f);
            Box(_helpBtnRect, _helpOpen ? new Color(0.35f, 0.25f, 0.05f, 0.85f) : new Color(0f, 0f, 0f, 0.5f));
            Outline(_helpBtnRect, new Color(1f, 0.85f, 0.35f, 0.5f), 1f);
            GUI.Label(_helpBtnRect, (_helpOpen ? "▾" : "▸") + " <b>HELP</b> [H]", new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter });
            if (!_helpOpen) { _helpRect = default(Rect); return; }

            const string text =
                "<b>Turn</b>: Draw → Standby → <b>Main 1</b> → <b>Battle</b> → <b>Main 2</b> → End, then the enemy's turn. [Space] moves to the next phase, [R] ends your turn. No Battle Phase on turn 1.\n" +
                "<b>Main Phase</b>: 1 Normal Summon or Set per turn (Lv 5-6 tribute 1, Lv 7+ tribute 2), change positions, activate spells, set traps.\n" +
                "<b>Battle Phase</b>: click a monster to attack your target. ATK beats ATK: the loser is destroyed and its owner takes the difference. Against DEF: higher ATK destroys it with no damage; lower ATK costs you the difference. No monsters? Attack their LP directly.\n" +
                "<b>Life Points</b>: 8000 = full health. The enemy's LP is their commander's health.\n" +
                "<b>Chains</b>: when something happens, the other side may respond. Spell Speed 1 normal spells, 2 quick-play spells, traps and enemy elite cards, 3 Counter Traps (only a 3 answers a 3). The last link resolves first. Traps can't be used the turn they're set.\n\n" +
                "<color=#ffffffaa>Hover anything for exact numbers. Keys: 1-8 cards · F1-F5 attack · Tab target · X extra · L log · H help</color>\n" +
                "<color=#ffffffaa>Controller: left stick moves the cursor, D-pad jumps between things · A select · X options · B back · Y next phase · LB/RB target · LT Extra Deck · RT end turn · View help</color>";
            const float width = 400f;
            float h = _tiny.CalcHeight(new GUIContent(text), width - 20f) + 16f;
            _helpRect = new Rect(12f, _helpBtnRect.yMax + 4f, width, h);
            Box(_helpRect, new Color(0.02f, 0.01f, 0.05f, 0.9f));
            GUI.Label(new Rect(_helpRect.x + 10f, _helpRect.y + 8f, width - 20f, h - 16f), text, _tiny);
        }

        private void DrawLife()
        {
            CharacterBody b = Session.Player;
            if (!b || !b.healthComponent) return;
            int my = Session.PlayerLP, en = Session.EnemyLP;
            if (_shownMyLp < 0f) _shownMyLp = my;
            if (_shownEnemyLp < 0f) _shownEnemyLp = en;
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Time.unscaledDeltaTime;
                if (Mathf.Abs(_shownMyLp - my) > 0.5f)
                {
                    if (my < _shownMyLp) _myLpHit = Time.unscaledTime;
                    _shownMyLp = Mathf.MoveTowards(_shownMyLp, my, Mathf.Max(1600f, Mathf.Abs(_shownMyLp - my) * 2.2f) * dt);
                    DuelSfx.Tick(b.gameObject);
                }
                if (Mathf.Abs(_shownEnemyLp - en) > 0.5f)
                {
                    if (en < _shownEnemyLp) _enemyLpHit = Time.unscaledTime;
                    _shownEnemyLp = Mathf.MoveTowards(_shownEnemyLp, en, Mathf.Max(1600f, Mathf.Abs(_shownEnemyLp - en) * 2.2f) * dt);
                    DuelSfx.Tick(b.gameObject);
                }
            }
            Rect theirs = new Rect(_w - 312f, 12f, 300f, 84f);
            Rect mine = new Rect(_w - 312f, 102f, 300f, 84f);
            _lifeRect = new Rect(theirs.x, theirs.y, theirs.width, mine.yMax - theirs.y);
            string who = Session.Commander != null ? Short(Session.Commander.Name, 16) : "The Director";
            LpBox(theirs, $"ENEMY · {who}", _shownEnemyLp, en, new Color(0.9f, 0.3f, 0.3f), _enemyLpHit, null);
            string extra = $"<color=#f2c640>+${Session.BaseGold + Session.BonusGold}</color> gold · {Session.KillCount} destroyed";
            LpBox(mine, "YOU", _shownMyLp, my, new Color(0.35f, 0.85f, 0.45f), _myLpHit, extra);
        }

        private void LpBox(Rect r, string title, float shown, int real, Color bar, float hitTime, string sub)
        {
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - hitTime) / 0.5f);
            Box(r, Color.Lerp(Panel, new Color(0.6f, 0.05f, 0.05f, 0.85f), flash));
            Outline(r, new Color(1f, 0.85f, 0.35f, 0.35f + flash * 0.6f), 1.5f);
            GUI.Label(new Rect(r.x + 12f, r.y + 2f, r.width - 24f, 22f), $"<color=#ffffffaa>{title}</color>", _tiny);
            Shadowed(new Rect(r.x + 12f, r.y + 16f, r.width - 24f, 40f), $"LP  {Mathf.RoundToInt(shown)}", new GUIStyle(_num) { fontSize = 32, alignment = TextAnchor.MiddleLeft }, flash > 0f ? Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), flash) : Color.white);
            Bar(new Rect(r.x + 12f, r.y + 58f, r.width - 24f, 8f), shown / DuelStats.StartLP, bar, Mathf.Max(shown, real) / DuelStats.StartLP);
            if (sub != null) GUI.Label(new Rect(r.x + 12f, r.y + 64f, r.width - 24f, 20f), sub, new GUIStyle(_tiny) { fontSize = 12 });
        }

        private void DrawButtons()
        {
            _nextRect = new Rect(_w - 232f, RefH - 196f, 220f, 58f);
            _endRect = new Rect(_w - 232f, RefH - 132f, 220f, 58f);
            _retreatRect = new Rect(_w - 232f, RefH - 66f, 220f, 44f);
            string next = Session.NextPhaseLabel();
            GUI.enabled = Session.IsPlayerTurn && !_extraOpen && next != null;
            if (GUI.Button(_nextRect, next != null ? $"<b>→ {next}</b> [Space]" : "<color=#888888>NEXT PHASE</color>", _btn)) Session.NextPhase();
            GUI.enabled = Session.IsPlayerTurn && !_extraOpen && next != null;
            if (GUI.Button(_endRect, "<b>END TURN</b> [R]", _btn)) Session.EndTurn();
            GUI.enabled = !_extraOpen;
            if (GUI.Button(_retreatRect, "Surrender [Bksp]", _btn)) Session.Retreat();
            GUI.enabled = true;
        }

        // ------------------------------------------------------------ hand

        private void DrawHand()
        {
            const float cw = 128f, ch = 182f;
            int n = Session.Hand.Count;
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) _handRects.Clear();

            // cards that just arrived fly in from your deck, one after another
            float now = Time.unscaledTime;
            if (repaint)
            {
                int fresh = 0;
                foreach (Card c in Session.Hand)
                    if (!_cardBorn.ContainsKey(c)) { _cardBorn[c] = now + fresh * 0.12f; fresh++; }
                if (_cardBorn.Count > n + 8)
                {
                    List<Card> gone = new List<Card>();
                    foreach (Card c in _cardBorn.Keys) if (!Session.Hand.Contains(c)) gone.Add(c);
                    foreach (Card c in gone) _cardBorn.Remove(c);
                }
            }
            if (n == 0) { if (repaint) _hoverHand = -1; return; }

            float avail = Mathf.Min(_w - 560f, 1100f);
            float step = Mathf.Min(cw + 10f, (avail - cw) / Mathf.Max(1, n - 1));
            float total = step * (n - 1) + cw;
            float x0 = _w * 0.5f - total * 0.5f;
            float y = RefH - ch - 8f;
            Vector2 mouse = Event.current.mousePosition;
            Vector2 deck = new Vector2(_w - 160f, RefH - ch - 40f);

            int hovered = -1;
            if (!_extraOpen && !Session.AwaitingResponse)
                for (int i = n - 1; i >= 0; i--)
                    if (new Rect(x0 + i * step, y - 40f, i == n - 1 ? cw : step, ch + 40f).Contains(mouse)) { hovered = i; break; }
            if (repaint) _hoverHand = hovered;

            for (int i = 0; i < n; i++)
            {
                if (i == hovered) continue;
                Card c = Session.Hand[i];
                Rect r = new Rect(x0 + i * step, y, cw, ch);
                if (_cardBorn.TryGetValue(c, out float born))
                {
                    float t = Mathf.Clamp01((now - born) / 0.35f);
                    if (t <= 0f) continue;
                    if (t < 1f)
                    {
                        float e = 1f - (1f - t) * (1f - t) * (1f - t);
                        r.x = Mathf.Lerp(deck.x, r.x, e);
                        r.y = Mathf.Lerp(deck.y, r.y, e) - Mathf.Sin(t * Mathf.PI) * 60f;
                        float sc = Mathf.Lerp(0.6f, 1f, e);
                        r = new Rect(r.x, r.y, cw * sc, ch * sc);
                    }
                }
                DrawCardFace(r, c, i);
            }
            if (hovered >= 0)
            {
                // Lifted and enlarged so it reads clearly.
                Rect r = new Rect(x0 + hovered * step - 16f, y - 70f, cw + 32f, ch + 46f);
                DrawCardFace(r, Session.Hand[hovered], hovered);
                Outline(r, Session.BlockReason(Session.Hand[hovered]) == null ? Gold : new Color(1f, 0.4f, 0.4f), 4f);
            }
        }

        /// <summary>A compact card face: frame, name, art, type line, one key number.</summary>
        private void DrawCardFace(Rect r, Card c, int handIndex)
        {
            bool playable = Session.BlockReason(c) == null;
            float a = playable ? 1f : 0.6f;
            Color fr = c.Frame;
            Box(r, new Color(0.08f, 0.06f, 0.05f, a));
            Rect f = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
            Box(f, new Color(fr.r, fr.g, fr.b, a));

            float s = r.width / 128f;
            GUIStyle nameSt = new GUIStyle(_tiny) { fontSize = Mathf.RoundToInt(13 * s), fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(f.x + 4f, f.y + 2f, f.width - 8f, 36f * s), Short(c.Name, 24), nameSt);

            Rect art = new Rect(f.x + 8f, f.y + 38f * s, f.width - 16f, 70f * s);
            Box(art, new Color(0.92f, 0.9f, 0.82f, a));
            if (c.Art) GUI.DrawTexture(new Rect(art.center.x - art.height * 0.45f, art.y + art.height * 0.05f, art.height * 0.9f, art.height * 0.9f), c.Art, ScaleMode.ScaleToFit);
            else GUI.Label(art, $"<color=#00000088><b>{c.Type.ToString().ToUpper()}</b></color>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });

            GUIStyle typeSt = new GUIStyle(_tiny) { fontSize = Mathf.RoundToInt(11 * s), wordWrap = false };
            GUI.Label(new Rect(f.x + 4f, art.yMax + 2f, f.width - 8f, 18f * s), $"<color=#ffffffdd>{c.TypeLine}</color>", typeSt);

            string key;
            switch (c.Type)
            {
                case CardType.Monster:
                case CardType.Extra: key = c.Generic ? c.Requirement : c.Extra == ExtraKind.Link ? $"ATK {c.AtkPts}\nLINK-{Mathf.Max(1, c.LinkRating)}" : $"ATK {c.AtkPts}\nDEF {c.DefPts}"; break;
                default: key = c.Describe(Session.Player); break;
            }
            Rect box = new Rect(f.x + 4f, art.yMax + 20f * s, f.width - 8f, f.yMax - art.yMax - 24f * s);
            Box(box, new Color(0.95f, 0.92f, 0.84f, a));
            GUIStyle dark = new GUIStyle(_tiny) { fontSize = Mathf.RoundToInt((c.Type == CardType.Monster || c.Type == CardType.Extra ? 14 : 11) * s), alignment = TextAnchor.MiddleCenter };
            if ((c.Type == CardType.Monster || c.Type == CardType.Extra) && !c.Generic) dark.fontStyle = FontStyle.Bold;
            if (c.Generic) dark.fontSize = Mathf.RoundToInt(11 * s);
            dark.normal.textColor = new Color(0.1f, 0.08f, 0.06f, a);
            GUI.Label(box, key, dark);

            if (handIndex >= 0) GUI.Label(new Rect(r.x + 5f, r.y - 18f, 30f, 18f), $"<color=#ffffffaa>{handIndex + 1}</color>", _tiny);
        }

        // ------------------------------------------------------------ Extra Deck

        private void DrawExtraPanel()
        {
            const float cw = 150f, ch = 212f, gap = 14f;
            int n = Session.ExtraDeck.Count;
            float pw = Mathf.Max(560f, n * (cw + gap) + 40f);
            _extraPanelRect = new Rect(_w * 0.5f - pw * 0.5f, 300f, pw, ch + 120f);
            Box(_extraPanelRect, new Color(0.06f, 0.03f, 0.1f, 0.95f));
            Outline(_extraPanelRect, new Color(0.6f, 0.4f, 0.9f), 3f);
            GUI.Label(new Rect(_extraPanelRect.x + 16f, _extraPanelRect.y + 8f, pw - 32f, 30f),
                "<b>EXTRA DECK</b>  ·  click a monster, then pick its materials on your field", _small);

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) { _extraRects.Clear(); _hoverExtraCard = -1; }
            if (n == 0)
                GUI.Label(new Rect(_extraPanelRect.x, _extraPanelRect.center.y - 20f, pw, 40f), "<color=#aaaaaa>Empty. Big robots become Extra Deck fusions.</color>", new GUIStyle(_txt) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(_extraPanelRect.x + 20f + i * (cw + gap), _extraPanelRect.y + 48f, cw, ch);
                if (repaint) _extraRects.Add(r);
                DrawCardFace(r, Session.ExtraDeck[i], -1);
                if (r.Contains(Event.current.mousePosition))
                {
                    if (repaint) _hoverExtraCard = i;
                    Outline(r, Session.BlockReason(Session.ExtraDeck[i]) == null ? Gold : new Color(1f, 0.4f, 0.4f), 4f);
                }
            }
            _extraCloseRect = new Rect(_extraPanelRect.xMax - 130f, _extraPanelRect.yMax - 50f, 110f, 38f);
            Box(_extraCloseRect, new Color(0.25f, 0.2f, 0.3f));
            GUI.Label(_extraCloseRect, "<b>Close</b>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        // ------------------------------------------------------------ popups (pinned to the board)

        private void DrawPopups()
        {
            float now = Time.unscaledTime;
            float screenY = 0f;
            foreach (Popup p in Session.Popups)
            {
                Vector2 at;
                if (p.OnScreen) { at = new Vector2(_w * 0.5f, 440f + screenY); screenY += 40f; }
                else if (!ToGui(p.World, out at)) continue;

                float age = now - p.Born;
                float rise = Mathf.Min(1f, age / 0.25f) * 18f + age * 22f;
                float alpha = age < 1.5f ? 1f : Mathf.Clamp01(1f - (age - 1.5f) / 0.7f);
                float punch = age < 0.12f ? 1.35f - age * 2.9f : 1f;
                float y = at.y - 30f - rise;

                GUIStyle main = p.Big ? new GUIStyle(_num) { fontSize = Mathf.RoundToInt(44 * punch) } : new GUIStyle(_numSub) { fontSize = Mathf.RoundToInt(22 * punch) };
                Shadowed(new Rect(at.x - 220f, y, 440f, 54f), p.Text, main, new Color(p.Color.r, p.Color.g, p.Color.b, alpha));
                if (!string.IsNullOrEmpty(p.Sub))
                    Shadowed(new Rect(at.x - 240f, y + 42f, 480f, 24f), p.Sub, _numSub, new Color(1f, 1f, 1f, alpha));
            }
        }

        // ------------------------------------------------------------ tooltip

        private void DrawTooltip()
        {
            string text = TooltipText();
            if (string.IsNullOrEmpty(text)) return;
            const float width = 340f;
            float h = _tip.CalcHeight(new GUIContent(text), width - 24f) + 18f;
            Vector2 m = Event.current.mousePosition;
            float x = m.x + 24f, y = m.y + 18f;
            if (x + width > _w - 8f) x = m.x - width - 16f;
            if (y + h > RefH - 8f) y = RefH - h - 8f;
            if (_hoverHand >= 0) y = RefH - 260f - h; // above the hand
            Rect r = new Rect(x, y, width, h);
            Box(r, new Color(0.02f, 0.01f, 0.05f, 0.94f));
            Outline(r, new Color(1f, 0.85f, 0.35f, 0.5f), 1.5f);
            GUI.Label(new Rect(r.x + 12f, r.y + 9f, width - 24f, h - 18f), text, _tip);
        }

        private string TooltipText()
        {
            CharacterBody p = Session.Player;
            Representative target = Session.CurrentTarget();

            if (_extraOpen)
            {
                if (_hoverExtraCard < 0 || _hoverExtraCard >= Session.ExtraDeck.Count) return null;
                Card c = Session.ExtraDeck[_hoverExtraCard];
                string reason = Session.BlockReason(c);
                string stats = c.Generic ? "<color=#ffffffaa>ATK and DEF come from the materials you use.</color>" : $"ATK <b>{c.AtkPts}</b> · DEF <b>{c.DefPts}</b>";
                return CardHeader(c) + c.Describe(p) + "\n\n" + stats + (reason != null ? $"\n\n<color=#ff8080>{reason}</color>" : $"\n\n<color=#f2c640>Click to {DuelSession.ExtraVerb(c)}.</color>");
            }

            if (_hoverHand >= 0 && _hoverHand < Session.Hand.Count)
            {
                Card c = Session.Hand[_hoverHand];
                string body = CardHeader(c) + c.Describe(p);
                if (c.Type == CardType.Monster)
                {
                    body += $"\n\n<size=22><b><color=#ffd27a>ATK {c.AtkPts}</color> / <color=#9cc8ff>DEF {c.DefPts}</color></b></size>";
                    if (c.TributesNeeded > 0) body += $"\nLevel {c.Level}: tribute {c.TributesNeeded} monster(s) to summon it.";
                }
                else if (c.Effect == CardEffect.DamageOne && target != null) body += $"\n\nTarget: {target.Label} ({target.Atk}/{target.Def}).";
                else if (c.Type == CardType.Trap) body += "\n\nSet it in your Main Phase. From the enemy's next turn on it can be chained when its moment comes.";
                string reason = Session.BlockReason(c);
                return body + (reason != null ? $"\n\n<color=#ff8080>{reason}</color>" : "\n\n<color=#f2c640>Click to play.</color>");
            }

            if (!_hoverWorld) return null;
            int i = _hoverWorld.Index;
            switch (_hoverWorld.Kind)
            {
                case ClickKind.Rep:
                {
                    if (i >= Session.Reps.Count) return null;
                    Representative rep = Session.Reps[i];
                    if (rep.Defeated) return $"<b>{rep.Name}</b>\nDestroyed.";
                    string body = $"<size=20><b>{rep.Name}</b></size>\n<color=#ffffffaa>A real monster on the map, {rep.Distance:0}m from where you started the duel. Destroy it here and it dies there.</color>\n\n" +
                                  $"<size=22><b><color=#ffd27a>ATK {rep.Atk}</color> / <color=#9cc8ff>DEF {rep.Def}</color></b></size>  " + (rep.DefensePos ? "<color=#9cc8ff>DEFENSE POSITION</color>" : "<color=#ffb070>ATTACK POSITION</color>") +
                                  (rep.AtkMod != 0 || rep.DefMod != 0 ? $"\n<color=#80e0d0>Weakened until the End Phase ({rep.AtkMod}/{rep.DefMod}).</color>" : "");
                    if (rep.Elite != DuelElite.None && rep.Elite != DuelElite.Other) body += $"\n\n<color=#{ColorUtility.ToHtmlStringRGB(rep.EliteColor)}>{rep.EliteName}</color>: {DuelSession.EliteText(rep.Elite)}";
                    string fights = "";
                    foreach (FieldMonster fm in Session.Monsters)
                        if (fm != null && !fm.FaceDown && !fm.Defense) fights += "\n  " + Short(fm.Title, 16) + $" ({Session.MonsterAtk(fm)}): " + Session.PredictBattle(fm, rep);
                    if (fights.Length > 0) body += "\n\n<b>If you attack it:</b>" + fights;
                    return body + (rep == target ? "\n\n<color=#f2c640>Current target.</color>" : "\n\n<color=#f2c640>Click to target.</color>");
                }
                case ClickKind.MonsterZone:
                {
                    FieldMonster fm = Session.Monsters[i];
                    if (fm == null) return "<b>Monster zone</b>\nSummon a monster from your hand here in your Main Phase.";
                    string body = fm.IsAlly
                        ? $"<size=20><b>{fm.Card.Name}</b></size>  x{fm.AllyAlive}\n<color=#8fc8ff>Your allies, pulled into the duel. If this card is destroyed they're knocked back onto the map.</color>\n\n"
                        : CardHeader(fm.Card);
                    body += $"<size=22><b><color=#ffd27a>ATK {Session.MonsterAtk(fm)}</color> / <color=#9cc8ff>DEF {Session.MonsterDef(fm)}</color></b></size>";
                    if (fm.FaceDown) body += "\n<color=#e0c080>Set face-down in defense. Flips face-up when attacked, or Flip Summon it in a later Main Phase.</color>";
                    else if (fm.Defense) body += "\n<color=#9cc8ff>Defense position: can't attack. Attackers must beat its DEF; losing costs them the difference, and you take no damage.</color>";
                    else body += "\n<color=#ffb070>Attack position: battles with its ATK; if it loses, you take the difference.</color>";
                    if (fm.FrozenRound == Session.Round) body += "\n<color=#9fe8ff>Frozen: can't attack this turn.</color>";
                    if (fm.Revived) body += "\n<color=#c0c0ff>Revived: banished if it leaves the field.</color>";
                    if (fm.Materials != null) body += $"\n<color=#c0c0ff>{fm.Materials.Count} XYZ material(s) attached.</color>";
                    body += "\n\n";
                    string attackWhy = Session.CanAttack(fm);
                    if (attackWhy == null)
                    {
                        if (fm.Card.HitsAll || fm.AllThisTurn) body += "Attacks every enemy monster, one battle after another.";
                        else body += Session.PredictBattle(fm, Session.EnemyMonstersOnField ? target : null);
                        body += "\n\n<color=#f2c640>Click to attack · right-click for options.</color>";
                    }
                    else body += $"<color=#aaaaaa>{attackWhy}.</color>\n<color=#f2c640>Click for options.</color>";
                    return body;
                }
                case ClickKind.BackZone:
                {
                    SetCard sc = Session.Backrow[i];
                    if (sc == null) return "<b>Spell & Trap zone</b>\nTraps and Quick-Play spells are set here in your Main Phase.";
                    string why = Session.SetCardReason(i);
                    return CardHeader(sc.Card) + sc.Card.Describe(p) + (sc.Charges > 1 ? $"\nCharges: {sc.Charges}" : "") +
                           (why == null ? "\n\n<color=#f2c640>Click to activate.</color>" : $"\n\n<color=#ffffffaa>{why}.</color>");
                }
                case ClickKind.Commander:
                {
                    Representative c = Session.Commander;
                    if (c == null)
                        return $"<size=20><b>The Director</b></size>\n<color=#ffffffaa>No commander on the map, so the Director duels you.</color>\n\nLP <b>{Session.EnemyLP}</b>\n\n" +
                               (!Session.EnemyMonstersOnField ? "<color=#f2c640>Its zones are empty: your attacks and damage spells hit its LP directly.</color>"
                                                              : "<color=#ffffffaa>Destroy its monsters; battle damage and direct attacks lower its LP. Zero LP wins the duel.</color>");
                    if (c.Defeated) return $"<b>{c.Name}</b>\nThe enemy commander has fallen.";
                    string body = $"<size=20><b>{c.Name}</b></size>  <color=#ff7777>COMMANDER</color>\n<color=#ffffffaa>The enemy duelist. Its health is their Life Points: battle damage and direct attacks lower it.</color>\n\n" +
                                  $"LP <b>{Session.EnemyLP}</b> · <color=#ffd27a>ATK {c.Atk}</color>, attacks {c.Actions}× in their Battle Phase";
                    if (c.Elite != DuelElite.None && c.Elite != DuelElite.Other) body += $"\n\n<color=#{ColorUtility.ToHtmlStringRGB(c.EliteColor)}>{c.EliteName}</color>: {DuelSession.EliteText(c.Elite)}";
                    body += !Session.EnemyMonstersOnField ? "\n\n<color=#f2c640>No monsters protect it: attack directly!</color>" : "\n\n<color=#ffffffaa>Its monsters stand in the way. Destroy them first.</color>";
                    return body;
                }
                case ClickKind.EnemyZone:
                    return "<b>Enemy monster zone</b>\nThe enemy summons monsters here from its deck in its Main Phase, in attack or defense position.";
                case ClickKind.EnemyBack:
                {
                    if (i >= Session.EnemyAuras.Count) return "<b>Enemy spell & trap zone</b>\nElite aspects on their field appear here as face-up cards that can chain.";
                    EnemyAura a = Session.EnemyAuras[i];
                    string state = Session.AuraDestroyed(a) ? "<color=#888888>Sealed by your Counter Trap for the rest of the duel.</color>"
                                 : Session.AuraReady(a) ? "<color=#ffb0a0>Ready: may chain once this round.</color>" : "<color=#ffffff88>Already used this round.</color>";
                    return $"<size=20><b>{a.Name}</b></size>  <color=#ffffffaa>face-up · {a.Count} on their field</color>\n\n{DuelSession.AuraText(a.Elite)}\n\n{state}\n<color=#ffffffaa>Counter Traps (Spell Speed 3) can negate it.</color>";
                }
                case ClickKind.EnemyDeck:
                {
                    string waiting = "";
                    foreach (Representative r in Session.Reps) if (!r.OnField && !r.Defeated) waiting += $"\n  {r.Name} ({r.Atk}/{r.Def})";
                    return $"<b>ENEMY DECK</b> ({Session.EnemyDeckCount})\nMonsters that spawn on the map during the duel, and monsters with no free zone. They're summoned in the enemy's Main Phase." + (waiting.Length > 0 ? "\n\nWaiting:" + waiting : "");
                }
                case ClickKind.EnemyGrave:
                    return $"<b>ENEMY GRAVEYARD</b>\n{Session.EnemyGraveCount} enemy monsters destroyed this duel.";
                case ClickKind.EnemyExtra:
                    return Session.EnemyExtraCount > 0
                        ? "<b>ENEMY EXTRA DECK</b>\nThe teleporter boss hasn't been summoned yet. When it appears it takes command of their side."
                        : "<b>ENEMY EXTRA DECK</b>\nEmpty.";
                case ClickKind.ExtraDeck:
                    return $"<b>EXTRA DECK</b> ({Session.ExtraDeck.Count})\nFusion, Synchro, XYZ and Link monsters, plus your big-robot fusions. Summon them in your Main Phase: pick one, then pick its materials from your field (not allies).\n\n<color=#f2c640>Click to open.</color>";
                case ClickKind.Deck:
                {
                    int total = Session.DeckTotal, meta = DuelConfig.MetaDeckSize.Value, max = DuelConfig.MaxDeckSize.Value;
                    string advice = total > meta ? $"<color=#ffcc88>Above the {meta}-card sweet spot: your best cards come up less often.</color>"
                                  : total == meta ? "<color=#a0ffa0>Right at the sweet spot.</color>"
                                  : $"<color=#ffffffaa>{meta} is the sweet spot; keep collecting.</color>";
                    return $"<b>DECK</b>  {total}/{max} cards\n{Session.DrawPile.Count} left to draw. You draw {DuelConfig.DrawPerTurn.Value} in each Draw Phase (not on turn 1).\n\n{advice}\n\n<color=#ffffffaa>Spells: white/green/red items (healing items are Quick-Play) · Traps: equipment and void items · Counter Traps: lunar items · Monsters: drones you buy.</color>";
                }
                case ClickKind.Graveyard:
                    return $"<b>GRAVEYARD</b>\n{Session.Graveyard.Count} cards. Revive spells bring monsters back from here. Shuffled back into the deck if it runs out.\n\n<color=#f2c640>Click to view.</color>";
                case ClickKind.Banished:
                    return $"<b>BANISHED</b>\n{Session.Banished.Count} cards, gone for the rest of the duel. Revived monsters that leave the field end up here.";
            }
            return null;
        }

        private static string CardHeader(Card c)
        {
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(c.Frame, Color.white, 0.35f));
            return $"<size=20><b>{c.Name}</b></size>\n<color=#{hex}>{c.TypeLine}</color>" + (string.IsNullOrEmpty(c.Source) ? "" : $"  <color=#ffffff88>from: {c.Source}</color>") + "\n\n";
        }

        // ------------------------------------------------------------ keys

        private void HandleKeys()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            KeyCode k = e.keyCode;
            if (Session.AwaitingResponse)
            {
                if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha5)
                {
                    List<int> opts = Session.PlayerResponses();
                    int n = k - KeyCode.Alpha1;
                    if (n < opts.Count) Session.RespondWith(opts[n]);
                    e.Use();
                    return;
                }
                if (k == KeyCode.Space || k == KeyCode.Escape || k == KeyCode.N) { Session.PassChain(); e.Use(); return; }
            }
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha8) { Session.PlayFromHand(k - KeyCode.Alpha1); e.Use(); }
            else if (k >= KeyCode.F1 && k <= KeyCode.F5) { Session.AttackWith(k - KeyCode.F1); e.Use(); }
            else if (k == KeyCode.Tab || k == KeyCode.E) { Session.CycleTarget(1); e.Use(); }
            else if (k == KeyCode.Q) { Session.CycleTarget(-1); e.Use(); }
            else if (k == KeyCode.X) { _extraOpen = !_extraOpen; e.Use(); }
            else if (k == KeyCode.L) { _fullLog = !_fullLog; e.Use(); }
            else if (k == KeyCode.H) { _helpOpen = !_helpOpen; e.Use(); }
            else if (k == KeyCode.Space && Session.Selecting == null && !AnyPanel) { Session.NextPhase(); e.Use(); }
            else if (k == KeyCode.B && Session.CurrentPhase == DuelSession.Phase.Main1) { Session.NextPhase(); e.Use(); }
            else if ((k == KeyCode.R || k == KeyCode.KeypadEnter) && Session.Selecting == null) { Session.EndTurn(); e.Use(); }
            else if (k == KeyCode.Backspace) { Session.Retreat(); e.Use(); }
            else if (k == KeyCode.Escape && (AnyPanel || Session.Selecting != null)) { _menu = null; _pile = PileView.None; _extraOpen = false; Session.CancelSelection(); e.Use(); }
            else if (k == KeyCode.C && Session.Selecting != null) { Session.ConfirmSelection(); e.Use(); }
        }
    }
}
