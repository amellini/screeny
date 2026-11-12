import json, re, sys

catalog = json.load(open(sys.argv[1], encoding="utf-8"))
cli = open(sys.argv[2], encoding="utf-8").read()
longopts = set(re.findall(r'\.longopt = "([^"]+)"', cli))

opts = [o for g in catalog["groups"] for o in g["options"]]
names = [o["name"] for o in opts]
assert len(names) == len(set(names)), "nomi duplicati"
byname = {o["name"]: o for o in opts}
types = {"flag", "enum", "multiEnum", "int", "float", "bitrate", "text", "file", "color", "optionalValue"}

for o in opts:
	n = o["name"]
	assert n in longopts, f"{n}: non esiste in cli.c"
	assert o["type"] in types, f"{n}: tipo {o['type']}"
	assert o.get("label") and o.get("description"), f"{n}: label/description"
	if o["type"] in ("enum", "multiEnum"):
		assert o.get("values"), f"{n}: values mancanti"
		vals = {v["value"] for v in o["values"]}
		if "default" in o:
			assert set(o["default"].split(o.get("separator", "\0"))) <= vals, f"{n}: default fuori dai values"
	for ref in o.get("requires", []) + o.get("conflicts", []):
		rn, _, rv = ref.partition("=")
		assert rn in byname, f"{n}: riferimento sconosciuto {rn}"
		if rv:
			assert rv in {v["value"] for v in byname[rn]["values"]}, f"{n}: valore {rv} non valido per {rn}"

skipped = sorted(longopts - set(names))
print(f"OK: {len(opts)} opzioni in {len(catalog['groups'])} gruppi")
print("Non incluse:", ", ".join(skipped))
