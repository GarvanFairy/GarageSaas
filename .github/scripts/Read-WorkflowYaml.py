"""Read-only duplicate-safe YAML decoding. Uses existing Azure CLI PyYAML,
without invoking Azure CLI, authentication, network access or file writes.
"""
import json
import re
import sys
from pathlib import Path
import yaml

class UniqueLoader(yaml.BaseLoader):
    pass

def mapping(loader, node):
    result = {}
    for key, value in node.value:
        k = loader.construct_object(key, deep=True)
        if not isinstance(k, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_-]*", k):
            raise ValueError("Unsupported key")
        if k in result:
            raise ValueError("Duplicate key")
        result[k] = loader.construct_object(value, deep=True)
    return result

UniqueLoader.add_constructor("tag:yaml.org,2002:map", mapping)
try:
    if len(sys.argv) != 2:
        raise ValueError("Arguments")
    data = Path(sys.argv[1]).read_bytes()
    if len(data) > 262144:
        raise ValueError("Size")
    text = data.decode("utf-8-sig")
    tokens = list(yaml.scan(text, Loader=UniqueLoader))
    if len(tokens) > 20000 or any(isinstance(t, (yaml.AliasToken, yaml.AnchorToken, yaml.TagToken)) for t in tokens):
        raise ValueError("Unsupported indirection")
    document = yaml.load(text, Loader=UniqueLoader)
    if not isinstance(document, dict):
        raise ValueError("Root")
    print(json.dumps(document, ensure_ascii=True))
except Exception:
    print("Workflow YAML decoding failed; details suppressed.", file=sys.stderr)
    sys.exit(1)
