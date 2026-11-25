# Rework of the Stitch design proposals (Issue #590, gate feedback round 1).
#
# Deterministic post-processing of the generated single-file HTML proposals:
#  - injects ONE canonical chrome per variant (sidebar nav, header, ribbon bar)
#    built from the REAL ribbon actions extracted from Docs/design/inventory/html
#  - replaces externally generated logo images with one consistent inline logo
#  - removes invented features (sync badges, PSD2/EBICS/CAMT/MT940, fake search,
#    command palette, multi-category budget-purpose assignment, activation
#    toggles on BudgetPurpose, invented ledgers etc.)
#  - collapses long explanation paragraphs behind an info button
#  - view-specific fixes (budget-purpose screens, variantB home, ...)
#
# Usage: python rework-proposals.py
# Output: reworked *.html next to the originals; PNGs are re-rendered by
#         render-proposals.js afterwards.

import json
import os
import re
from lxml import html as lh
from lxml import etree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
PROP = os.path.join(REPO, "Docs", "design", "proposals")
INV = os.path.join(REPO, "Docs", "design", "inventory", "html", "desktop")
VIEWS = json.load(open(os.path.join(HERE, "proposal-views.json"), encoding="utf8"))["views"]

# ---------------------------------------------------------------- real ribbons

def extract_ribbon(view_id):
    """Real ribbon groups from the inventory HTML of a view."""
    path = os.path.join(INV, view_id + ".html")
    if not os.path.exists(path):
        return []
    doc = lh.fromstring(open(path, encoding="utf8", errors="replace").read())
    groups = []
    for g in doc.xpath('//div[contains(@class,"fm-ribbon-group fm-ribbon-group-desktop")]'):
        footer = g.xpath('.//div[contains(@class,"fm-ribbon-group-footer")]')
        glabel = footer[0].text_content().strip() if footer else ""
        body = g.xpath('.//div[contains(@class,"fm-ribbon-group-body")]')
        btns = []
        if body:
            for b in body[0].xpath('.//button[contains(@class,"fm-ribbon-btn")]'):
                use = b.xpath(".//use")
                icon = use[0].get("href", "").split("#")[-1] if use else ""
                label = "".join(
                    b.xpath('.//span[contains(@class,"text") or contains(@class,"text-inline")]//text()')
                ).strip()
                size = "large" if "large" in b.get("class", "") else "small"
                if label:
                    btns.append((label, icon, size))
        if btns:
            groups.append((glabel, btns))
    return groups

RIBBONS = {v["id"]: extract_ribbon(v["id"]) for v in VIEWS}

# --------------------------------------------------------------- icon mapping

ICON = {
    "archive": "archive", "attachment": "attach_file", "back": "arrow_back",
    "bank": "account_balance", "calendar": "calendar_month", "chart": "bar_chart",
    "check": "check", "chevron-left": "chevron_left", "chevron-right": "chevron_right",
    "clear": "close", "delete": "delete", "download": "download", "edit": "edit",
    "external": "open_in_new", "filters": "filter_alt", "groups": "group",
    "key": "vpn_key", "layers": "layers", "list": "format_list_bulleted",
    "merge": "call_merge", "new": "add", "next": "arrow_forward", "plus": "add",
    "postings": "receipt_long", "prev": "arrow_back", "refresh": "refresh",
    "save": "save", "save-as": "save_as", "settings": "settings", "tag": "label",
    "trash": "delete", "undo": "undo", "upload": "upload", "view": "visibility",
}

def icon(name):
    return ICON.get(name, "circle")

# ------------------------------------------------------------------ nav model

NAV_MAIN = [
    ("start", "Start", "grid_view"),
    ("konten", "Konten", "account_balance_wallet"),
    ("kontakte", "Kontakte", "contacts"),
    ("sparplaene", "Sparpläne", "savings"),
    ("wertpapiere", "Wertpapiere", "trending_up"),
    ("kontoauszuege", "Kontoauszüge", "receipt_long"),
    ("budget", "Budget", "pie_chart"),
    ("berichte", "Berichte", "analytics"),
]
NAV_BOTTOM = [
    ("benutzerverwaltung", "Benutzerverwaltung", "group"),
    ("ki-zugaenge", "KI-Zugänge", "psychology"),
    ("einstellungen", "Einstellungen", "settings"),
    ("hilfe", "Hilfe", "help"),
    ("abmelden", "Abmelden", "logout"),
]

def view2nav(view_id):
    if view_id in ("home", "overlay-home-mass-import"):
        return "start"
    if "account" in view_id and "postings" not in view_id:
        return "konten"
    if "postings-account" in view_id:
        return "konten"
    if "contact" in view_id or "postings-contact" in view_id:
        return "kontakte"
    if "savings-plan" in view_id or "postings-savings-plan" in view_id:
        return "sparplaene"
    if "securit" in view_id or "postings-security" in view_id or "portfolio" in view_id:
        return "wertpapiere"
    if "statement" in view_id:
        return "kontoauszuege"
    if "budget" in view_id:
        return "budget"
    if "report" in view_id or "dashboard" in view_id:
        return "berichte"
    if "user" in view_id:
        return "benutzerverwaltung"
    if "mcp" in view_id:
        return "ki-zugaenge"
    if "setup" in view_id or "password" in view_id:
        return "einstellungen"
    if "help" in view_id:
        return "hilfe"
    return ""

NO_SIDEBAR = {"login", "register", "legal", "error"}

# ------------------------------------------------------------ canonical style

