using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// <summary>
    /// M6b: the elite_swap channel (CONCEPT_personas_v2 v2.1 Sec.1.1) -
    /// count- AND budget-neutral candidate transformation, BY CONSTRUCTION:
    /// a selected REGULAR card is replaced by its printed elite counterpart
    /// of the SAME name and stays charged at the REGULAR card's printed cost
    /// (the persona pays the difference - Sec.1.1-1). The plan's figure
    /// count and total booked cost therefore equal the baseline
    /// (persona=none) plan; the Delta-0 canon pins that equality per
    /// persona x mission x threat (personas_swap.json).
    ///
    /// The runtime laws (concept Sec.1.1-2..4, each pinned in the
    /// personas_swap.json canon):
    ///   HAND     - the swap consumes the unique elite deploy card; it must
    ///              be in the hand when the candidate is taken (consumption
    ///              model like the Hard-Mode upgrade, DeploymentManager.
    ///              DoDeployGroup). Missing elite card -> the swap fails
    ///              HARMLESSLY: no crash, no fallback, no substitution -
    ///              the regular deploys as-is.
    ///   NAME CAP - at most ONE swap per figure name per mission (Beschluss
    ///              12; holds even for names shipping two elite cards).
    ///   BOARD    - no swap while an elite model of the same name stands on
    ///              the board. The caller feeds the board in DEFENSIVELY
    ///              (mission start groups and Hard-Mode upgrade products
    ///              included); this class reads it and changes nothing -
    ///              the legacy elite rails stay untouched (Sec.1.2-2,
    ///              Thomas 02.11: their upstream rule bends are deliberate,
    ///              their own double-elite possibility is a separate
    ///              investigation, never fixed inside a persona hop).
    ///   BUDGET   - at most Sum(amount) swaps per mission over the active
    ///              elite_swap rows (rank rows stack additively).
    ///
    /// Locale law (M6b, the M13f/ignored-lane lesson): name matching runs
    /// on the INTERNAL (English) card names, never the localized display
    /// names - SetCardTranslations() overwrites live card names with the
    /// active language, so callers must pass internal names. The Unity seam
    /// resolves them id-first through the shipped EN card asset, and the
    /// swap target itself is always the row's explicit elite_card id.
    ///
    /// Pinned by SecPersonasSwap (hand-authored fixture, personas_swap.json)
    /// and gated against the shipped asset by PersonasGate (flat schema +
    /// target-set law). No UnityEngine, no RNG, no wall time: every
    /// decision is a pure function of the inputs.
    /// </summary>

    /// <summary>One card taking part in a deployment plan: draft entry,
    /// board model or hand card. Names are INTERNAL (English); Amount is
    /// the printed group size (the figure count the parity law pins -
    /// PersonasGate additionally demands regular.size == elite.size for
    /// every swap pairing, so a swap can never move the figure count).</summary>
    public sealed class SwapCard
    {
        public string Id;
        public string Name;
        public string Faction;
        public int PrintedCost;
        public int Amount;        // printed group size (figures)
        public bool IsElite;

        public SwapCard(string id, string name, string faction, int printedCost,
                        int amount, bool isElite)
        {
            Id = id; Name = name; Faction = faction;
            PrintedCost = printedCost; Amount = amount; IsElite = isElite;
        }
    }

    /// <summary>One plan entry after the transformation. ChargedCost is the
    /// value the budget sees: ALWAYS the regular card's printed cost - a
    /// swap never pays the elite's higher print (Sec.1.1-1).</summary>
    public sealed class SwapRow
    {
        public SwapCard Input;
        public SwapCard Out;          // elite counterpart when swapped, else Input
        public bool Swapped;
        public string EliteId;        // elite_card id when swapped, else null
        public int ChargedCost;

        public SwapRow(SwapCard input)
        {
            Input = input; Out = input; Swapped = false;
            EliteId = null; ChargedCost = input.PrintedCost;
        }

        public Dictionary<string, object> ToDict()
        {
            var d = new Dictionary<string, object>();
            d["in"] = Input.Id;
            d["out"] = Out.Id;
            d["swapped"] = Swapped;
            if (EliteId != null) d["elite"] = EliteId;
            d["amount"] = Out.Amount;
            d["charged"] = ChargedCost;
            return d;
        }
    }

    /// <summary>Mission-scoped swap state (budget consumed + names used).
    /// The Unity side holds ONE instance per mission identity (campaign
    /// GUID + missionID + threat - same recipe as the free-deployment
    /// subsidy) and rebuilds it when the identity changes; Plan() runs a
    /// fresh ledger per mission.</summary>
    public sealed class SwapLedger
    {
        int _swaps;
        readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Swaps { get { return _swaps; } }
        public bool NameUsed(string baseName)
        {
            return baseName != null && _names.Contains(baseName);
        }
        public bool Full(int budget) { return _swaps >= budget; }
        public void Book(string baseName) { _swaps++; _names.Add(baseName); }
    }

    public static class PersonaSwaps
    {
        /// <summary>The printed elite marker on internal card names
        /// ("Stormtrooper (Elite)"). The shipped EN lane pairs regular and
        /// elite exclusively through this suffix (PersonasGate pairing law),
        /// so BaseOf is the name-cap and board key in every locale - never
        /// a translation.</summary>
        public const string EliteSuffix = " (Elite)";

        public static string BaseOf(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return internalName;
            if (internalName.Length > EliteSuffix.Length &&
                internalName.EndsWith(EliteSuffix, StringComparison.OrdinalIgnoreCase))
                return internalName.Substring(0, internalName.Length - EliteSuffix.Length);
            return internalName;
        }

        /// <summary>Sum of Amount over the active elite_swap rows - the
        /// per-mission swap budget (0 for no persona / no swap rows).</summary>
        public static int Budget(PersonaView v)
        {
            if (v.IsEmpty) return 0;
            int n = 0;
            foreach (var e in v.Active)
                if (e.Kind == "elite_swap") n += Math.Max(1, e.Amount);
            return n;
        }

        /// <summary>First active elite_swap row matching the candidate
        /// (faction exact when set, name regex on the INTERNAL name -
        /// PersonaEffect.Matches is the one filter law the whole asset
        /// rides). Null when no row applies.</summary>
        public static PersonaEffect RowFor(PersonaView v, string internalName, string faction)
        {
            if (v.IsEmpty) return null;
            foreach (var e in v.Active)
                if (e.Kind == "elite_swap" && e.Matches(internalName, faction))
                    return e;
            return null;
        }

        /// <summary>The single swap decision, shared by the harness Plan()
        /// and the Unity seam (one law, no second truth).
        /// boardEliteBaseNames = base names of elite models currently on the
        /// board (fed defensively by the caller - start groups and Hard-Roll
        /// products included, read-only); availableEliteIds = elite deploy
        /// cards still in hand. outEliteId is the row's elite_card id on a
        /// green light; the caller then deploys that card INSTEAD of the
        /// regular, charges the regular's printed cost and books the
        /// ledger.</summary>
        public static bool TrySwap(PersonaView v, SwapLedger ledger,
                                   SwapCard candidate,
                                   ISet<string> boardEliteBaseNames,
                                   ISet<string> availableEliteIds,
                                   out string outEliteId)
        {
            outEliteId = null;
            if (v == null || ledger == null || candidate == null) return false;
            if (candidate.IsElite) return false;      // already the elite print
            int budget = Budget(v);
            if (budget <= 0 || ledger.Full(budget)) return false;
            var row = RowFor(v, candidate.Name, candidate.Faction);
            if (row == null) return false;
            // Target id law: the row's explicit elite_card. A row without
            // one is an asset error (PersonasGate fails the build); at
            // runtime it degrades harmlessly to no swap.
            var eliteId = row.EliteCard;
            if (string.IsNullOrEmpty(eliteId)) return false;
            // NAME CAP: max one swap per figure name per mission.
            var baseName = BaseOf(candidate.Name);
            if (ledger.NameUsed(baseName)) return false;
            // BOARD: no swap while an elite of the same name stands.
            if (boardEliteBaseNames != null && boardEliteBaseNames.Contains(baseName))
                return false;
            // HAND: the unique elite deploy card must still be in hand;
            // missing -> the swap fails harmlessly (no crash, no fallback,
            // no substitution - the regular deploys as-is).
            if (availableEliteIds == null || !availableEliteIds.Contains(eliteId))
                return false;
            outEliteId = eliteId;
            return true;
        }

        /// <summary>Run a whole mission draft through the transformation
        /// with a fresh ledger (harness canon driver; the live rails apply
        /// TrySwap per candidate with their own persistent ledger). The
        /// sequential pass keeps the hand consumption and the board
        /// additions of EARLIER swaps visible to LATER entries, exactly
        /// like the live deployment rails. hand = the deploy hand; board =
        /// what already stands at mission start. An empty view
        /// (persona=none) is the identity - the Delta-0 parity baseline.</summary>
        public static List<SwapRow> Plan(PersonaView view, IList<SwapCard> draft,
                                         IList<SwapCard> board, IList<SwapCard> hand)
        {
            var ledger = new SwapLedger();
            var boardBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (board != null)
                foreach (var b in board)
                    if (b.IsElite) boardBases.Add(BaseOf(b.Name));
            var handIds = new HashSet<string>(StringComparer.Ordinal);
            if (hand != null)
                foreach (var h in hand) handIds.Add(h.Id);

            var rows = new List<SwapRow>();
            if (draft == null) return rows;
            foreach (var c in draft)
            {
                var row = new SwapRow(c);
                string eliteId;
                if (TrySwap(view, ledger, c, boardBases, handIds, out eliteId))
                {
                    // the hand lookup by the ROW's target id - the pairing
                    // is the asset contract (elite_card), never a second
                    // derivation. No card behind the id = harmless miss.
                    var elite = FindById(hand, eliteId);
                    if (elite != null)
                    {
                        row.Swapped = true;
                        row.Out = elite;
                        row.EliteId = eliteId;
                        row.ChargedCost = c.PrintedCost;    // the regular's print
                        ledger.Book(BaseOf(c.Name));
                        handIds.Remove(eliteId);            // hand consumption
                        boardBases.Add(BaseOf(elite.Name)); // it stands now
                    }
                }
                rows.Add(row);
            }
            return rows;
        }

        static SwapCard FindById(IList<SwapCard> cards, string id)
        {
            if (cards == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in cards)
                if (string.Equals(c.Id, id, StringComparison.Ordinal)) return c;
            return null;
        }

        /// <summary>Budget actually consumed by a plan (harness swap-proof
        /// helper).</summary>
        public static int SwapsInPlan(List<SwapRow> plan)
        {
            int n = 0;
            if (plan != null) foreach (var r in plan) if (r.Swapped) n++;
            return n;
        }

        /// <summary>Figure total of a plan (the Delta-0 parity subject).</summary>
        public static int FiguresInPlan(List<SwapRow> plan)
        {
            int n = 0;
            if (plan != null) foreach (var r in plan) n += r.Out.Amount;
            return n;
        }

        /// <summary>Booked cost total (the parity subject: the CHARGED
        /// values, never the elite's higher prints).</summary>
        public static int CostInPlan(List<SwapRow> plan)
        {
            int n = 0;
            if (plan != null) foreach (var r in plan) n += r.ChargedCost;
            return n;
        }

        /// <summary>Double-elite audit (harness canon 3b, CONCEPT_personas_v2
        /// v2.1 Sec.1.1-4 / Beschluss 12): a plan never holds TWO ELITE prints
        /// of one figure name - per figure name at most ONE elite ever stands
        /// on the board (double elite cards and the legacy double-elite state
        /// are exactly this forbidden shape). Read-only over plans built here;
        /// the LEGACY elite rails are outside this class by law (Sec.1.2-2).
        /// An elite print ALONGSIDE a regular of the same name is the plain
        /// post-swap state elite_swap legitimately produces by design (DG003
        /// swapped in, a second Stormtrooper group still regular) and two
        /// plain regulars of one name stay legal - flagging either would
        /// flag the channel or mission authoring itself (documented reading
        /// of the task wording "nie zwei Modelle gleichen Figurennamens,
        /// davon eines elite": the forbidden state is the double ELITE).</summary>
        public static bool HasDoubleElite(List<SwapRow> plan)
        {
            if (plan == null) return false;
            var elites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in plan)
                if (r.Out != null && r.Out.IsElite && !elites.Add(BaseOf(r.Out.Name)))
                    return true;
            return false;
        }
    }
}
