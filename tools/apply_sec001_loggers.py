from pathlib import Path

# SessionProtocolLogger
p = Path("src/MailLoadTester.Core/SmtpSessionLogger.cs")
t = p.read_text()
old = '''    public void LogClient(byte[] buffer, int offset, int count) =>
        _target.LogClient(Decode(buffer, offset, count));

    public void LogServer(byte[] buffer, int offset, int count) =>
        _target.LogServer(Decode(buffer, offset, count));

    static string Decode(byte[] buffer, int offset, int count) =>
        System.Text.Encoding.ASCII.GetString(buffer, offset, count).TrimEnd('\\r', '\\n');
'''
# fix escaping - actual file uses \r in source as chars
old = (
    "    public void LogClient(byte[] buffer, int offset, int count) =>\n"
    "        _target.LogClient(Decode(buffer, offset, count));\n"
    "\n"
    "    public void LogServer(byte[] buffer, int offset, int count) =>\n"
    "        _target.LogServer(Decode(buffer, offset, count));\n"
    "\n"
    "    static string Decode(byte[] buffer, int offset, int count) =>\n"
    "        System.Text.Encoding.ASCII.GetString(buffer, offset, count).TrimEnd('\\r', '\\n');\n"
)
new = (
    "    public void LogClient(byte[] buffer, int offset, int count) =>\n"
    "        _target.LogClient(ProtocolLogRedaction.DecodeClientOrServer(buffer, offset, count, AuthenticationSecretDetector));\n"
    "\n"
    "    public void LogServer(byte[] buffer, int offset, int count) =>\n"
    "        _target.LogServer(ProtocolLogRedaction.DecodeClientOrServer(buffer, offset, count, AuthenticationSecretDetector));\n"
)
if "ProtocolLogRedaction.DecodeClientOrServer" in t and "static string Decode" not in t:
    print("SessionProtocolLogger already patched")
elif old not in t:
    # try with single-quoted char literals as in C#
    old2 = old.replace("'\\r', '\\n'", "'\\r', '\\n'")
    if old not in t:
        # show snippet
        idx = t.find("LogClient(byte[] buffer")
        print("NOT FOUND Session block, snippet:")
        print(repr(t[idx:idx+350]))
        raise SystemExit(1)
else:
    p.write_text(t.replace(old, new, 1))
    print("SessionProtocolLogger patched")

# ProtocolPathObserver
p = Path("src/MailLoadTester.Core/ProtocolPathObserver.cs")
t = p.read_text()
changed = False
old_c = (
    "    public void LogClient(byte[] buffer, int offset, int count)\n"
    "    {\n"
    "        var text = Decode(buffer, offset, count).Trim();\n"
)
new_c = (
    "    public void LogClient(byte[] buffer, int offset, int count)\n"
    "    {\n"
    "        // SEC-AUDIT-001: redact AUTH secrets before any detail is enqueued.\n"
    "        var text = ProtocolLogRedaction.DecodeClientOrServer(buffer, offset, count, AuthenticationSecretDetector).Trim();\n"
)
if "ProtocolLogRedaction.DecodeClientOrServer" in t and "var text = Decode(buffer, offset, count).Trim();" not in t:
    print("ProtocolPathObserver already patched")
elif old_c not in t:
    idx = t.find("public void LogClient(byte[] buffer")
    print("LogClient not found", repr(t[idx:idx+200]))
    raise SystemExit(2)
else:
    t = t.replace(old_c, new_c, 1)
    changed = True

old_s = (
    "    public void LogServer(byte[] buffer, int offset, int count)\n"
    "    {\n"
    "        var text = Decode(buffer, offset, count).Trim();\n"
)
new_s = (
    "    public void LogServer(byte[] buffer, int offset, int count)\n"
    "    {\n"
    "        var text = ProtocolLogRedaction.DecodeClientOrServer(buffer, offset, count, AuthenticationSecretDetector).Trim();\n"
)
if old_s in t:
    t = t.replace(old_s, new_s, 1)
    changed = True
if changed:
    p.write_text(t)
    print("ProtocolPathObserver patched")
else:
    print("ProtocolPathObserver no change or already done")