STYLE = """
<style id="fm-canonical">
/* --- canonical chrome injected by rework-proposals.py --- */
.fm-aside{position:fixed;left:0;top:0;bottom:0;width:232px;display:flex;flex-direction:column;
  background:var(--fm-bg2);border-right:1px solid var(--fm-border);z-index:60}
.fm-brand{display:flex;align-items:center;gap:10px;padding:16px 16px 14px;border-bottom:1px solid var(--fm-border)}
.fm-logo{width:34px;height:34px;border-radius:9px;display:flex;align-items:center;justify-content:center;flex-shrink:0}
.fm-brand-name{display:block;font-weight:600;font-size:15px;color:var(--fm-text);letter-spacing:-.01em}
.fm-brand-sub{display:block;font-size:10px;text-transform:uppercase;letter-spacing:.08em;color:var(--fm-muted)}
.fm-nav{flex:1;overflow-y:auto;padding:10px 8px;display:flex;flex-direction:column;gap:1px}
.fm-nav a{display:flex;align-items:center;gap:11px;padding:8px 10px;border-radius:8px;
  color:var(--fm-muted);font-size:13px;text-decoration:none}
.fm-nav a .material-symbols-outlined{font-size:19px;width:20px;text-align:center}
.fm-nav a.active{background:var(--fm-active);color:var(--fm-accent);font-weight:600}
.fm-nav a:hover{background:var(--fm-hover);color:var(--fm-text)}
.fm-nav-sep{height:1px;background:var(--fm-border);margin:8px 10px}
.fm-topbar{position:fixed;top:0;left:232px;right:0;height:52px;display:flex;align-items:center;
  justify-content:space-between;padding:0 24px;background:var(--fm-bg2);border-bottom:1px solid var(--fm-border);z-index:50}
.fm-page-title{font-size:15px;font-weight:600;color:var(--fm-text);letter-spacing:-.01em}
.fm-avatar{width:30px;height:30px;border-radius:50%;background:var(--fm-accent);color:#fff;
  display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700}
.fm-ribbon{position:fixed;top:52px;left:232px;right:0;display:flex;gap:14px;align-items:stretch;
  padding:8px 24px;background:var(--fm-bg3);border-bottom:1px solid var(--fm-border);z-index:40;
  overflow-x:auto;min-height:58px}
.fm-rib-group{display:flex;flex-direction:column}
.fm-rib-group-body{display:flex;gap:4px;align-items:stretch;flex:1}
.fm-rib-btn{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;
  min-width:60px;padding:5px 10px;border:none;border-radius:7px;background:transparent;
  color:var(--fm-text);font-size:11px;cursor:default}
.fm-rib-btn .material-symbols-outlined{font-size:20px}
.fm-rib-btn.primary{background:var(--fm-accent);color:#fff}
.fm-rib-caption{font-size:10px;text-transform:uppercase;letter-spacing:.06em;color:var(--fm-muted);
  text-align:center;border-top:1px solid var(--fm-border);padding-top:2px;margin-top:2px}
.fm-info{display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;
  border-radius:50%;background:var(--fm-bg3);border:1px solid var(--fm-border);cursor:help;
  color:var(--fm-muted);vertical-align:middle}
.fm-info .material-symbols-outlined{font-size:13px}
body{margin:0;padding-left:232px;padding-top:110px;background:var(--fm-bg);color:var(--fm-text)}
/* Variant B (icon rail) */
.fm-aside-b{width:72px;align-items:center}
.fm-aside-b .fm-brand{padding:14px 0;flex-direction:column;gap:4px}
.fm-aside-b .fm-brand-text{display:none}
.fm-aside-b .fm-brand-sub{display:none}
.fm-aside-b .fm-nav{padding:12px 0;align-items:center;gap:4px}
.fm-aside-b .fm-nav a{padding:0;width:44px;height:44px;justify-content:center;border-radius:12px;position:relative}
.fm-aside-b .fm-nav a .material-symbols-outlined{font-size:20px}
.fm-aside-b .fm-nav a .fm-nav-label{display:none}
.fm-aside-b .fm-nav-sep{width:32px;margin:8px 0}
.fm-topbar-b{left:72px}
.fm-ribbon-b{top:60px;left:72px;background:var(--fm-bg2);border-radius:0;min-height:64px;gap:10px;align-items:center}
.fm-ribbon-b .fm-rib-group{background:var(--fm-bg3);border:1px solid var(--fm-border);border-radius:14px;padding:6px 10px}
.fm-ribbon-b .fm-rib-btn{min-width:0;padding:4px 9px}
body.fm-body-b{padding-left:72px;padding-top:124px}
/* mobile */
.fm-aside-m{display:none}
body.fm-body-m{padding-left:0;padding-top:108px}
.fm-topbar-m{left:0}
.fm-ribbon-m{left:0;top:56px}
.fm-burger{display:flex;align-items:center;justify-content:center;width:36px;height:36px;
  border:none;border-radius:8px;background:transparent;color:var(--fm-muted)}
/* neutralize legacy offsets of the old generated chrome */
body>*{padding-left:0!important;margin-left:0!important;padding-top:0!important}
body>header,body>aside{position:fixed}
main{min-height:60vh}
</style>
"""

VAR_CSS = {
    "a": ":root{--fm-bg:#0b0e13;--fm-bg2:#10131a;--fm-bg3:#151922;--fm-border:#232a36;"
         "--fm-text:#e1e2ea;--fm-muted:#8e9aa8;--fm-accent:#6d8dff;--fm-active:#1c2333;--fm-hover:#171c26}"
         "body{font-family:'Inter',system-ui,sans-serif}",
    "b": ":root{--fm-bg:#0a0d12;--fm-bg2:#0e1219;--fm-bg3:#141a24;--fm-border:rgba(255,255,255,.09);"
         "--fm-text:#e8eef7;--fm-muted:#94a3b8;--fm-accent:#2dd4bf;--fm-active:rgba(45,212,191,.14);--fm-hover:rgba(255,255,255,.05)}"
         "body{font-family:'Plus Jakarta Sans','Inter',system-ui,sans-serif}",
}

