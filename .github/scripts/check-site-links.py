#!/usr/bin/env python3
"""Resolve every internal link in the built site against the output directory.

The site's navigation is hand-written — cards, callouts and cross-references in
markdown — so a renamed or moved page becomes a 404 that nothing else catches. Hugo
does not fail a build for a link that points nowhere.

Usage: check-site-links.py <public-dir> <base-path>
"""
import re
import sys
from pathlib import Path

IGNORED_SUFFIXES = (".css", ".js", ".json", ".xml", ".png", ".svg", ".ico", ".webp", ".txt")


def main() -> int:
    root = Path(sys.argv[1])
    base = sys.argv[2]

    if not root.is_dir():
        print(f"::error::{root} is not a directory")
        return 1

    broken: dict[str, str] = {}
    checked = 0

    for page in root.rglob("*.html"):
        html = page.read_text(errors="ignore")
        for href in re.findall(rf'href=["\']?({re.escape(base)}[^"\'\s>]*)', html):
            href = href.split("#")[0]
            if not href or href.endswith(IGNORED_SUFFIXES):
                continue

            checked += 1
            relative = href[len(base):]
            target = root / relative

            if (
                target.is_file()
                or (target / "index.html").is_file()
                or (root / (relative.rstrip("/") + ".html")).is_file()
            ):
                continue

            broken.setdefault(href, str(page.relative_to(root)))

    print(f"internal links checked: {checked}")

    if broken:
        print(f"::error::{len(broken)} internal link(s) do not resolve")
        for href, source in sorted(broken.items()):
            print(f"  {href}  (from {source})")
        return 1

    print("all internal links resolve")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
