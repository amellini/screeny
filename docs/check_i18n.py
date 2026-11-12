"""Controlla le traduzioni: stringhe UI (Screeny/Strings/*.json) e catalogo (docs/i18n/scrcpy-options.<lingua>.json).

Uso: python docs/check_i18n.py   (dalla radice del repository)
"""
import json, pathlib, re, sys

root = pathlib.Path(__file__).resolve().parent.parent
catalog = json.loads((root / "docs/scrcpy-options.json").read_text(encoding="utf-8"))
errors = []

# Stringhe UI: stesse chiavi e stessi segnaposto {n} dell'italiano.
strings_dir = root / "Screeny/Strings"
base = json.loads((strings_dir / "it.json").read_text(encoding="utf-8"))
for path in sorted(strings_dir.glob("*.json")):
	other = json.loads(path.read_text(encoding="utf-8"))
	for key in base.keys() - other.keys():
		errors.append(f"{path.name}: manca {key}")
	for key in other.keys() - base.keys():
		errors.append(f"{path.name}: chiave in più {key}")
	for key in base.keys() & other.keys():
		if sorted(re.findall(r"\{\d+\}", base[key])) != sorted(re.findall(r"\{\d+\}", other[key])):
			errors.append(f"{path.name}: segnaposto diversi in {key}")

# Catalogo: ogni gruppo, categoria, opzione e descrizione di valore tradotta, niente chiavi sconosciute.
options = {o["name"]: o for g in catalog["groups"] for o in g["options"]}
for path in sorted((root / "docs/i18n").glob("scrcpy-options.*.json")):
	tr = json.loads(path.read_text(encoding="utf-8"))
	name = path.name
	if set(tr["categories"]) != {c["id"] for c in catalog["categories"]}:
		errors.append(f"{name}: categorie diverse dal catalogo")
	for g in catalog["groups"]:
		t = tr["groups"].get(g["id"], {})
		if not t.get("title") or not t.get("description"):
			errors.append(f"{name}: gruppo {g['id']} incompleto")
	for key in tr["options"].keys() - options.keys():
		errors.append(f"{name}: opzione sconosciuta {key}")
	for key, o in options.items():
		t = tr["options"].get(key)
		if not t or not t.get("label") or not t.get("description"):
			errors.append(f"{name}: opzione {key} senza label/description")
			continue
		described = {v["value"] for v in o.get("values", []) if v.get("description")}
		translated = set(t.get("values", {}))
		for v in described - translated:
			errors.append(f"{name}: {key} valore '{v}' non tradotto")
		for v in translated - described:
			errors.append(f"{name}: {key} valore '{v}' non esiste")

print("\n".join(errors) or "OK: traduzioni complete")
sys.exit(1 if errors else 0)