LOGO_SVG = ('<svg width="22" height="22" viewBox="0 0 24 24" fill="none" '
            'xmlns="http://www.w3.org/2000/svg">'
            '<rect x="3" y="12" width="4.5" height="8" rx="1" fill="currentColor"/>'
            '<rect x="10" y="7" width="4.5" height="13" rx="1" fill="currentColor" opacity=".75"/>'
            '<rect x="17" y="3" width="4.5" height="17" rx="1" fill="currentColor" opacity=".5"/>'
            '</svg>')


def canon_aside(variant, device, active):
    """Canonical sidebar / icon rail. `device` is 'desktop' or 'mobile'."""
    if device == "mobile":
        return '<aside class="fm-aside fm-aside-m"></aside>'
    cls = "fm-aside" + (" fm-aside-b" if variant == "b" else "")
    logo_color = "#2dd4bf" if variant == "b" else "#6d8dff"
    logo_bg = ("background:linear-gradient(135deg,rgba(45,212,191,.18),rgba(139,92,246,.18));color:#2dd4bf"
               if variant == "b" else "background:#1c2333;color:#6d8dff")
    items = []
    for path, label, icn in NAV_MAIN + NAV_BOTTOM:
        sep = '<div class="fm-nav-sep"></div>' if path == "benutzerverwaltung" or path == "hilfe" else ""
        act = " active" if path == active else ""
        items.append(
            f'{sep}<a class="{act}" data-path="{path}" href="#" title="{label}">'
            f'<span class="material-symbols-outlined">{icn}</span>'
            f'<span class="fm-nav-label">{label}</span></a>'
        )
    return (
        f'<aside class="{cls}">'
        f'<div class="fm-brand"><span class="fm-logo" style="{logo_bg}">{LOGO_SVG}</span>'
        f'<div class="fm-brand-text"><span class="fm-brand-name">FinanceManager</span>'
        f'<span class="fm-brand-sub">Finanzverwaltung</span></div></div>'
        f'<nav class="fm-nav">{"".join(items)}</nav></aside>'
    )


def canon_header(variant, device, title):
    if device == "mobile":
        return (
            '<header class="fm-topbar fm-topbar-m">'
            '<div style="display:flex;align-items:center;gap:10px">'
            f'<button class="fm-burger"><span class="material-symbols-outlined">menu</span></button>'
            f'<span class="fm-page-title">{title}</span></div>'
            '<div class="fm-avatar">FM</div></header>'
        )
    return (
        '<header class="fm-topbar fm-topbar-b">' if variant == "b" else '<header class="fm-topbar">'
    ) + (
        f'<span class="fm-page-title">{title}</span><div class="fm-avatar">FM</div></header>'
    )


def canon_ribbon(variant, device, view_id):
    groups = RIBBONS.get(view_id) or []
    if not groups:
        return ""
    out = [f'<div class="fm-ribbon{" fm-ribbon-b" if variant == "b" else ""}{" fm-ribbon-m" if device == "mobile" else ""}">']
    for glabel, btns in groups:
        out.append('<div class="fm-rib-group"><div class="fm-rib-group-body">')
        for i, (label, icn, size) in enumerate(btns):
            prim = ' primary' if i == 0 and size == "large" and variant == "a" else ""
            out.append(
                f'<button class="fm-rib-btn{prim}" type="button" title="{label}">'
                f'<span class="material-symbols-outlined">{icon(icn)}</span>'
                f'<span>{label}</span></button>'
            )
        out.append("</div>")
        if glabel:
            out.append(f'<div class="fm-rib-caption">{glabel}</div>')
        out.append("</div>")
    out.append("</div>")
    return "".join(out)


# ------------------------------------------------------- content sanitization

# Text patterns that flag INVENTED functionality/decoration. Elements matching
# these (bounded size) get removed; inside longer prose the containing block
# is removed wholesale.
BAD_TEXT = re.compile(
    r"Lokal\s*/\s*Synchronisiert|[Ss]ynchronisiert(?:\s+(?:mit|vor|via|alle|gestern))?|"
    r"PSD2|EBICS|CAMT|MT940|XETRA|[Xx]etra|Tradegate|WM Datenservice|EZB|"
    r"Kurs-Sync|⌘K|Strg\+K|Live Mandant|Mandant|[Cc]luster|mTLS|SHA-?256|GoBD|BaFin|"
    r"revisionssicher|Batch-Abruf|Bankhäuser|Bankenschnittstelle|Institutional|"
    r"INSTITUTIONAL|Blazor Core|Arbeitsmappe|Cockpit|Latenz|Latency|Tick-Daten|"
    r"Intraday|Datenabdeckung|Reconciliation|Hauptbuch|Stichtag|Börsenplatz|"
    r"Online synchronisiert|Health Ping|Realtime|Aktivierungsstatus|"
    r"Ledger|Duplikaterkennung",
    re.I,
)

# elements that are allowed to contain those words legitimately is handled per-view.

def kill_bad(doc):
    """Remove small elements containing invented-feature text. Returns count."""
    removed = 0
    for _ in range(3):
        done = True
        for el in doc.xpath("//*[self::span or self::button or self::a or self::p or self::div or self::label or self::td or self::small or self::li]"):
            try:
                txt = el.text_content()
            except Exception:
                continue
            txtn = re.sub(r"\s+", " ", txt).strip()
            if not txtn or not BAD_TEXT.search(txtn):
                continue
            # only bounded elements: small chips/badges/buttons, or prose
            # paragraphs whose whole content is invented
            if len(txtn) > 140 and el.tag not in ("p",):
                continue
            parent = el.getparent()
            if parent is None or el.tag == "body":
                continue
            parent.remove(el)
            removed += 1
            done = False
        if done:
            break
    return removed


