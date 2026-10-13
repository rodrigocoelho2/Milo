"""Signs up on a running BodyOps, imports the sample CorpoOps backup and screenshots every screen on a computer and a phone.

Usage: python screenshots.py <base-url> <output-folder>
Fails (exit 1) when a page throws a JavaScript error.
"""
import pathlib
import sys

from playwright.sync_api import sync_playwright

base = sys.argv[1].rstrip("/")
out = pathlib.Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
sample = pathlib.Path(__file__).resolve().parents[2] / "BodyOps.UnitTest" / "Backups" / "corpoops-sample.json"

errors = []

with sync_playwright() as p:
    browser = p.chromium.launch()

    def watch(page, label):
        page.on("pageerror", lambda e: errors.append(f"{label}: {e}"))

    desktop = browser.new_context(viewport={"width": 1360, "height": 900})
    page = desktop.new_page()
    watch(page, "desktop")

    page.goto(base + "/")
    page.screenshot(path=str(out / "desktop-landing.png"), full_page=True)

    page.goto(base + "/signup")
    page.fill("#authUser", "Screens")
    page.fill("#authPass", "screens-2026")
    page.click("#authSubmit")
    page.wait_for_url("**/plan")

    body = sample.read_text(encoding="utf-8")
    result = page.evaluate(
        """async body => (await fetch('/api/backup', { method: 'POST', headers: { 'X-BodyOps': '1', 'Content-Type': 'application/json' }, body })).json()""",
        body,
    )
    print("import:", result)

    screens = [
        ("today", "/today"),
        ("week", "/week?date=2026-07-21&day=2026-07-21"),
        ("plan", "/plan?day=2"),
        ("plan-diet", "/plan?day=2&tab=diet"),
        ("weight", "/weight"),
        ("settings", "/settings"),
    ]
    for name, path in screens:
        page.goto(base + path)
        page.wait_for_timeout(700)
        page.screenshot(path=str(out / f"desktop-{name}.png"), full_page=True)

    phone = browser.new_context(viewport={"width": 390, "height": 844}, device_scale_factor=2, is_mobile=True, has_touch=True)
    phone.add_cookies(desktop.cookies())
    mobile = phone.new_page()
    watch(mobile, "phone")
    for name, path in screens:
        mobile.goto(base + path)
        mobile.wait_for_timeout(700)
        mobile.screenshot(path=str(out / f"phone-{name}.png"), full_page=True)

    signed_out = browser.new_context(viewport={"width": 390, "height": 844}, device_scale_factor=2, is_mobile=True)
    anon = signed_out.new_page()
    watch(anon, "phone signed out")
    for name, path in [("landing", "/"), ("signin", "/signin")]:
        anon.goto(base + path)
        anon.wait_for_timeout(400)
        anon.screenshot(path=str(out / f"phone-{name}.png"), full_page=True)

    browser.close()

print("\n".join(errors) if errors else "no page errors")
sys.exit(1 if errors else 0)
