# Doktrin-Texte der kaiserlichen KI — vollständiger Export (21.11.2026)

*Zur Kontrolle nach T.-Auftrag. Quelle: `Resources/Languages/De/ui.json` Abschnitt `uiTacticalAI` (Deutsch), Original `Languages/En/ui.json`. Neu: Popup-Reihenfolge DOKTRIN zuerst, dann Primärziel; Begriff „Primärziel“ statt „Fokus-Ziel“ (Regel 21.11.). Alle Platzhalter in geschweiften Klammern füllt die Laufzeit (Zielname, Zonenname, Zahl). Die Entscheidungsbäume erscheinen bei ERKLÄRUNG = VOLL; Doktrin- und Primärziel-Zeile immer.*

---

## 0. Die dauerhaften Popup-Zeilen bei jeder Aktivierung (neue Reihenfolge)

**Zeile 1 — Doktrin** (`doctrineHeader`, der Name trägt seine Erklärung in sich):

> Doktrin: 〈Doktrin-Name〉

*EN: Doctrine: 〈name〉*

Die sechs Doktrin-Namen mit ihrer eingebauten Erklärung:

- **Vorstoß (zum Kampf vorrücken)**
  *EN:* Advance (close into combat)
- **Wachschutz (Zone halten, Wege versperren, nie herauslocken)**
  *EN:* Guard (hold the zone, deny the paths, never be lured out)
- **Blockierer (den Weg zum Missionsziel versperren)**
  *EN:* Blocker (deny the path to the mission objective)