def kill_bad_inputs(doc):
    """Remove invented interactive controls: command-palette inputs, fake
    global search in header zones, checkboxes used as invented toggles."""
    removed = 0
    for el in doc.xpath("//input[@type='checkbox' and contains(@class,'peer')]"):
        # a toggle switch: remove the enclosing label/div (bounded)
        anc = el
        for _ in range(4):
            p = anc.getparent()
            if p is None or p.tag in ("main", "body", "section"):
                break
            anc = p
            if len(anc.text_content().strip()) < 200:
                break
        anc.getparent().remove(anc)
        removed += 1
    for el in doc.xpath("//input[contains(@placeholder,'⌘') or contains(@placeholder,'Strg+K') or contains(@placeholder,'Command')]"):
        el.getparent().remove(el)
        removed += 1
    return removed


def collapse_explanations(doc, view_id):
    """Move long explanation paragraphs behind an info button."""
    if view_id in ("help-view", "help-hub", "legal"):
        return 0
    changed = 0
    for el in doc.xpath("//main//p | //main//span"):
        txt = re.sub(r"\s+", " ", el.text_content()).strip()
        if len(txt) < 170:
            continue
        tip = etree.Element("span")
        tip.set("class", "fm-info")
        tip.set("title", txt[:900])
        ic = etree.SubElement(tip, "span")
        ic.set("class", "material-symbols-outlined")
        ic.text = "info"
        el.getparent().replace(el, tip)
        changed += 1
    return changed


def fix_logos(doc):
    """Replace externally generated logo <img>s with the canonical mark."""
    n = 0
    for img in doc.xpath("//img[contains(@src,'lh3.googleusercontent.com') or contains(@alt,'Logo') or contains(@alt,'logo')]"):
        frag = lh.fromstring(
            f'<span class="fm-logo" style="background:#1c2333;color:#6d8dff">{LOGO_SVG}</span>'
        )
        img.getparent().replace(img, frag)
        n += 1
    return n


def strip_stray_action_clusters(doc):
    """Remove leftover action-bar containers: direct children before <main>
    AND preceding siblings on every ancestor level of <main>."""
    removed = 0
    mains = doc.xpath("//main")
    if not mains:
        return 0
    main = mains[0]
    # collect ancestors of main up to body
    chain = []
    node = main
    while node is not None and node.tag != "body":
        chain.append(node)
        node = node.getparent()
    # for each level, drop preceding siblings that look like toolbars
    for node in chain:
        parent = node.getparent()
        if parent is None:
            continue
        for sib in list(parent):
            if sib is node:
                break
            if not isinstance(sib.tag, str) or sib.tag in (
                "aside", "header", "style", "script", "link", "title"
            ):
                continue
            nbtns = len(sib.xpath(".//button"))
            txt = sib.text_content().strip()
            looks_bar = (
                nbtns >= 2 and len(txt) < 3000
            ) or (
                nbtns >= 1 and len(txt) < 400 and sib.tag == "div"
            )
            if looks_bar:
                parent.remove(sib)
                removed += 1
    return removed


def strip_intoolbar(doc):
    """Remove old toolbar bands inside <main> (divs that are mostly action
    buttons). Table rows and forms are excluded."""
    removed = 0
    for main in doc.xpath("//main"):
        for el in main.xpath("./div | ./*/div"):
            if not isinstance(el.tag, str) or el.tag != "div":
                continue
            nbtns = len(el.xpath("./div//button | ./button"))
            txt = el.text_content().strip()
            if nbtns >= 3 and len(txt) < 500:
                el.getparent().remove(el)
                removed += 1
    return removed


# ------------------------------------------------------------ view rebuilds

def tile_a(title, big, sub, subcls=""):
    return (
        f'<div class="rounded-xl border border-[#232a36] bg-[#10131a] p-4 flex flex-col gap-1">'
        f'<span class="text-[10px] font-semibold uppercase tracking-wider text-[#8e9aa8]">{title}</span>'
        f'<span class="text-2xl font-semibold text-[#e1e2ea] tabular-nums">{big}</span>'
        f'<span class="text-xs text-[#8e9aa8]">{sub}</span></div>'
    )


def tile_b(title, big, sub):
    return (
        '<div class="rounded-2xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4 flex flex-col gap-1 shadow-lg">'
        f'<span class="text-[10px] font-semibold uppercase tracking-widest text-slate-400">{title}</span>'
        f'<span class="text-2xl font-bold text-teal-300 tabular-nums">{big}</span>'
        f'<span class="text-xs text-slate-400">{sub}</span></div>'
    )


def fav_tile_a(name, kind, summary):
    return (
        '<div class="rounded-xl border border-[#232a36] bg-[#10131a] p-4 flex flex-col gap-2">'
        '<div class="flex items-center justify-between">'
        f'<span class="text-sm font-semibold text-[#e1e2ea]">{name}</span>'
        f'<span class="text-[10px] px-2 py-0.5 rounded-full bg-[#1c2333] text-[#6d8dff] font-medium">{kind}</span></div>'
        f'<div class="text-xs text-[#8e9aa8]">{summary}</div>'
        '<div class="h-10 flex items-end gap-1 mt-1">'
        '<div class="w-1/6 h-2/5 rounded-sm bg-[#6d8dff]/30"></div><div class="w-1/6 h-3/5 rounded-sm bg-[#6d8dff]/40"></div>'
        '<div class="w-1/6 h-1/2 rounded-sm bg-[#6d8dff]/50"></div><div class="w-1/6 h-4/5 rounded-sm bg-[#6d8dff]/70"></div>'
        '<div class="w-1/6 h-3/5 rounded-sm bg-[#6d8dff]/50"></div><div class="w-1/6 h-full rounded-sm bg-[#6d8dff]"></div>'
        "</div></div>"
    )


def fav_tile_b(name, kind, summary):
    return (
        '<div class="rounded-2xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4 flex flex-col gap-2 shadow-lg">'
        '<div class="flex items-center justify-between">'
        f'<span class="text-sm font-semibold text-slate-100">{name}</span>'
        f'<span class="text-[10px] px-2 py-0.5 rounded-full bg-teal-400/10 text-teal-300 font-medium border border-teal-400/20">{kind}</span></div>'
        f'<div class="text-xs text-slate-400">{summary}</div>'
        '<div class="h-10 flex items-end gap-1 mt-1">'
        '<div class="w-1/6 h-2/5 rounded-sm bg-teal-400/25"></div><div class="w-1/6 h-3/5 rounded-sm bg-teal-400/35"></div>'
        '<div class="w-1/6 h-1/2 rounded-sm bg-teal-400/45"></div><div class="w-1/6 h-4/5 rounded-sm bg-teal-400/60"></div>'
        '<div class="w-1/6 h-3/5 rounded-sm bg-violet-400/50"></div><div class="w-1/6 h-full rounded-sm bg-violet-400"></div>'
        "</div></div>"
    )


def add_tile(cls_extra, border, txtcls):
    return (
        f'<button type="button" class="rounded-xl border-2 border-dashed {border} min-h-[104px] '
        f'flex flex-col items-center justify-center gap-1.5 {txtcls} {cls_extra}">'
        '<span class="material-symbols-outlined text-[22px]">add</span>'
        '<span class="text-xs font-medium">Kachel hinzufügen</span></button>'
    )


def rebuild_home(doc, variant, device):
    """Home = Mitteilungen + konfigurierbare KPI-Kacheln + Berichts-Favoriten
    (echte Funktionen der Startseite). Ersetzt den kompletten <main>-Inhalt."""
    mains = doc.xpath("//main")
    if not mains:
        return
    main = mains[0]
    for c in list(main):
        main.remove(c)
    if variant == "a":
        head_cls = "text-xs font-semibold uppercase tracking-wider text-[#8e9aa8] mb-3"
        box = (
            '<section class="rounded-xl border border-[#232a36] bg-[#10131a] px-4 py-3 '
            'flex items-center gap-3">'
            '<span class="material-symbols-outlined text-[20px] text-[#6d8dff]">notifications</span>'
            '<div class="flex-1"><div class="text-sm font-semibold text-[#e1e2ea]">Mitteilungen</div>'
            '<div class="text-xs text-[#8e9aa8]">2 neue Mitteilungen · Import abgeschlossen</div></div>'
            '<span class="material-symbols-outlined text-[18px] text-[#8e9aa8]">chevron_right</span></section>'
        )
        tiles = "".join([
            tile_a("Gesamtsaldo", "142.850,00 €", "+3.420,00 € vs. Vormonat"),
            tile_a("Konten", "6", "Giro, Tagesgeld, Depot, Kreditkarte"),
            tile_a("Offene Entwürfe", "3", "Kontoauszüge zur Prüfung"),
            tile_a("Budgetstatus", "68 %", "ausgeschöpft · im Rahmen"),
        ]) + add_tile("", "border-[#232a36]", "text-[#8e9aa8]")
        favs = fav_tile_a("Kontaktanalyse", "Berichts-Favorit · Monat", "Aufwand je Kontakt im laufenden Monat") \
             + fav_tile_a("Wertpapier-Projektion", "Berichts-Favorit · Monat", "Kurs-Entwicklung des Depots")
        inner = (
            '<div class="flex flex-col w-full gap-5 max-w-[1200px]">'
            + box
            + f'<section><h2 class="{head_cls}">Kennzahlen</h2>'
            + f'<div class="grid grid-cols-2 {"lg:grid-cols-4" if device == "desktop" else ""} gap-4">{tiles}</div></section>'
            + f'<section><h2 class="{head_cls}">Berichts-Favoriten</h2>'
            + f'<div class="grid grid-cols-1 {"lg:grid-cols-2" if device == "desktop" else ""} gap-4">{favs}</div></section>'
            + "</div>"
        )
    else:
        head_cls = "text-[11px] font-semibold uppercase tracking-widest text-slate-400 mb-3"
        box = (
            '<section class="rounded-2xl border border-white/10 bg-white/[0.04] backdrop-blur-md px-4 py-3 '
            'flex items-center gap-3 shadow-lg">'
            '<span class="material-symbols-outlined text-[20px] text-teal-300">notifications</span>'
            '<div class="flex-1"><div class="text-sm font-semibold text-slate-100">Mitteilungen</div>'
            '<div class="text-xs text-slate-400">2 neue Mitteilungen · Import abgeschlossen</div></div>'
            '<span class="material-symbols-outlined text-[18px] text-slate-500">chevron_right</span></section>'
        )
        tiles = "".join([
            tile_b("Gesamtsaldo", "142.850,00 €", "+3.420,00 € vs. Vormonat"),
            tile_b("Konten", "6", "Giro, Tagesgeld, Depot, Kreditkarte"),
            tile_b("Offene Entwürfe", "3", "Kontoauszüge zur Prüfung"),
            tile_b("Budgetstatus", "68 %", "ausgeschöpft · im Rahmen"),
        ]) + add_tile("", "border-white/15", "text-slate-400")
        favs = fav_tile_b("Kontaktanalyse", "Berichts-Favorit · Monat", "Aufwand je Kontakt im laufenden Monat") \
             + fav_tile_b("Wertpapier-Projektion", "Berichts-Favorit · Monat", "Kurs-Entwicklung des Depots")
        inner = (
            '<div class="flex flex-col w-full gap-6">'
            + box
            + f'<section><h2 class="{head_cls}">Kennzahlen</h2>'
            + f'<div class="grid grid-cols-2 {"lg:grid-cols-4" if device == "desktop" else ""} gap-4">{tiles}</div></section>'
            + f'<section><h2 class="{head_cls}">Berichts-Favoriten</h2>'
            + f'<div class="grid grid-cols-1 {"lg:grid-cols-2" if device == "desktop" else ""} gap-4">{favs}</div></section>'
            + "</div>"
        )
    frag = lh.fromstring(f'<div id="fm-home">{inner}</div>')
    main.append(frag)