- **Jäger (Fokuszone abkämmen — die Fokuszone ist der Bereich, auf den das aktuelle Missionsziel der Rebellen zeigt: dort patrouillieren, Wege nehmen und Helden angreifen, die am Ziel arbeiten)**
  *EN:* Hunter (sweep the focus zone — the focus zone is the area the rebels' current mission objective points at: patrol it, take the paths there, attack heroes working the objective)
- **Support (Distanz halten, aus der Deckung)**
  *EN:* Support (keep distance, shoot from cover)
- **Hinterhalt (ungesehen bis zum ersten Schuss)**
  *EN:* Ambush (stay unseen until the first shot)

**Zeile 2 — Primärziel** (umbenannt von „Fokus-Ziel“). Klammer-Gesetz seit 24.11.: bei den drei Gebiets-Doktrinen (Wachschutz, Jäger, Blockierer) steht der Name in Klammern — „Primärziel: (Fenn)“ — als Signal, dass die Zielvorgabe dort weich ist; die Klammer umschließt nur den Namen, Tatsachen-Zusätze stehen dahinter. Bei Stürmer, Unterstützung, Hinterhalt bleibt der blanke Name (strikter Wunsch). Die EXPLAIN-Begründungszeile nennt den Namen immer blank:

Zusatz-Zusätze an der Zielzeile (erscheinen je nach Tatsachenlage hintereiander):

**Zeile 3 — die Handlungsblöcke** (aus der gedruckten Befehlskarte, lokalisiert):

**Duty-/Beute-Zeilen** (Pflichtkontingent, Muster-Cohort):

---

## 1. Je Doktrin: der vollständige Entscheidungsbaum-Text

### Stürmer — Muster `aggressive`

Doktrin-Zeile (Name+Erklärung): **Vorstoß (zum Kampf vorrücken)**

#### Normale kaiserliche Figur  
`apTreeAggressive`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Primärziel in Reichweite und freier Sichtlinie? → Primärziel angreifen (Waffen-/Fähigkeitsreichweite).</color><br>
> <color=#FFD76A>    Sichtlinie durch eine andere Heldenfigur blockiert? → diese blockierende Figur angreifen.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: bis zu {0} Felder auf das Primärziel zubewegen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Primärziel noch in Reichweite? → Bewege dich mit den restlichen Bewegungspunkten in Deckung.</color><br>
> <color=#FFD76A>    Primärziel NICHT in Reichweite → bis zu {0} Felder auf das Primärziel zubewegen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Primary target within range + clear line of sight? → attack the primary target (weapon/ability range).</color><br>
> <color=#FFD76A>    Line of sight blocked by another hero figure? → attack that blocking figure.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement: move up to {0} spaces toward the primary target.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: primary target still in range? → move into cover with your remaining movement.</color><br>
> <color=#FFD76A>    Primary target NOT in range → move up to {0} spaces toward it.</color>

#### Offizier  
`apTreeOfficerAggressive`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung des Primärziels, von ihm weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Befehl? → Primärziel in Reichweite + freie Sichtlinie? → selbst angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung des Primärziels ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the primary target, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No Order? → Primary target in range + clear line of sight? → attack yourself.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteAggressive`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung des Primärziels, von ihm weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: das Primärziel, oder ein anderes Ziel in ihrer Reichweite? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Primärziel in Reichweite + freie Sichtlinie? → selbst angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) or attack (which target)? Per Executive Order only one of the two, never both at once – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the primary target, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: the primary target, or another target in its reach? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Primary target in range + clear line of sight? → attack yourself.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepAggressive`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung des Primärziels, von ihm weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung des Primärziels ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the primary target, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepAggressive`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung des Primärziels, von ihm weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the primary target, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können


### Wächter (Wachschutz) — Muster `guard`

Doktrin-Zeile (Name+Erklärung): **Wachschutz (Zone halten, Wege versperren, nie herauslocken)**

#### Normale kaiserliche Figur  
`apTreeGuard`

**DE**  
> <i><color=#9FC6FF>Wachzone: der Bereich der Mission, in dem Rebellen ihre Ziele erfüllen müssen (Terminals, Türen, Objekte) – dort stehen eure Wachen.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Ein Held in eurer Wachzone oder an einem ihrer Missionsziele in Reichweite + freie Sichtlinie? → Angriff.</color><br>
> <color=#FFD76A>    Sonst prüfen, ob das Primärziel angreifbar ist? → Primärziel angreifen.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung (wähle eine Möglichkeit, keine Rangfolge): bis zu {0} Felder, die Bewegung endet in der Wachzone (auch von außerhalb hinein): auf den nächsten Helden zu, den Weg eines anderen Helden zum Ziel/Fluchtpunkt durch Engstelle oder Tür verstellen – oder Stellung halten.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung (wähle eine Möglichkeit, keine Rangfolge): bis zu {0} Felder, die Bewegung endet in der Wachzone (auch von außerhalb hinein): auf den nächsten Helden zu, den Weg eines anderen Helden zum Ziel/Fluchtpunkt durch Engstelle oder Tür verstellen – oder Stellung halten.</color>

**EN**  
> <i><color=#9FC6FF>Guard zone: the area of the mission where the rebels must complete their objectives (terminals, doors, objects) – that is where your guards stand.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    A hero in your guard zone or at one of its mission objectives within range + clear line of sight? → attack.</color><br>
> <color=#FFD76A>    Otherwise check the primary target: attackable? → attack the primary target.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement (choose one option, no priority): up to {0} spaces, the move ends inside the guard zone (entering it from outside is allowed): advance toward the nearest hero, block another hero's path to the objective/escape point at a chokepoint or door – or hold your position.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement (choose one option, no priority): up to {0} spaces, the move ends inside the guard zone (entering it from outside is allowed): advance toward the nearest hero, block another hero's path to the objective/escape point at a chokepoint or door – or hold your position.</color>

#### Offizier  
`apTreeOfficerGuard`

**DE**  
> <i><color=#9FC6FF>Wachzone: der Bereich der Mission, in dem Rebellen ihre Ziele erfüllen müssen (Terminals, Türen, Objekte) – dort stehen eure Wachen.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in die Wachzone hinein, an ihren Rand, ein Herankommen an das Missionsziel/Fluchtpunkt versperren, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Kein Befehl? → Nächster Held in der Wachzone in Reichweite + freie Sichtlinie? → selbst angreifen. Kein Held darin, aber Primärziel angreifbar? → Primärziel angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet.</color>

**EN**  
> <i><color=#9FC6FF>Guard zone: the area of the mission where the rebels must complete their objectives (terminals, doors, objects) – that is where your guards stand.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: into the guard zone, to its edge, denying access to the objective/escape point, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    No Order? → Nearest hero in the guard zone in range + clear line of sight? → attack yourself. Nobody inside but the primary target shootable? → attack the primary target.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteGuard`

**DE**  
> <i><color=#9FC6FF>Wachzone: der Bereich der Mission, in dem Rebellen ihre Ziele erfüllen müssen (Terminals, Türen, Objekte) – dort stehen eure Wachen.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in die Wachzone hinein, an ihren Rand, ein Herankommen an das Missionsziel/Fluchtpunkt versperren, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: ein Held in der Wachzone, oder das Primärziel? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Nächster Held in der Wachzone in Reichweite + freie Sichtlinie? → selbst angreifen. Kein Held darin, aber Primärziel angreifbar? → Primärziel angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet.</color>

**EN**  
> <i><color=#9FC6FF>Guard zone: the area of the mission where the rebels must complete their objectives (terminals, doors, objects) – that is where your guards stand.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) or attack (which target)? Per Executive Order only one of the two, never both at once – the command spends your attack point; it also overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: into the guard zone, to its edge, denying access to the objective/escape point, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: a hero inside the guard zone, or the primary target? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Nearest hero in the guard zone in range + clear line of sight? → attack yourself. Nobody inside but the primary target shootable? → attack the primary target.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepGuard`

**DE**  
> <i><color=#9FC6FF>Wachzone: der Bereich der Mission, in dem Rebellen ihre Ziele erfüllen müssen (Terminals, Türen, Objekte) – dort stehen eure Wachen.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in die Wachzone hinein, an ihren Rand, ein Herankommen an das Missionsziel/Fluchtpunkt versperren, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <i><color=#9FC6FF>Guard zone: the area of the mission where the rebels must complete their objectives (terminals, doors, objects) – that is where your guards stand.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: into the guard zone, to its edge, denying access to the objective/escape point, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepGuard`

**DE**  
> <i><color=#9FC6FF>Wachzone: der Bereich der Mission, in dem Rebellen ihre Ziele erfüllen müssen (Terminals, Türen, Objekte) – dort stehen eure Wachen.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in die Wachzone hinein, an ihren Rand, ein Herankommen an das Missionsziel/Fluchtpunkt versperren, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur (sie darf dafür auch die Wachzone verlassen).</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, bis zu {0} Felder innerhalb der Wachzone / in die Wachzone hinein ziehen – oder so postieren, dass ein Held den Weg zum Ziel/Fluchtpunkt nur am Posten vorbei findet – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <i><color=#9FC6FF>Guard zone: the area of the mission where the rebels must complete their objectives (terminals, doors, objects) – that is where your guards stand.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: into the guard zone, to its edge, denying access to the objective/escape point, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine (it may leave its guard zone for this).</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, move up to {0} spaces inside / into the guard zone – or take a post that forces a hero to pass it to reach the objective/escape point – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können


### Blocker — Muster `blocker`

Doktrin-Zeile (Name+Erklärung): **Blockierer (den Weg zum Missionsziel versperren)**

#### Normale kaiserliche Figur  
`apTreeBlocker`

**DE**  
> <i><color=#9FC6FF>Blockierauftrag: den Weg zum Missionsziel versperren – der Angriff ist Gelegenheit am Weg, nicht der Auftrag.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Ein Held oder Primärziel in Reichweite und freier Sichtlinie? → diesen angreifen.</color><br>
> <color=#FFD76A>    Sichtlinie durch eine andere Heldenfigur blockiert? → diese blockierende Figur angreifen.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: bis zu {0} Felder auf ein Blockierfeld zwischen Missionsziel und Primärziel (kein Primärziel? → auf den nächsten Helden zu).</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: das Blockierfeld zwischen Missionsziel und Primärziel halten – oder bis zu {0} Felder auf eines zubewegen.</color>

**EN**  
> <i><color=#9FC6FF>Blocking duty: deny the path to the mission objective – an attack is an opportunity on the way, not the order.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    A hero or the primary target in range + clear line of sight? → attack it.</color><br>
> <color=#FFD76A>    Line of sight blocked by another hero figure? → attack that blocking figure.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement: up to {0} spaces onto a blocking space between the mission objective and the primary target (no primary target? → toward the nearest hero).</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold the blocking space between mission objective and primary target – or shift up to {0} spaces onto one.</color>

#### Offizier  
`apTreeOfficerBlocker`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Blockierfeld zwischen Missionsziel und Primärziel, von einem Blockierfeld weg, oder zur eigenen Stellung hin? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Befehl? → Primärziel in Reichweite + freie Sichtlinie? → selbst angreifen. Sichtlinie durch andere Figur blockiert? → diese angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a block field between mission objective and primary target, off a block field, or toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No Order? → Primary target in range + clear line of sight? → attack yourself. Line of sight blocked by another figure? → attack that figure.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteBlocker`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Blockierfeld zwischen Missionsziel und Primärziel, von einem Blockierfeld weg, oder zur eigenen Stellung hin? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: das Primärziel, oder die Figur, die die Sichtlinie blockiert? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Primärziel in Reichweite + freie Sichtlinie? → selbst angreifen. Sichtlinie durch andere Figur blockiert? → diese angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) or attack (which target)? Per Executive Order only one of the two, never both at once – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a block field between mission objective and primary target, off a block field, or toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: the primary target, or the figure blocking the line of sight? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Primary target in range + clear line of sight? → attack yourself. Line of sight blocked by another figure? → attack that figure.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepBlocker`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Blockierfeld zwischen Missionsziel und Primärziel, von einem Blockierfeld weg, oder zur eigenen Stellung hin? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a block field between mission objective and primary target, off a block field, or toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepBlocker`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Blockierfeld zwischen Missionsziel und Primärziel, von einem Blockierfeld weg, oder zur eigenen Stellung hin? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung eines Blockierfeldes zwischen Missionsziel und Primärziel ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a block field between mission objective and primary target, off a block field, or toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces toward a block field between mission objective and primary target – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können


### Jäger — Muster `hunter`

Doktrin-Zeile (Name+Erklärung): **Jäger (Fokuszone abkämmen — die Fokuszone ist der Bereich, auf den das aktuelle Missionsziel der Rebellen zeigt: dort patrouillieren, Wege nehmen und Helden angreifen, die am Ziel arbeiten)**

#### Normale kaiserliche Figur  
`apTreeHunter`

**DE**  
> <i><color=#9FC6FF>Fokuszone: der Bereich, auf den das aktuelle Missionsziel der Rebellen zeigt – dort jagt der Jäger.</color></i><br>
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Ein Held IN der Fokuszone oder auf dem Weg zum Missionsziel in Reichweite und freier Sichtlinie? → ihn angreifen (mehrere Kandidaten? → der mit dem höheren Bedrohungswert; EXPLAIN zeigt ihn).</color><br>
> <color=#FFD76A>    Kein Zone-Held erreichbar, aber Primärziel in Reichweite + freier Sichtlinie? → Primärziel angreifen.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: bis zu {0} Felder in die Fokuszone bzw. auf den Zone-Helden zu.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: bis zu {0} Felder in die Fokuszone bzw. auf den Zone-Helden zu. Merksatz der Jäger: ein Held AUßERHALB der Fokuszone, der nicht auf dem Weg in sie ist, ist nicht euer Ziel – ignoriert ihn, selbst wenn er im Weg steht. Nur wer EUCH angreift, darf in dieser Runde euren Rückschuss ernten; verfolgt wird er nicht.</color>

**EN**  
> <i><color=#9FC6FF>Focus zone: the area the rebels' current mission objective points at – that is where the hunter hunts.</color></i><br>
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    A hero INSIDE the focus zone OR on the way to the mission objective in range and clear line of sight? → attack him (several candidates? → the one with the higher threat value; EXPLAIN shows it).</color><br>
> <color=#FFD76A>    No zone hero reachable, but primary target in range + clear line of sight? → attack the primary target.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement: move up to {0} spaces toward the focus zone resp. the zone hero.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: move up to {0} spaces toward the focus zone resp. the zone hero. The hunter's creed: a hero OUTSIDE the focus zone who is not moving into it is not your target – ignore him, even when he blocks your path. Only whoever attacks YOU may reap your shot back this round; nobody is pursued for it.</color>

#### Offizier  
`apTreeOfficerHunter`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung der Fokuszone, von ihr weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Befehl? → Zone-Held (arbeitet am Missionsziel in der Fokuszone) in Reichweite + freie Sichtlinie? → selbst angreifen; sonst Primärziel, wenn beschießbar.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung der Fokuszone ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the focus zone, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No Order? → Zone hero (working the mission objective inside the focus zone) in range + clear line of sight? → attack yourself; otherwise the primary target, if shootable.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward the focus zone.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteHunter`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung der Fokuszone, von ihr weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: der Zone-Held (arbeitet am Missionsziel in der Fokuszone), sonst das Primärziel? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Zone-Held in Reichweite + freie Sichtlinie? → selbst angreifen; sonst Primärziel, wenn beschießbar.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen.</color>

**EN**  
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: the zone hero (working the mission objective inside the focus zone), otherwise the primary target? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Zone hero in range + clear line of sight? → attack yourself; otherwise the primary target, if shootable.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepHunter`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung der Fokuszone, von ihr weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in Richtung der Fokuszone ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the focus zone, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces toward the focus zone – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepHunter`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in Richtung der Fokuszone, von ihr weg, oder seitlich? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: toward the focus zone, away from it, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können


### Unterstützung — Muster `support`

Doktrin-Zeile (Name+Erklärung): **Support (Distanz halten, aus der Deckung)**

#### Normale kaiserliche Figur  
`apTreeSupport`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Weichstes Ziel von Deckung aus beschießbar (Reichweite + freie Sichtlinie)? → es angreifen. Kein weiches Ziel? → Primärziel angreifen, wenn beschießbar.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: einen Schritt zurück in Deckung.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: einen Schritt zurück in Deckung.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Softest target shootable from cover (range + clear line of sight)? → attack it. No soft target? → attack the primary target if shootable.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement: step back into cover.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: step back into cover.</color>

#### Offizier  
`apTreeOfficerSupport`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in eine andere Deckung, zurück zur eigenen Stellung, oder seitlich versetzt? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Befehl? → Weichstes Ziel von Deckung aus beschießbar (Reichweite + freie Sichtlinie)? → es angreifen. Kein weiches Ziel? → Primärziel angreifen, wenn beschießbar.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: into another cover position, back toward your own line, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No Order? → Softest target shootable from cover (range + clear line of sight)? → attack it. No soft target? → attack the primary target if shootable.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces into another cover position.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces into another cover position.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteSupport`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in eine andere Deckung, zurück zur eigenen Stellung, oder seitlich versetzt? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: das weichste Ziel von ihrer Deckung aus, oder das Primärziel? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Weichstes Ziel von Deckung aus beschießbar (Reichweite + freie Sichtlinie)? → es angreifen. Kein weiches Ziel? → Primärziel angreifen, wenn beschießbar.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) or attack (which target)? Per Executive Order only one of the two, never both at once – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: into another cover position, back toward your own line, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: the softest target from its cover, or the primary target? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Softest target shootable from cover (range + clear line of sight)? → attack it. No soft target? → attack the primary target if shootable.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces into another cover position.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces into another cover position.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepSupport`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in eine andere Deckung, zurück zur eigenen Stellung, oder seitlich versetzt? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: into another cover position, back toward your own line, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces into another cover position – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces into another cover position – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepSupport`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: in eine andere Deckung, zurück zur eigenen Stellung, oder seitlich versetzt? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder in eine andere Deckung ziehen – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: into another cover position, back toward your own line, or sideways? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces into another cover position – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces into another cover position – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können


### Hinterhalt — Muster `ambush`

Doktrin-Zeile (Name+Erklärung): **Hinterhalt (ungesehen bis zum ersten Schuss)**

#### Normale kaiserliche Figur  
`apTreeAmbush`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Hinterhaltschuss auf den zuerst beschießbaren Helden möglich (Reichweite + freie Sichtlinie)? → ihn angreifen.</color><br>
> <color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: bis zu {0} Felder in neue Deckung.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: auf ein Feld, das kein Held sieht (bis zu {0}) – dein nächster Schuss kommt aus neuer Deckung.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Ambush shot at the first hero you can shoot (range + clear line of sight)? → attack him.</color><br>
> <color=#FFD76A>    No attack possible? → this point becomes movement: up to {0} spaces into new cover.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: relocate to a space no hero can see (up to {0}) – your next shot comes from new cover.</color>

#### Offizier  
`apTreeOfficerAmbush`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Feld, das kein Held sieht, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Befehl? → Hinterhaltschuss auf den zuerst beschießbaren Helden möglich (Reichweite + freie Sichtlinie)? → ihn angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a tile no hero sees, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No Order? → Ambush shot on the first shootable hero possible (range + clear line of sight)? → attack it.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces onto a tile no hero sees.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces onto a tile no hero sees.</color>

#### Offizier-ELITE  
`apTreeOfficerEliteAmbush`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) oder Angriff (welches Ziel)? Pro Vollstreckungsbefehl nur eines von beiden, nie beides zugleich – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Feld, das kein Held sieht, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Angriff → welche andere imperiale Figur in Reichweite greift welches Ziel an: den zuerst beschießbaren Helden, oder das Primärziel? – dieser Angriff verbraucht deinen Angriffspunkt.</color><br>
> <color=#FFD76A>    Kein Vollstreckungsbefehl? → Hinterhaltschuss auf den zuerst beschießbaren Helden möglich (Reichweite + freie Sichtlinie)? → ihn angreifen.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) or attack (which target)? Per Executive Order only one of the two, never both at once – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a tile no hero sees, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Attack → which other Imperial figure in reach attacks which target: the first shootable hero, or the primary target? – that attack spends your attack point.</color><br>
> <color=#FFD76A>    No Executive Order? → Ambush shot on the first shootable hero possible (range + clear line of sight)? → attack it.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces onto a tile no hero sees.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces onto a tile no hero sees.</color>

#### Offizier, Ziel-Halte-Variante  
`apTreeOfficerKeepAmbush`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>    Befehl möglich? → Welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Feld, das kein Held sieht, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>    Order possible? → Which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a tile no hero sees, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces onto a tile no hero sees – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces onto a tile no hero sees – never moving farther from your nearest Imperial ally than it is right now.</color>

#### Offizier-ELITE, Ziel-Halte-Variante  
`apTreeOfficerEliteKeepAmbush`

**DE**  
> <b>? Angriffspunkt verfügbar?</b><br>
> <color=#FFD76A>Vollstreckungsbefehl → welche imperiale Figur in Reichweite empfängt den Befehl: Bewegung (bis zu 2 Felder, wohin) – ohne Notfall nur Bewegung, nie ein Angriff – der Befehl verbraucht deinen Angriffspunkt; er gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Bewegung → welche andere imperiale Figur in Reichweite zieht sich bis zu 2 Felder wohin: auf ein Feld, das kein Held sieht, oder zurück zur eigenen Stellung? – der Befehl verbraucht deinen Angriffspunkt und gilt auch gegen die Doktrin der befohlenen Figur.</color><br>
> <color=#FFD76A>    Kein Notfall? → nicht von selbst angreifen. Ein eigener Angriff ist nur in einem dieser Notfälle erlaubt: er steht unter Feuer; ein Held steht direkt an ihm; das Missionsziel ist akut bedroht und nur er kann eingreifen; oder er ist die letzte lebende imperiale Gruppe – dann kämpfe er wie es seine Doktrin erlaubt. Ohne Notfall: gib deinen Befehl, bleib in Deckung hinter der Gruppe, der du den Befehl gabst, und drift nicht von ihr weg – zieh nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist; ist keine Deckung haltbar, geh einen Schritt zurück – nie einen nach vorn.</color><br>
> <color=#FFD76A>    Weder Befehl noch Angriff möglich? → dieser Punkt wird Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color><br>
> <b>? Angriffspunkt(e) verbraucht?</b><br>
> <color=#FFD76A>    Dann ist dieser Punkt Bewegung: Stellung halten, oder bis zu {0} Felder auf ein Feld ziehen, das kein Held sieht – dabei nie weiter vom nächsten imperialen Verbündeten weg als er gerade entfernt ist.</color>

**EN**  
> <b>? Attack point available?</b><br>
> <color=#FFD76A>Executive Order → which Imperial figure in reach receives the command: movement (up to 2 spaces, where to) – without an emergency: movement only, never an attack – the command spends your attack point; it also overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    Movement → which other Imperial figure in reach repositions up to 2 spaces, and where to: onto a tile no hero sees, or back toward your own line? – the Order spends your attack point and overrides the commanded figure's doctrine.</color><br>
> <color=#FFD76A>    No emergency? → do not attack on your own. A self-initiated attack is allowed only in one of these emergencies: he is under fire; a hero stands directly beside him; the mission objective is acutely threatened and only he can intervene; or he is the last surviving Imperial group – then he may fight as his doctrine allows. Without an emergency: give your Order, stay in cover behind the group you gave the order to, and do not drift away from it – never move farther from your nearest Imperial ally than it is right now; if no cover can be held, take one step back – never one forward.</color><br>
> <color=#FFD76A>    Neither Order nor attack possible? → this point becomes movement: hold position, or move up to {0} spaces onto a tile no hero sees – never moving farther from your nearest Imperial ally than it is right now.</color><br>
> <b>? Attack point(s) spent?</b><br>
> <color=#FFD76A>    Then this point is movement: hold position, or move up to {0} spaces onto a tile no hero sees – never moving farther from your nearest Imperial ally than it is right now.</color>


---

## 2. Gemeinsame Baum-Texte (doktrinunabhängige Sonderfälle)

---

## 3. Risiko-, Phasen- und Bedrohungszeilen (EXPLAIN = VOLL)

### Entscheidungs-Begründungen (die „basis“-Zeilen der Primärziel-Wahl)

---

## 4. Rest des KI-Textbestands (Vollständigkeit)

---

## 5. Wer würfelt welche Doktrin? (Kuratierte Würfel-Gewichte)

*Die Doktrin wird je Aktivierung gewürfelt; die Gewichte stehen pro Gruppe fest (`tactical_pattern_weights.json`, öffentliche Datei). Gelistete Gruppen haben eine kuratierte Verteilung; alle übrigen Pool-Figuren laufen über den alternden Legacy-Zweig (Rollen-Klassifikation nach Haltung) — derselbe Text, andere Würfel. Haltungs-Modifikatoren unten.Additive Verschiebungen können Muster sogar neu öffnen (defend + Wache).*

- **E-Web-Schütze** (DG007) — guard 100%
- **E-Web-Schütze (Elite)** (DG008) — guard 100%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG071) — hunter 80%, aggressive 20%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG076) — hunter 60%, aggressive 30%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG077) — hunter 60%, aggressive 30%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG080) — hunter 60%, aggressive 30%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG089) — hunter 60%, aggressive 30%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG090) — hunter 60%, aggressive 30%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG072) — aggressive 50%, hunter 40%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG084) — aggressive 50%, hunter 40%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG087) — hunter 50%, aggressive 40%, guard 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG083) — aggressive 45%, guard 30%, blocker 25%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG085) — guard 45%, aggressive 35%, blocker 15%, hunter 5%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG086) — aggressive 50%, guard 25%, hunter 15%, blocker 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG081) — guard 50%, blocker 35%, aggressive 15%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG088) — aggressive 55%, hunter 30%, guard 15%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG073) — blocker 45%, aggressive 25%, guard 20%, hunter 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG082) — aggressive 40%, blocker 30%, guard 20%, hunter 10%
- **Imperialer Offizier** (DG004) — support 55%, guard 25%, blocker 10%, hunter 10%
- **Imperialer Offizier** (DG005) — support 55%, guard 25%, blocker 10%, hunter 10%
- **Imperialer Offizier (Elite)** (DG006) — support 55%, guard 20%, hunter 15%, blocker 10%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG075) — aggressive 45%, guard 35%, blocker 15%, hunter 5%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG078) — aggressive 45%, guard 35%, blocker 15%, hunter 5%
- **ISB-Infiltratoren** (DG068) — aggressive 40%, hunter 40%, guard 20%
- **ISB-Infiltratoren (Elite)** (DG069) — aggressive 40%, hunter 40%, guard 20%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG079) — aggressive 40%, hunter 40%, guard 20%
- **Loth-Katze** (DG060) — hunter 50%, aggressive 35%, guard 15%
- **Loth-Katze (Elite)** (DG061) — hunter 50%, aggressive 35%, guard 15%
- **Tusken-Räuber** (DG021) — aggressive 40%, hunter 40%, guard 20%
- **Tusken-Räuber (Elite)** (DG022) — aggressive 40%, hunter 40%, guard 20%
- **Trandoshanische Jäger** (DG015) — hunter 45%, aggressive 40%, guard 15%
- **Trandoshanische Jäger (Elite)** (DG016) — hunter 45%, aggressive 40%, guard 15%
- **HK-Attentäterdroiden** (DG026) — hunter 45%, aggressive 40%, guard 15%
- **HK-Attentäterdroiden (Elite)** (DG027) — hunter 45%, aggressive 40%, guard 15%
- **Weequay-Piraten** (DG042) — hunter 45%, aggressive 40%, guard 15%
- **Weequay-Piraten** (DG043) — hunter 45%, aggressive 40%, guard 15%
- **Weequay-Piraten (Elite)** (DG044) — hunter 45%, aggressive 40%, guard 15%
- **Clawditen-Gestaltwandler** (DG053) — hunter 45%, aggressive 40%, guard 15%
- **Clawditen-Gestaltwandler** (DG054) — hunter 45%, aggressive 40%, guard 15%
- **Clawditen-Gestaltwandler (Elite)** (DG055) — hunter 45%, aggressive 40%, guard 15%
- **Suchdroide** (DG011) — hunter 45%, aggressive 30%, guard 25%
- **Suchdroide** (DG012) — hunter 45%, aggressive 30%, guard 25%
- **Suchdroide (Elite)** (DG013) — hunter 45%, aggressive 30%, guard 25%
- **Rote Garde** (DG009) — guard 55%, aggressive 25%, blocker 20%
- **Rote Garde (Elite)** (DG010) — guard 55%, aggressive 25%, blocker 20%
- **Gamorreanische Wachen** (DG039) — guard 55%, aggressive 25%, blocker 20%
- **Gamorreanische Wachen** (DG040) — guard 55%, aggressive 25%, blocker 20%
- **Gamorreanische Wachen (Elite)** (DG041) — guard 55%, aggressive 25%, blocker 20%
- **Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)** (DG074) — guard 55%, aggressive 25%, blocker 20%
- **AT-ST** (DG014) — blocker 40%, aggressive 35%, hunter 15%, guard 10%
- **SC2-M-Repulsorpanzer** (DG025) — blocker 40%, aggressive 35%, hunter 15%, guard 10%
- **AT-DP** (DG046) — blocker 40%, aggressive 35%, hunter 15%, guard 10%
- **Nexu** (DG017) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Nexu (Elite)** (DG018) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Wampa** (DG028) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Wampa (Elite)** (DG029) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Rancor** (DG045) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Taurücken-Reiter (Elite)** (DG062) — blocker 48%, guard 26%, aggressive 22%, hunter 4%
- **Bantha-Reiter (Elite)** (DG065) — blocker 48%, guard 26%, aggressive 22%, hunter 4%

*Haltungs-Modifikatoren (additiv, Klammerung bei 0):*

- defend: aggressive -25, blocker +15, guard +20, hunter -10