def _text_els(doc, pattern):
    out = []
    rx = re.compile(pattern, re.I)
    for el in doc.xpath("//*[self::span or self::div or self::p or self::h2 or self::h3 or self::th or self::td or self::a or self::label or self::button]"):
        own = (el.text or "").strip()
        if own and rx.fullmatch(own):
            out.append(el)
    return out


# invented-feature phrases that poison whole blocks (camt import, sync, rollover…)
PHRASE_KILL = [
    "camt", "mt940", "psd2", "ebics", "finapi", "banking api", "banking-api",
    "auto-matching", "matching rate", "matching-rate", "ki-matching",
    "rollierend", "rollover", "restguthaben", "restbudget", "übertrag unverbrauchter",
    "unbefristet gültig", "100% aktiv",
    "synchronisations-status", "synchronisationsstatus", "synchronisationsintervall",
    "synchronisiert mit", "datensynchronisierung", "synchronisierungsstatus",
    "letzte synchronisation", "zuletzt synchronisiert", "synchronisation",
    "synchronisieren", "synchronisiert", "prioritätsstufe",
]


def kill_phrase_blocks(doc):
    """Remove the deepest block containing an invented-feature phrase, when the
    block is small enough to be a card/row rather than a page section."""
    removed = 0
    for ph in PHRASE_KILL:
        hits = doc.xpath(
            "//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜ', "
            "'abcdefghijklmnopqrstuvwxyzäöü'), $p)]", p=ph)
        for el in hits:
            tag = el.tag if isinstance(el.tag, str) else ""
            if tag in ("html", "body", "main", "table", "tbody", "thead",
                       "head", "style", "script"):
                continue
            # deepest container only (no descendant also containing phrase)
            if el.xpath(".//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜ',"
                        " 'abcdefghijklmnopqrstuvwxyzäöü'), $p)]", p=ph):
                continue
            txt = el.text_content().strip()
            if len(txt) > 900:
                continue
            # promote to a sensible block boundary (tile/row), not just the word
            target = el
            par = el.getparent()
            while (par is not None and isinstance(par.tag, str)
                   and par.tag in ("div", "li", "tr", "td", "section", "article")
                   and len(par.text_content().strip()) <= 700
                   and len(txt) <= 250):
                target = par
                txt = par.text_content().strip()
                par = par.getparent()
            par = target.getparent()
            if par is not None:
                par.remove(target)
                removed += 1
    return removed


def fix_budget_purpose(doc):
    """BudgetPurpose: genau EINE Kategorie, kein Aktiv-Status."""
    n = 0
    # "N Kategorien" / "Kategorien (N)" summaries -> singular
    for el in doc.xpath("//*[self::span or self::div or self::a or self::h2 or self::h3]"):
        t = re.sub(r"\s+", " ", el.text_content()).strip()
        if re.fullmatch(r"(?:Zugeordnete\s+)?Kategorien\s*\(\s*\d+\s*\)?", t) or re.fullmatch(r"\d+\s+Kategorien", t):
            el.text_content()  # keep children? simplest: replace whole text
            for c in list(el):
                el.remove(c)
            el.text = "Kategorie"
            n += 1
        elif re.fullmatch(r"Zugeordnete Kategorien", t) and len(list(el)) == 0:
            el.text = "Zugeordnete Kategorie"
            n += 1
    # table headers "Kategorien" -> "Kategorie"
    for el in doc.xpath("//th"):
        if re.fullmatch(r"Kategorien", el.text_content().strip()):
            el.text = "Kategorie"
            n += 1
    # category tables: keep only the first data row
    for tbl in doc.xpath("//table"):
        head = re.sub(r"\s+", " ", tbl.text_content()).strip()[:200]
        if "Kategorie" in head:
            tbody = tbl.xpath(".//tbody")
            if tbody:
                rows = tbody[0].xpath("./tr")
                for r in rows[1:]:
                    tbody[0].remove(r)
                    n += 1
    # invented fields of BudgetPurpose
    for el in doc.xpath("//*[self::label or self::span or self::div]"):
        own = re.sub(r"\s+", " ", (el.text or "").strip())
        if re.fullmatch(r"(Prioritätsstufe.*|Klassifizierung|Gültigkeitszeitraum.*|Aktivierungsstatus.*)", own, re.I):
            anc = el
            for _ in range(4):
                p = anc.getparent()
                if p is None or p.tag in ("form", "section", "main", "body"):
                    break
                anc = p
            if anc.getparent() is not None and len(anc.text_content().strip()) < 300:
                anc.getparent().remove(anc)
                n += 1
    # chips/lists that show several category chips: keep first
    for cont in doc.xpath("//*[self::div or self::td]"):
        chips = cont.xpath("./span[contains(@class,'rounded') or contains(@class,'chip') or contains(@class,'bg-')]")
        if len(chips) > 1 and all(len(c.text_content().strip()) < 30 for c in chips):
            joined = " ".join(c.text_content() for c in chips)
            if joined.count("Kategorie") == 0 and len(chips) > 1:
                for c in chips[1:]:
                    cont.remove(c)
                    n += 1
    return n


def _v(variant):
    """Style tokens per variant."""
    if variant == "a":
        return dict(
            card="rounded-xl border border-[#232a36] bg-[#10131a] p-5",
            h2="text-sm font-semibold text-[#e1e2ea] mb-4",
            lbl="block text-xs font-medium text-[#8e9aa8] mb-1.5",
            inp="w-full rounded-lg bg-[#0b0e13] border border-[#232a36] px-3 py-2 text-sm text-[#e1e2ea]",
            table="w-full text-sm",
            th="text-left text-[10px] font-semibold uppercase tracking-wider text-[#8e9aa8] py-2 px-3 border-b border-[#232a36]",
            td="py-3 px-3 border-b border-[#1a1f2b] text-[#e1e2ea]",
            td_m="py-3 px-3 border-b border-[#1a1f2b] text-[#8e9aa8]",
            chip="inline-block text-xs px-2 py-0.5 rounded-full bg-[#1c2333] text-[#6d8dff]",
            accent="text-[#6d8dff]",
        )
    return dict(
        card="rounded-2xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-5 shadow-lg",
        h2="text-sm font-semibold text-slate-100 mb-4",
        lbl="block text-xs font-medium text-slate-400 mb-1.5",
        inp="w-full rounded-xl bg-black/25 border border-white/10 px-3 py-2 text-sm text-slate-100",
        table="w-full text-sm",
        th="text-left text-[10px] font-semibold uppercase tracking-widest text-slate-400 py-2 px-3 border-b border-white/10",
        td="py-3 px-3 border-b border-white/5 text-slate-100",
        td_m="py-3 px-3 border-b border-white/5 text-slate-400",
        chip="inline-block text-xs px-2 py-0.5 rounded-full bg-teal-400/10 text-teal-300 border border-teal-400/20",
        accent="text-teal-300",
    )


PURPOSE_ROWS = [
    ("Lebenshaltung & Haushalt", "Kontaktgruppe · Einzelhandel", "Lebensmittel", "Exakte Buchungen", "2"),
    ("Mobilität", "Kontaktgruppe · Tankstellen", "Transport", "Exakte Buchungen", "1"),
    ("Wohnen", "Kontaktgruppe · Vermieter", "Miete & Nebenkosten", "Gesamtbudget", "1"),
    ("Altersvorsorge", "Sparplan · ETF-Sparplan", "Sparen", "Gesamtbudget", "1"),
    ("Versicherungen", "Kontakt · Versicherer", "Versicherungen", "Exakte Buchungen", "1"),
    ("Freizeit", "Kontaktgruppe · Freizeit", "–", "Exakte Buchungen", "0"),
]


def rebuild_purpose_card(doc, variant):
    """BudgetPurpose-Detailkarte: Stammdaten + Quelle (Kontakt/Kontaktgruppe/
    Sparplan) + EINE Kategorie + Bewertungsart + zugehörige Budgetregeln."""
    mains = doc.xpath("//main")
    if not mains:
        return
    main = mains[0]
    for c in list(main):
        main.remove(c)
    v = _v(variant)
    rule_items = [
        ("Lebenshaltung monatlich", "750,00 €", "Monatlich"),
        ("Haushalt Wochenmarkt", "120,00 €", "Monatlich"),
        ("Jahresbudget Drogerie", "600,00 €", "Jährlich"),
    ]
    rules_html = "".join(
        f'<div class="flex items-center justify-between gap-3 py-2.5' + (' border-b border-[#1a1f2b]' if variant == "a" and i < 2 else ' border-b border-white/5' if i < 2 else '') + '">'
        f'<div><div class="text-sm font-medium {"text-[#e1e2ea]" if variant == "a" else "text-slate-100"}">{n}</div>'
        f'<div class="text-xs {"text-[#8e9aa8]" if variant == "a" else "text-slate-400"}">{i_}</div></div>'
        f'<span class="text-sm font-semibold tabular-nums {v["accent"]}">{a}</span></div>'
        for i, (n, a, i_) in enumerate(rule_items))
    inner = f'''
<div class="flex flex-col w-full gap-5 max-w-[1100px]">
  <section class="{v['card']}">
    <h2 class="{v['h2']}">Stammdaten</h2>
    <div class="grid grid-cols-1 gap-4">
      <div><label class="{v['lbl']}">Name des Budgetzwecks</label>
        <div class="{v['inp']}">Lebenshaltung &amp; Haushalt</div></div>
      <div><label class="{v['lbl']}">Beschreibung</label>
        <div class="{v['inp']}">Laufende Grundbedürfnisse und Haushaltsführung</div></div>
    </div>
  </section>
  <section class="{v['card']}">
    <h2 class="{v['h2']}">Zuordnung</h2>
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <div><label class="{v['lbl']}">Quelle</label>
        <div class="{v['inp']} flex items-center justify-between">Kontaktgruppe · Einzelhandel
          <span class="material-symbols-outlined text-[16px]">expand_more</span></div></div>
      <div><label class="{v['lbl']}">Kategorie</label>
        <div class="{v['inp']} flex items-center justify-between">Lebensmittel
          <span class="material-symbols-outlined text-[16px]">expand_more</span></div></div>
      <div><label class="{v['lbl']}">Bewertungsart</label>
        <div class="{v['inp']} flex items-center justify-between">Exakte Buchungen
          <span class="material-symbols-outlined text-[16px]">expand_more</span></div></div>
    </div>
  </section>
  <section class="{v['card']}">
    <h2 class="{v['h2']}">Budgetregeln</h2>
    {rules_html}
  </section>
</div>'''
    main.append(lh.fromstring(f'<div id="fm-purpose-card">{inner}</div>'))


def rebuild_purpose_list(doc, variant):
    mains = doc.xpath("//main")
    if not mains:
        return
    main = mains[0]
    for c in list(main):
        main.remove(c)
    v = _v(variant)
    rows = "".join(
        f'<tr><td class="{v["td"]} font-medium">{n}</td>'
        f'<td class="{v["td_m"]}">{s}</td>'
        f'<td class="{v["td"]}"><span class="{v["chip"]}">{k}</span></td>'
        f'<td class="{v["td_m"]}">{b}</td>'
        f'<td class="{v["td_m"]} text-center">{r}</td></tr>'
        for n, s, k, b, r in PURPOSE_ROWS)
    inner = f'''
<div class="flex flex-col w-full gap-4">
  <div class="{v['inp']} flex items-center gap-2 max-w-md">
    <span class="material-symbols-outlined text-[18px]">search</span>
    <span class="{"text-[#8e9aa8]" if variant == "a" else "text-slate-500"}">Budgetzweck suchen…</span>
  </div>
  <section class="{v['card']} p-0 overflow-hidden">
    <table class="{v['table']}">
      <thead><tr>
        <th class="{v['th']}">Name</th><th class="{v['th']}">Quelle</th>
        <th class="{v['th']}">Kategorie</th><th class="{v['th']}">Bewertungsart</th>
        <th class="{v['th']} text-center">Regeln</th>
      </tr></thead>
      <tbody>{rows}</tbody>
    </table>
  </section>
</div>'''
    main.append(lh.fromstring(f'<div id="fm-purpose-list">{inner}</div>'))


SPECIAL = {
    "home": lambda doc, variant, device: rebuild_home(doc, variant, device),
    "card-budget-purpose": lambda doc, variant, device: rebuild_purpose_card(doc, variant),
    "list-budget-purposes": lambda doc, variant, device: rebuild_purpose_list(doc, variant),
}


def ensure_icon_font(doc):
    head = doc.xpath("//head")
    if not head:
        return
    head = head[0]
    if not head.xpath(".//link[contains(@href,'Material+Symbols')]"):
        link = etree.Element("link")
        link.set("href", "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&display=swap")
        link.set("rel", "stylesheet")
        head.append(link)


def process_file(path, variant, device, view_id, title):
    src = open(path, encoding="utf8", errors="replace").read()
    doc = lh.fromstring(src)
    body = doc.xpath("//body")[0]
    body.set("class", (body.get("class", "") + f" fm-body-{variant}" + (" fm-body-m" if device == "mobile" else "")).strip())

    # 1. canonical chrome -------------------------------------------------
    for el in doc.xpath("//aside"):
        el.getparent().remove(el)
    for el in doc.xpath("//header"):
        el.getparent().remove(el)
    removed_clusters = strip_stray_action_clusters(doc)

    has_sidebar = view_id not in NO_SIDEBAR
    if has_sidebar:
        body.insert(0, lh.fromstring(canon_header(variant, device, title)))
        body.insert(0, lh.fromstring(canon_aside(variant, device, view2nav(view_id))))
        rib = canon_ribbon(variant, device, view_id)
        if rib:
            # insert ribbon right after header
            hdr = doc.xpath("//header")[0]
            hdr.addnext(lh.fromstring(rib))
    else:
        # public page: slim brand bar
        logo_bg = "background:linear-gradient(135deg,rgba(45,212,191,.18),rgba(139,92,246,.18));color:#2dd4bf" if variant == "b" else "background:#1c2333;color:#6d8dff"
        bar = lh.fromstring(
            '<header class="fm-topbar" style="left:0">'
            f'<div style="display:flex;align-items:center;gap:10px">'
            f'<span class="fm-logo" style="{logo_bg}">{LOGO_SVG}</span>'
            f'<span class="fm-page-title">FinanceManager</span></div></header>'
        )
        body.insert(0, bar)

    # 2. clean content ----------------------------------------------------
    fix_logos(doc)
    n_bad = kill_bad(doc)
    n_inp = kill_bad_inputs(doc)
    n_intb = strip_intoolbar(doc)
    n_phr = kill_phrase_blocks(doc)
    n_expl = collapse_explanations(doc, view_id)
    ensure_icon_font(doc)

    # 3. view-specific rebuilds --------------------------------------------
    special = SPECIAL.get(view_id)
    if special:
        special(doc, variant, device)

    # 4. inject canonical style -------------------------------------------
    head = doc.xpath("//head")[0]
    st = etree.Element("style")
    st.set("id", "fm-canonical")
    st.text = STYLE.replace("<style id=\"fm-canonical\">", "").replace("</style>", "")
    head.append(st)
    st2 = etree.Element("style")
    st2.text = VAR_CSS[variant]
    head.append(st2)

    out = lh.tostring(doc, encoding="unicode", method="html", doctype="<!DOCTYPE html>")
    open(path, "w", encoding="utf8").write(out)
    return dict(bad=n_bad, inputs=n_inp, expl=n_expl, clusters=removed_clusters, intb=n_intb, phr=n_phr)


# ------------------------------------------------------------------- driver

def main():
    import sys
    only = set()
    if "--only" in sys.argv:
        i = sys.argv.index("--only")
        only = set(sys.argv[i + 1].split(","))
    report = {}
    for view in VIEWS:
        vid, title = view["id"], view["title"]
        if only and vid not in only:
            continue
        for variant in ("a", "b"):
            vdir = f"variant{variant.upper()}"
            for device in ("desktop", "mobile"):
                p = os.path.join(PROP, vdir, device, vid + ".html")
                if not os.path.exists(p):
                    continue
                r = process_file(p, variant, device, vid, title)
                report[f"{vdir}/{device}/{vid}"] = r
                print(f"{vdir}/{device}/{vid:38s} bad={r['bad']:2d} inp={r['inputs']} expl={r['expl']} clusters={r['clusters']}")
    json.dump(report, open(os.path.join(PROP, "rework-report.json"), "w", encoding="utf8"), indent=1)


if __name__ == "__main__":
    main()
